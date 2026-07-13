using System.Text.Json;

namespace OpenPortalKit.Cli.Checks;

public static class CheckReportWriter
{
    public static void WriteText(TextWriter writer, CheckReport report)
    {
        foreach (var result in report.Results)
        {
            writer.WriteLine($"{StatusLabel(result.Status),-4} {result.Code} {result.Target}: {result.Message}");
        }

        writer.WriteLine();
        writer.WriteLine(
            $"{report.Name}: {report.PassedCount} passed, {report.WarningCount} warnings, {report.FailedCount} failed.");
    }

    public static void WriteJson(TextWriter writer, CheckReport report)
    {
        var payload = new
        {
            report.Name,
            Status = report.IsSuccessful ? "passed" : "failed",
            Summary = new
            {
                Passed = report.PassedCount,
                Warnings = report.WarningCount,
                Failed = report.FailedCount
            },
            Results = report.Results.Select(result => new
            {
                result.Code,
                Status = result.Status.ToString().ToLowerInvariant(),
                result.Target,
                result.Message
            })
        };

        writer.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string StatusLabel(CheckStatus status) => status switch
    {
        CheckStatus.Passed => "PASS",
        CheckStatus.Warning => "WARN",
        CheckStatus.Failed => "FAIL",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
