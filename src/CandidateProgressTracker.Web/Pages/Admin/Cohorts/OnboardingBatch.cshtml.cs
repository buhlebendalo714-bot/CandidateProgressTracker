using System.Security.Claims;
using System.Text;
using CandidateProgressTracker.Application.Cohorts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CandidateProgressTracker.Web.Pages.Admin.Cohorts;
public class OnboardingBatchModel(IBulkOnboardingImportService imports) : PageModel
{
    public OnboardingImportResult Result { get; private set; } = null!;
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    public async Task<IActionResult> OnGetExportAsync(Guid id)
    {
        Response.Headers.CacheControl="no-store";
        var result=await imports.GetAsync(id,Actor);
        if(result is null)return NotFound();
        var csv=new StringBuilder("CSV row,UserName,Status,Details\r\n");
        foreach(var row in result.Rows)
            csv.Append(row.Row).Append(',').Append(CsvCell(row.UserName)).Append(',')
                .Append(CsvCell(row.Status)).Append(',').Append(CsvCell(row.Message ?? "")).Append("\r\n");
        var encoding=new UTF8Encoding(true);
        return File(encoding.GetPreamble().Concat(encoding.GetBytes(csv.ToString())).ToArray(),
            "text/csv; charset=utf-8", $"onboarding-{id:D}-results.csv");
    }
    public static string CsvCell(string value)
    {
        // Quoting alone does not stop spreadsheet formula evaluation. Prefix risky
        // cells with a literal apostrophe, including leading whitespace/control chars.
        var trimmed=value.TrimStart();
        if(value.Any(c=>c is '\r' or '\n' or '\t') ||
            (trimmed.Length>0 && "=+-@".Contains(trimmed[0]))) value="'"+value;
        return "\""+value.Replace("\"","\"\"")+"\"";
    }
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Response.Headers.CacheControl="no-store";
        var result=await imports.GetAsync(id,Actor); if(result is null)return NotFound();
        Result=result;return Page();
    }
    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        Response.Headers.CacheControl="no-store";
        var result=await imports.ResumeAsync(id,Actor);if(result is null)return NotFound();
        Result=result;return Page();
    }
}
