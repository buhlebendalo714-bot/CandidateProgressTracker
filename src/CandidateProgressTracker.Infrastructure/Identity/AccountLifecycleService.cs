using System.Data;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using CandidateProgressTracker.Application.Accounts;
using CandidateProgressTracker.Domain.Accounts;
using CandidateProgressTracker.Infrastructure.Persistence;
using CandidateProgressTracker.Infrastructure.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace CandidateProgressTracker.Infrastructure.Identity;

public sealed class AccountLifecycleService(TrackerDbContext db, UserManager<IdentityUser> users) : IAccountLifecycleService
{
    private static readonly string[] AllowedRoles=["Administrator","Mentor","Candidate"];
    private const string Denied="The account operation is unavailable. Refresh and check your access.";
    private static string Version(IdentityUser u,AccountAccessState? s)=>$"{u.ConcurrencyStamp}:{s?.PermissionVersion ?? 0}";
    private static string Hash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static bool EmailValid(string email)=>email.Length<=256 && MailAddress.TryCreate(email,out var parsed) && parsed.Address==email;
    // Lock order: affected cohort gates (sorted), lifecycle gate, user/role/state
    // rows. Workflow writers already take their cohort gate first. Routing reads
    // never authorize; current roles/state are reread inside Serializable below.
    private async Task GateAsync(string actor,string? target=null)
    {
        var cohorts=await db.Database.SqlQuery<Guid>($"SELECT DISTINCT CohortId AS [Value] FROM CohortMemberships WITH(NOLOCK) WHERE UserId={actor} OR UserId={target}").ToListAsync();
        foreach(var cohort in cohorts.Order())await CohortWorkflowGate.LockAsync(db,cohort);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'AccountLifecycle',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51010,'Account operation is busy; retry.',1;");
    }
    public async Task<ManagedAccount?> GetAsync(string userId,string actor)
    {
        if(!await AdministratorAccess.IsCurrentAsync(db,actor))return null;
        var u=await users.FindByIdAsync(userId);if(u is null)return null;
        var s=await db.AccountAccessStates.AsNoTracking().SingleOrDefaultAsync(s=>s.UserId==userId);
        return new(u.Id,u.UserName!,u.Email,string.Join(", ",await users.GetRolesAsync(u)),s?.Enabled ?? true,Version(u,s));
    }
    public async Task<AccountOperation> CreateAsync(string userName,string email,string role,string actor)
    {
        userName=userName.Trim();email=email.Trim();
        if(userName.Length is 0 or >256 || !EmailValid(email) || !AllowedRoles.Contains(role))return new(false,"Enter a valid username, unique email and role.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);await GateAsync(actor);
        if(!await AdministratorAccess.IsCurrentAsync(db,actor))return new(false,Denied);
        if(await db.Users.AnyAsync(u=>u.NormalizedEmail==users.NormalizeEmail(email)))return new(false,"That email is already assigned.");
        var u=new IdentityUser {UserName=userName,Email=email,LockoutEnabled=true};
        var r=await users.CreateAsync(u);if(!r.Succeeded)return Errors(r);
        r=await users.AddToRoleAsync(u,role);if(!r.Succeeded)return Errors(r);
        var s=new AccountAccessState {UserId=u.Id,Enabled=false,ChangedBy=actor,ChangedAt=DateTimeOffset.UtcNow,Reason="Account created; activation required"};db.Add(s);
        var receipt=await IssueCoreAsync(u,s,"Activation",actor,"Account created; activation required");
        await tx.CommitAsync();return new(true,Credential:receipt);
    }
    public async Task<AccountOperation> UpdateAsync(string userId,string email,string[] roles,string version,string reason,string actor)
    {
        email=email.Trim();roles=roles.Distinct().Order().ToArray();
        if(!EmailValid(email)||roles.Length==0||roles.Any(r=>!AllowedRoles.Contains(r))||!ReasonValid(reason))return new(false,"Enter a valid unique email, roles and a reason of 1–1000 characters.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);await GateAsync(actor,userId);
        var target=await TargetAsync(userId,version,actor);if(target is null)return new(false,Denied);var(u,s)=target.Value;
        if(await LastAdminAsync(u,s,roles.Contains("Administrator")))return new(false,"The last active Administrator cannot lose access.");
        if(await db.Users.AnyAsync(x=>x.Id!=userId&&x.NormalizedEmail==users.NormalizeEmail(email)))return new(false,"That email is already assigned.");
        u.Email=email;u.EmailConfirmed=false;var result=await users.UpdateAsync(u);if(!result.Succeeded)return Errors(result);
        var old=await users.GetRolesAsync(u);result=await users.RemoveFromRolesAsync(u,old.Except(roles));if(!result.Succeeded)return Errors(result);
        result=await users.AddToRolesAsync(u,roles.Except(old));if(!result.Succeeded)return Errors(result);
        await InvalidateAsync(u,s,actor,reason,"Profile/roles changed");await tx.CommitAsync();return new(true);
    }
    public async Task<AccountOperation> DeactivateAsync(string userId,string version,string reason,string actor)
    {
        if(!ReasonValid(reason))return new(false,"A reason of 1–1000 characters is required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);await GateAsync(actor,userId);
        var target=await TargetAsync(userId,version,actor);if(target is null)return new(false,Denied);var(u,s)=target.Value;
        if(await LastAdminAsync(u,s,false))return new(false,"The last active Administrator cannot lose access.");
        s.Enabled=false;await InvalidateAsync(u,s,actor,reason,"Deactivated");await tx.CommitAsync();return new(true);
    }
    public async Task<AccountOperation> IssueAsync(string userId,string kind,string version,string reason,string actor)
    {
        if(kind is not ("Activation" or "Recovery")||!ReasonValid(reason))return new(false,"Select activation/recovery and enter a reason of 1–1000 characters.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);await GateAsync(actor,userId);
        var target=await TargetAsync(userId,version,actor);if(target is null)return new(false,Denied);var(u,s)=target.Value;
        if((kind=="Recovery"&&!s.Enabled)||(kind=="Activation"&&s.Enabled))return new(false,"Use recovery for an active account and activation for an inactive account.");
        var receipt=await IssueCoreAsync(u,s,kind,actor,reason);await tx.CommitAsync();return new(true,Credential:receipt);
    }
    public async Task<AccountOperation> RedeemAsync(string userName,string credential,string password)
    {
        const string invalid="This code is invalid, expired or already used. Ask your Administrator for a new activation or password reset code.";
        if(credential.Length is <20 or >100||password.Length is <12 or >128)return new(false,invalid);
        var u=await users.FindByNameAsync(userName.Trim());if(u is null)return new(false,invalid);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);await GateAsync(u.Id,u.Id);
        await db.Entry(u).ReloadAsync();
        var hash=Hash(credential);var now=DateTimeOffset.UtcNow;
        var token=await db.AccountCredentials.SingleOrDefaultAsync(c=>c.UserId==u.Id&&c.TokenHash==hash&&c.UsedAt==null&&c.RevokedAt==null&&c.ExpiresAt>now);
        var s=await db.AccountAccessStates.SingleOrDefaultAsync(s=>s.UserId==u.Id);
        if(token is null||s is null||(token.Kind=="Recovery"&&!s.Enabled)||(token.Kind=="Activation"&&s.Enabled))return new(false,invalid);
        foreach(var validator in users.PasswordValidators){var result=await validator.ValidateAsync(users,u,password);if(!result.Succeeded)return Errors(result);}
        u.PasswordHash=users.PasswordHasher.HashPassword(u,password);u.EmailConfirmed=true;u.AccessFailedCount=0;u.LockoutEnd=null;
        token.UsedAt=now;s.Enabled=true;
        await InvalidateAsync(u,s,u.Id,"Single-use credential redeemed",token.Kind+" completed");await tx.CommitAsync();return new(true);
    }
    private async Task<(IdentityUser User,AccountAccessState State)?> TargetAsync(string id,string version,string actor)
    {
        if(!await AdministratorAccess.IsCurrentAsync(db,actor))return null;
        var u=await users.FindByIdAsync(id);if(u is null)return null;
        var s=await db.AccountAccessStates.SingleOrDefaultAsync(s=>s.UserId==id);
        if(Version(u,s)!=version)return null;
        if(s is null)s=new(){UserId=id,Enabled=true,ChangedBy=actor,ChangedAt=DateTimeOffset.UtcNow,Reason="Existing account managed"};
        return(u,s);
    }
    private async Task<bool> LastAdminAsync(IdentityUser u,AccountAccessState s,bool retainsAdmin)
    {
        if(!s.Enabled||retainsAdmin||!await users.IsInRoleAsync(u,"Administrator"))return false;
        return !await(from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where r.NormalizedName=="ADMINISTRATOR"&&ur.UserId!=u.Id&&!db.AccountAccessStates.Any(s=>s.UserId==ur.UserId&&!s.Enabled) select ur.UserId).AnyAsync();
    }
    private async Task<CredentialReceipt> IssueCoreAsync(IdentityUser u,AccountAccessState s,string kind,string actor,string reason)
    {
        await InvalidateAsync(u,s,actor,reason,kind+" issued");
        var token=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));var now=DateTimeOffset.UtcNow;var expiry=now.AddHours(kind=="Activation"?24:1);
        db.AccountCredentials.Add(new(){UserId=u.Id,Kind=kind,TokenHash=Hash(token),IssuedAt=now,ExpiresAt=expiry,IssuedBy=actor});await db.SaveChangesAsync();
        return new(u.UserName!,token,expiry,kind);
    }
    private async Task InvalidateAsync(IdentityUser u,AccountAccessState s,string actor,string reason,string action)
    {
        db.AuditActorId=actor;
        if(db.Entry(s).State==EntityState.Detached)db.Add(s);
        var now=DateTimeOffset.UtcNow;
        foreach(var c in await db.AccountCredentials.Where(c=>c.UserId==u.Id&&c.UsedAt==null&&c.RevokedAt==null).ToListAsync())c.RevokedAt=now;
        s.PermissionVersion++;s.ChangedBy=actor;s.ChangedAt=now;s.Reason=reason.Trim();
        var result=await users.UpdateSecurityStampAsync(u);if(!result.Succeeded)throw new InvalidOperationException("Account session invalidation failed.");
        db.AccountChanges.Add(new(){UserId=u.Id,ActorId=actor,OccurredAt=now,Action=action,Reason=reason.Trim()});await db.SaveChangesAsync();
    }
    private static bool ReasonValid(string reason)=>!string.IsNullOrWhiteSpace(reason)&&reason.Trim().Length<=1000;
    private static AccountOperation Errors(IdentityResult r)=>new(false,string.Join(" ",r.Errors.Select(e=>e.Description)));
}
