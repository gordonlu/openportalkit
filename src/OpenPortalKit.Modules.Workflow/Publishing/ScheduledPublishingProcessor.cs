namespace OpenPortalKit.Modules.Workflow.Publishing;

public sealed class ScheduledPublishingProcessor
{
    private readonly IPublishingWorkflowItemStore _workflowStore;
    private readonly PublishingWorkflowService _workflowService;
    private readonly IReadOnlyDictionary<string, IScheduledPublishingTarget> _targets;

    public ScheduledPublishingProcessor(
        IPublishingWorkflowItemStore workflowStore,
        PublishingWorkflowService workflowService,
        IEnumerable<IScheduledPublishingTarget> targets)
    {
        _workflowStore = workflowStore ?? throw new ArgumentNullException(nameof(workflowStore));
        _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
        ArgumentNullException.ThrowIfNull(targets);
        _targets = targets.ToDictionary(target => target.TargetType, StringComparer.Ordinal);
    }

    public async Task<ScheduledPublishingBatchResult> ProcessDueAsync(
        DateTimeOffset asOf,
        int take,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var due = await _workflowStore.ListScheduledDueAsync(asOf, take, cancellationToken);
        var published = 0;
        var skipped = 0;
        var failures = new List<ScheduledPublishingFailure>();

        foreach (var item in due)
        {
            if (!_targets.TryGetValue(item.TargetType, out var target))
            {
                failures.Add(new(item.Id, item.TargetType, item.TargetId,
                    $"No scheduled publishing target handles '{item.TargetType}'."));
                continue;
            }

            var targetResult = await target.PublishAsync(item.TargetId, actorId, cancellationToken);
            if (!targetResult.Succeeded)
            {
                failures.Add(new(item.Id, item.TargetType, item.TargetId, string.Join(" ", targetResult.Errors)));
                continue;
            }

            var transition = await _workflowService.TransitionAsync(item, new WorkflowTransitionRequest(
                WorkflowAction.Publish,
                actorId,
                OccurredAt: asOf,
                Readiness: new WorkflowPublicationReadiness(true, true, true)), cancellationToken);
            if (transition.Succeeded)
            {
                published++;
            }
            else if (transition.Errors.Any(error => error.Contains("changed", StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
            }
            else
            {
                failures.Add(new(item.Id, item.TargetType, item.TargetId, string.Join(" ", transition.Errors)));
            }
        }

        return new ScheduledPublishingBatchResult(due.Count, published, skipped, failures);
    }
}

public sealed record ScheduledPublishingBatchResult(
    int DueCount,
    int PublishedCount,
    int SkippedCount,
    IReadOnlyList<ScheduledPublishingFailure> Failures);

public sealed record ScheduledPublishingFailure(
    Guid WorkflowItemId,
    string TargetType,
    string TargetId,
    string Error);
