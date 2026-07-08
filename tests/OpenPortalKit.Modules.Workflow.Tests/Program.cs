using OpenPortalKit.Kernel.Audit;
using OpenPortalKit.Modules.Workflow.Publishing;

var tests = new (string Name, Func<Task> Run)[]
{
    ("content can move through review approval and publish", ContentCanMoveThroughReviewApprovalAndPublish),
    ("rejections require review comments", RejectionsRequireReviewComments),
    ("publish requires title slug and summary readiness", PublishRequiresTitleSlugAndSummaryReadiness),
    ("schedule publish requires future scheduled time", SchedulePublishRequiresFutureScheduledTime),
    ("archive hides public state and restore creates new draft version", ArchiveHidesPublicStateAndRestoreCreatesNewDraftVersion),
    ("workflow actions are queryable in audit log", WorkflowActionsAreQueryableInAuditLog),
    ("review actions create approval records", ReviewActionsCreateApprovalRecords)
};

var failed = 0;

foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

return failed == 0 ? 0 : 1;

static async Task ContentCanMoveThroughReviewApprovalAndPublish()
{
    var service = CreateService(out _);
    var actorId = Guid.NewGuid();
    var item = PublishingWorkflowItem.CreateDraft(
        "ContentItem",
        "content-1",
        actorId,
        new DateTimeOffset(2026, 7, 8, 9, 0, 0, TimeSpan.Zero));

    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(
        WorkflowAction.SubmitForReview,
        actorId,
        OccurredAt: new DateTimeOffset(2026, 7, 8, 9, 5, 0, TimeSpan.Zero)))).Item!;

    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(
        WorkflowAction.Approve,
        actorId,
        Comment: "Ready for public release.",
        OccurredAt: new DateTimeOffset(2026, 7, 8, 9, 10, 0, TimeSpan.Zero)))).Item!;

    var published = await service.TransitionAsync(item, new WorkflowTransitionRequest(
        WorkflowAction.Publish,
        actorId,
        OccurredAt: new DateTimeOffset(2026, 7, 8, 9, 15, 0, TimeSpan.Zero),
        Readiness: new WorkflowPublicationReadiness(true, true, true)));

    Assert.True(published.Succeeded, "Expected publish transition to succeed.");
    Assert.Equal(WorkflowState.Published, published.Item?.State);
    Assert.Equal(new DateTimeOffset(2026, 7, 8, 9, 15, 0, TimeSpan.Zero), published.Item?.PublishedAt);
}

static async Task RejectionsRequireReviewComments()
{
    var service = CreateService(out _);
    var actorId = Guid.NewGuid();
    var item = PublishingWorkflowItem.CreateDraft("ContentItem", "content-2", actorId);
    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(WorkflowAction.SubmitForReview, actorId))).Item!;

    var result = await service.TransitionAsync(item, new WorkflowTransitionRequest(WorkflowAction.Reject, actorId));

    Assert.False(result.Succeeded, "Expected reject transition to fail without comment.");
    Assert.Contains("Rejected content must include a review comment.", result.Errors);
}

static async Task PublishRequiresTitleSlugAndSummaryReadiness()
{
    var service = CreateService(out _);
    var actorId = Guid.NewGuid();
    var item = PublishingWorkflowItem.CreateDraft("ContentItem", "content-3", actorId);
    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(WorkflowAction.SubmitForReview, actorId))).Item!;
    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(WorkflowAction.Approve, actorId))).Item!;

    var result = await service.TransitionAsync(item, new WorkflowTransitionRequest(
        WorkflowAction.Publish,
        actorId,
        Readiness: new WorkflowPublicationReadiness(HasTitle: true, HasSlug: false, HasSummary: false)));

    Assert.False(result.Succeeded, "Expected publish transition to fail when readiness is incomplete.");
    Assert.Contains("Published content must have a slug.", result.Errors);
    Assert.Contains("Published content must have a summary.", result.Errors);
}

static async Task SchedulePublishRequiresFutureScheduledTime()
{
    var service = CreateService(out _);
    var actorId = Guid.NewGuid();
    var now = new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero);
    var item = PublishingWorkflowItem.CreateDraft("ContentItem", "content-3b", actorId, now);
    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(WorkflowAction.SubmitForReview, actorId, OccurredAt: now.AddMinutes(1)))).Item!;
    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(WorkflowAction.Approve, actorId, OccurredAt: now.AddMinutes(2)))).Item!;

    var missingTime = await service.TransitionAsync(item, new WorkflowTransitionRequest(
        WorkflowAction.SchedulePublish,
        actorId,
        OccurredAt: now.AddMinutes(3),
        Readiness: new WorkflowPublicationReadiness(true, true, true)));
    var scheduled = await service.TransitionAsync(item, new WorkflowTransitionRequest(
        WorkflowAction.SchedulePublish,
        actorId,
        ScheduledAt: now.AddDays(1),
        OccurredAt: now.AddMinutes(3),
        Readiness: new WorkflowPublicationReadiness(true, true, true)));

    Assert.False(missingTime.Succeeded, "Expected schedule publish to fail without scheduled time.");
    Assert.Contains("Scheduled publish must include a scheduled time.", missingTime.Errors);
    Assert.True(scheduled.Succeeded, "Expected schedule publish to succeed with future time.");
    Assert.Equal(now.AddDays(1), scheduled.Item?.ScheduledAt);
}

static async Task ArchiveHidesPublicStateAndRestoreCreatesNewDraftVersion()
{
    var service = CreateService(out _);
    var actorId = Guid.NewGuid();
    var item = PublishingWorkflowItem.CreateDraft("ContentItem", "content-4", actorId);
    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(WorkflowAction.SubmitForReview, actorId))).Item!;
    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(WorkflowAction.Approve, actorId))).Item!;
    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(
        WorkflowAction.Publish,
        actorId,
        Readiness: new WorkflowPublicationReadiness(true, true, true)))).Item!;

    var archived = (await service.TransitionAsync(item, new WorkflowTransitionRequest(WorkflowAction.Archive, actorId))).Item!;
    var restored = (await service.TransitionAsync(archived, new WorkflowTransitionRequest(WorkflowAction.Restore, actorId))).Item!;

    Assert.Equal(WorkflowState.Archived, archived.State);
    Assert.Equal(WorkflowState.Draft, restored.State);
    Assert.Equal(2, restored.VersionNumber);
    Assert.Equal(null, restored.ArchivedAt);
}

static async Task WorkflowActionsAreQueryableInAuditLog()
{
    var service = CreateService(out var auditStore);
    var actorId = Guid.NewGuid();
    var item = PublishingWorkflowItem.CreateDraft("ContentItem", "content-5", actorId);

    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(WorkflowAction.SubmitForReview, actorId))).Item!;
    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(WorkflowAction.Approve, actorId))).Item!;

    var logs = await auditStore.FindByTargetAsync("ContentItem", "content-5");
    var actions = logs.Select(log => log.Action).ToArray();

    Assert.Equal(2, logs.Count);
    Assert.Contains("WorkflowSubmitForReview", actions);
    Assert.Contains("WorkflowApprove", actions);
}

static async Task ReviewActionsCreateApprovalRecords()
{
    var service = CreateServiceWithApprovalStore(out _, out var approvalStore);
    var actorId = Guid.NewGuid();
    var item = PublishingWorkflowItem.CreateDraft("ContentItem", "content-6", actorId);

    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(WorkflowAction.SubmitForReview, actorId))).Item!;
    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(
        WorkflowAction.Reject,
        actorId,
        Comment: "Summary needs to be clearer."))).Item!;
    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(WorkflowAction.SubmitForReview, actorId))).Item!;
    item = (await service.TransitionAsync(item, new WorkflowTransitionRequest(
        WorkflowAction.Approve,
        actorId,
        Comment: "Ready."))).Item!;

    var records = await approvalStore.FindByTargetAsync("ContentItem", "content-6");
    var actions = records.Select(record => record.Action).ToArray();

    Assert.Equal(2, records.Count);
    Assert.Contains(WorkflowAction.Reject, actions);
    Assert.Contains(WorkflowAction.Approve, actions);
}

static PublishingWorkflowService CreateService(out InMemoryAuditLogStore auditStore)
{
    auditStore = new InMemoryAuditLogStore();
    return new PublishingWorkflowService(new AuditRecorder(auditStore));
}

static PublishingWorkflowService CreateServiceWithApprovalStore(
    out InMemoryAuditLogStore auditStore,
    out InMemoryApprovalRecordStore approvalStore)
{
    auditStore = new InMemoryAuditLogStore();
    approvalStore = new InMemoryApprovalRecordStore();
    return new PublishingWorkflowService(new AuditRecorder(auditStore), approvalStore);
}

internal static class Assert
{
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void False(bool condition, string message)
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Contains<T>(T expected, IEnumerable<T> actual)
    {
        if (!actual.Contains(expected))
        {
            throw new InvalidOperationException($"Expected output to contain '{expected}'.");
        }
    }
}
