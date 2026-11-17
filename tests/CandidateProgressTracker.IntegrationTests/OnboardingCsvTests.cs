using CandidateProgressTracker.Web.Pages.Admin.Cohorts;
namespace CandidateProgressTracker.IntegrationTests;
public sealed class OnboardingCsvTests
{
    [Theory]
    [InlineData("=SUM(1,2)", "\"'=SUM(1,2)\"")]
    [InlineData("  +cmd", "\"'  +cmd\"")]
    [InlineData("-1", "\"'-1\"")]
    [InlineData("@formula", "\"'@formula\"")]
    [InlineData("\t=1", "\"'\t=1\"")]
    [InlineData("a\r\nb", "\"'a\r\nb\"")]
    [InlineData("a,\"b", "\"a,\"\"b\"")]
    [InlineData("ordinary.name", "\"ordinary.name\"")]
    public void Export_quotes_and_neutralizes_spreadsheet_cells(string value,string expected) =>
        Assert.Equal(expected,OnboardingBatchModel.CsvCell(value));
}
