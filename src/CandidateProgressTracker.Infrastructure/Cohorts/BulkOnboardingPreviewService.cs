using System.Data;
using System.Net.Mail;
using System.Text;
using CandidateProgressTracker.Application.Cohorts;
using CandidateProgressTracker.Domain.Cohorts;
using CandidateProgressTracker.Infrastructure.Identity;
using CandidateProgressTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CandidateProgressTracker.Infrastructure.Cohorts;

public sealed class BulkOnboardingPreviewService(TrackerDbContext db, UserManager<IdentityUser> users)
    : IBulkOnboardingPreviewService
{
    public async Task<BulkOnboardingPreview?> PreviewAsync(Guid cohortId, DateOnly startDate, byte[] csv, string actor)
    {
        // Read-only transaction: current service authorization precedes matching data.
        await using var tx = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        if (!await AdministratorAccess.IsCurrentAsync(db, actor)) return null;
        var cohort = await db.Cohorts.AsNoTracking().SingleOrDefaultAsync(c => c.CohortId == cohortId && !c.IsArchived);
        if (cohort is null) return null;
        BulkOnboardingPreview Invalid(int row, string field, string message) =>
            new(cohortId, cohort.Name, startDate, [], [new(row, field, message)]);
        if (startDate == default) return Invalid(0, "StartDate", "Select one membership start date for the batch.");
        if (csv.Length == 0 || csv.Length > IBulkOnboardingPreviewService.MaximumBytes)
            return Invalid(0, "File", "Upload a nonempty UTF-8 CSV no larger than 1 MB (1,048,576 bytes).");
        string text;
        try { text = new UTF8Encoding(false, true).GetString(csv).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { return Invalid(0, "File", "Export the file as CSV UTF-8 and upload it again."); }
        var parsed = Parse(text);
        if (parsed.Error is not null) return Invalid(parsed.Error.Row, parsed.Error.Field, parsed.Error.Message);
        var records = parsed.Rows;
        if (records.Count == 0) return Invalid(1, "Header", "Use the downloaded template with UserName and optional Email columns.");
        var header = records[0].Fields.Select(f => f.Trim()).ToArray();
        if (header.Length is <1 or >2 || header.Distinct(StringComparer.OrdinalIgnoreCase).Count() != header.Length ||
            !header.Contains("UserName", StringComparer.OrdinalIgnoreCase) ||
            header.Any(h => !h.Equals("UserName", StringComparison.OrdinalIgnoreCase) && !h.Equals("Email", StringComparison.OrdinalIgnoreCase)))
            return Invalid(records[0].Line, "Header", "Only UserName and Email columns are supported. UserName is required; do not include passwords, roles or names.");
        var data = records.Skip(1).Where(r => r.Fields.Any(f => !string.IsNullOrWhiteSpace(f))).ToList();
        if (data.Count is 0 or > IBulkOnboardingPreviewService.MaximumCandidates)
            return Invalid(0, "File", "Include between 1 and 100 candidate rows; completely blank rows are ignored.");
        var nameIndex = Array.FindIndex(header, h => h.Equals("UserName", StringComparison.OrdinalIgnoreCase));
        var emailIndex = Array.FindIndex(header, h => h.Equals("Email", StringComparison.OrdinalIgnoreCase));
        string Name(CsvRow r) => r.Fields.ElementAtOrDefault(nameIndex)?.Trim() ?? "";
        string Email(CsvRow r) => emailIndex < 0 ? "" : r.Fields.ElementAtOrDefault(emailIndex)?.Trim() ?? "";
        var duplicateNames = data.GroupBy(r => users.NormalizeName(Name(r))).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var duplicateEmails = data.Where(r => Email(r).Length > 0).GroupBy(r => users.NormalizeEmail(Email(r))).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var result = new List<OnboardingPreviewRow>();
        foreach (var row in data)
        {
            var name = Name(row); var email = Email(row); var issues = new List<OnboardingIssue>();
            void Error(string field, string message) => issues.Add(new(row.Line, field, message));
            if (row.Fields.Count != header.Length) Error("Row", "Use the same number of columns as the header; quote values containing commas.");
            if (name.Length is 0 or >256) Error("UserName", "Enter a username of 1 to 256 characters.");
            if (duplicateNames.Contains(users.NormalizeName(name))) Error("UserName", "Duplicate normalized username in this file; keep one row.");
            if (email.Length > 0 && (email.Length > 256 || !MailAddress.TryCreate(email, out var address) || address.Address != email))
                Error("Email", "Enter a valid email of up to 256 characters.");
            if (duplicateEmails.Contains(users.NormalizeEmail(email))) Error("Email", "Duplicate normalized email in this file; use a unique email for each candidate.");
            var matched = issues.Count == 0 ? await users.FindByNameAsync(name) : null;
            var action = OnboardingAction.Conflict;
            var explanation = "Correct the row and upload again.";
            if (issues.Count == 0 && matched is null)
            {
                if (email.Length == 0) Error("Email", "Email is required for a new pending account under the existing lifecycle rules.");
                else if (await db.Users.AnyAsync(u => u.NormalizedEmail == users.NormalizeEmail(email))) Error("Email", "Email belongs to another account; handle it individually.");
                var proposed = new IdentityUser { UserName = name, Email = email };
                foreach (var validator in users.UserValidators)
                {
                    var valid = await validator.ValidateAsync(users, proposed);
                    foreach (var error in valid.Errors) Error(error.Code.Contains("Email") ? "Email" : "UserName", error.Description);
                }
                if (issues.Count == 0) { action = OnboardingAction.Create; explanation = "Would create a pending Candidate and cohort membership; activation is administrator-mediated."; }
            }
            else if (issues.Count == 0 && matched is not null)
            {
                var roles = await users.GetRolesAsync(matched);
                if (roles.Count != 1 || roles[0] != "Candidate") Error("UserName", "Account must have only the Candidate role; handle role conflicts individually.");
                if (!await AccountAvailability.IsActiveAsync(db, matched.Id) || matched.LockoutEnd > DateTimeOffset.UtcNow)
                    Error("UserName", "Account is inactive, pending activation or locked; handle it individually.");
                if (email.Length > 0 && users.NormalizeEmail(email) != matched.NormalizedEmail)
                    Error("Email", "Email does not match the existing account; profile updates are not part of onboarding.");
                var memberships = await db.CohortMemberships.AsNoTracking().Where(m => m.CohortId == cohortId && m.UserId == matched.Id).ToListAsync();
                var now = DateTimeOffset.UtcNow;
                var batchStart = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(2));
                if (memberships.Any(m => m.MembershipKind != MembershipKind.Candidate || m.StartsAt > now || (m.EndsAt != null && (m.EndsAt <= now || m.EndsAt <= batchStart))))
                    Error("Membership", "Ended, future or conflicting membership exists; handle it individually.");
                if (issues.Count == 0 && memberships.Any(m => m.StartsAt <= now && (m.EndsAt == null || m.EndsAt > now)))
                {
                    action = OnboardingAction.Skip; explanation = "Already enrolled; no duplicate membership would be created.";

                }
                else if (issues.Count == 0) { action = OnboardingAction.EnrolExisting; explanation = "Would enrol the matched Candidate; credentials, roles and profile stay unchanged."; }
            }
            if (issues.Count > 0) { action = OnboardingAction.Conflict; explanation = "Resolve individually or correct/remove this row and upload again."; }
            result.Add(new(row.Line, name, email, matched?.UserName, matched?.Email, action, explanation, issues, matched?.Id));
        }
        // Never SaveChanges, create identities, issue credentials or persist uploaded rows.
        return new(cohortId, cohort.Name, startDate, result, []);
    }

    private sealed record CsvRow(int Line, List<string> Fields);
    private static (List<CsvRow> Rows, OnboardingIssue? Error) Parse(string text)
    {
        var rows = new List<CsvRow>(); var fields = new List<string>(); var value = new StringBuilder();
        var quoted = false; var closed = false; var line = 1; var firstLine = 1;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { value.Append('"'); i++; } else { quoted = false; closed = true; } }
                else { value.Append(c); if (c == '\n') line++; }
                continue;
            }
            if (c == '"' && value.Length == 0 && !closed) { quoted = true; continue; }
            if (c == ',' && fields.Count >= 1) return (rows, new(firstLine, "CSV", "Only UserName and Email columns are supported; quote commas inside values."));
            if (c == ',') { fields.Add(value.ToString()); value.Clear(); closed = false; continue; }
            if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                fields.Add(value.ToString()); if (fields.Any(f => !string.IsNullOrWhiteSpace(f))) rows.Add(new(firstLine, fields)); if (rows.Count > 101) return (rows, new(firstLine, "File", "Include no more than 100 candidate rows.")); fields = []; value.Clear(); closed = false; firstLine = ++line; continue;
            }
            if (closed || c == '"') return (rows, new(firstLine, "CSV", "Malformed quoting; export using CSV UTF-8 and upload again."));
            value.Append(c);
        }
        if (quoted) return (rows, new(firstLine, "CSV", "Close the quoted field or export using CSV UTF-8 again."));
        if (value.Length > 0 || fields.Count > 0 || closed) { fields.Add(value.ToString()); rows.Add(new(firstLine, fields)); }
        return (rows, null);
    }
}
