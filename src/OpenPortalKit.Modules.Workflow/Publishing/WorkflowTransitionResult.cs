namespace OpenPortalKit.Modules.Workflow.Publishing;

public sealed record WorkflowTransitionResult(
    bool Succeeded,
    PublishingWorkflowItem? Item,
    IReadOnlyList<string> Errors)
{
    public static WorkflowTransitionResult Success(PublishingWorkflowItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new WorkflowTransitionResult(true, item, Array.Empty<string>());
    }

    public static WorkflowTransitionResult Failure(params string[] errors)
    {
        return new WorkflowTransitionResult(false, null, errors);
    }
}
