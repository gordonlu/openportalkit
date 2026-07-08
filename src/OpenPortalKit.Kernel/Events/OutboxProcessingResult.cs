namespace OpenPortalKit.Kernel.Events;

public sealed record OutboxProcessingResult(
    int ProcessedCount,
    int FailedCount,
    int SkippedCount)
{
    public int TotalCount => ProcessedCount + FailedCount + SkippedCount;
}
