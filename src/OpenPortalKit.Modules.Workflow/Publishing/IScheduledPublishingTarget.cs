namespace OpenPortalKit.Modules.Workflow.Publishing;

public interface IScheduledPublishingTarget
{
    string TargetType { get; }

    Task<ScheduledPublishingTargetResult> PublishAsync(
        string targetId,
        Guid actorId,
        CancellationToken cancellationToken = default);
}

public sealed record ScheduledPublishingTargetResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static ScheduledPublishingTargetResult Success() => new(true, Array.Empty<string>());

    public static ScheduledPublishingTargetResult Failure(params string[] errors) => new(false, errors);
}
