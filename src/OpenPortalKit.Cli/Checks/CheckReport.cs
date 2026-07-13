namespace OpenPortalKit.Cli.Checks;

public enum CheckStatus
{
    Passed,
    Warning,
    Failed
}

public sealed record CheckResult(
    string Code,
    CheckStatus Status,
    string Target,
    string Message);

public sealed record CheckReport(
    string Name,
    IReadOnlyList<CheckResult> Results)
{
    public bool IsSuccessful => Results.All(result => result.Status != CheckStatus.Failed);

    public int PassedCount => Results.Count(result => result.Status == CheckStatus.Passed);

    public int WarningCount => Results.Count(result => result.Status == CheckStatus.Warning);

    public int FailedCount => Results.Count(result => result.Status == CheckStatus.Failed);
}
