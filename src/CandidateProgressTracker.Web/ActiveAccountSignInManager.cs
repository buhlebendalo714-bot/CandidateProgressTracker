using System.Security.Claims;
using CandidateProgressTracker.Infrastructure.Identity;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
namespace CandidateProgressTracker.Web;

public sealed class ActiveAccountSignInManager(
    UserManager<IdentityUser> users,IHttpContextAccessor context,
    IUserClaimsPrincipalFactory<IdentityUser> claims,IOptions<IdentityOptions> options,
    ILogger<SignInManager<IdentityUser>> logger,IAuthenticationSchemeProvider schemes,
    IUserConfirmation<IdentityUser> confirmation,TrackerDbContext db)
    : SignInManager<IdentityUser>(users,context,claims,options,logger,schemes,confirmation)
{
    public override async Task<bool> CanSignInAsync(IdentityUser user)=>
        await AccountAvailability.IsActiveAsync(db,user.Id)&&await base.CanSignInAsync(user);
    public override async Task<IdentityUser?> ValidateSecurityStampAsync(ClaimsPrincipal? principal)
    {
        var user=await base.ValidateSecurityStampAsync(principal);
        if(user is null||!await AccountAvailability.IsActiveAsync(db,user.Id))return null;
        // Also invalidates cookies when roles were changed outside lifecycle UI.
        var current=(await UserManager.GetRolesAsync(user)).Order().ToArray();
        var claimed=principal!.FindAll(Options.ClaimsIdentity.RoleClaimType).Select(c=>c.Value).Order().ToArray();
        return current.SequenceEqual(claimed)?user:null;
    }
}
