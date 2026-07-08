using System.Text.Json;
using OpenPortalKit.Kernel.Audit;

namespace OpenPortalKit.Modules.Workflow.Publishing;

public sealed class PublishingWorkflowService
{
    private readonly AuditRecorder _auditRecorder;
    private readonly IApprovalRecordStore? _approvalRecordStore;

    public PublishingWorkflowService(
        AuditRecorder auditRecorder,
        IApprovalRecordStore? approvalRecordStore = null)
    {
        _auditRecorder = auditRecorder ?? throw new ArgumentNullException(nameof(auditRecorder));
        _approvalRecordStore = approvalRecordStore;
    }

    public async Task<WorkflowTransitionResult> TransitionAsync(
        PublishingWorkflowItem item,
        WorkflowTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(request);

        var occurredAt = request.OccurredAt ?? DateTimeOffset.UtcNow;
        var validationErrors = Validate(item, request, occurredAt);

        if (validationErrors.Count > 0)
        {
            return WorkflowTransitionResult.Failure(validationErrors.ToArray());
        }

        var updated = Apply(item, request, occurredAt);

        await RecordAuditAsync(item, updated, request, occurredAt, cancellationToken);
        await RecordApprovalAsync(item, updated, request, occurredAt, cancellationToken);
        return WorkflowTransitionResult.Success(updated);
    }

    private static IReadOnlyList<string> Validate(
        PublishingWorkflowItem item,
        WorkflowTransitionRequest request,
        DateTimeOffset occurredAt)
    {
        var errors = new List<string>();

        if (!AllowedActions(item.State).Contains(request.Action))
        {
            errors.Add($"Action '{request.Action}' is not allowed from state '{item.State}'.");
        }

        if (request.Action == WorkflowAction.Reject &&
            string.IsNullOrWhiteSpace(request.Comment))
        {
            errors.Add("Rejected content must include a review comment.");
        }

        if (request.Action == WorkflowAction.RequestChanges &&
            string.IsNullOrWhiteSpace(request.Comment))
        {
            errors.Add("Requested changes must include a review comment.");
        }

        if (request.Action == WorkflowAction.SchedulePublish)
        {
            if (request.ScheduledAt is null)
            {
                errors.Add("Scheduled publish must include a scheduled time.");
            }
            else if (request.ScheduledAt <= occurredAt)
            {
                errors.Add("Scheduled publish time must be in the future.");
            }
        }

        if (request.Action is WorkflowAction.Publish or WorkflowAction.SchedulePublish)
        {
            errors.AddRange(ValidateReadiness(request.Readiness));
        }

        return errors;
    }

    private static IEnumerable<string> ValidateReadiness(WorkflowPublicationReadiness? readiness)
    {
        if (readiness is null)
        {
            yield return "Publishing requires title, slug, and summary readiness.";
            yield break;
        }

        if (!readiness.HasTitle)
        {
            yield return "Published content must have a title.";
        }

        if (!readiness.HasSlug)
        {
            yield return "Published content must have a slug.";
        }

        if (!readiness.HasSummary)
        {
            yield return "Published content must have a summary.";
        }
    }

    private static IReadOnlySet<WorkflowAction> AllowedActions(WorkflowState state)
    {
        return state switch
        {
            WorkflowState.Draft => new HashSet<WorkflowAction>
            {
                WorkflowAction.SubmitForReview,
                WorkflowAction.Archive
            },
            WorkflowState.Review => new HashSet<WorkflowAction>
            {
                WorkflowAction.Approve,
                WorkflowAction.Reject,
                WorkflowAction.RequestChanges,
                WorkflowAction.Archive
            },
            WorkflowState.Approved => new HashSet<WorkflowAction>
            {
                WorkflowAction.Publish,
                WorkflowAction.SchedulePublish,
                WorkflowAction.RequestChanges,
                WorkflowAction.Archive
            },
            WorkflowState.Published => new HashSet<WorkflowAction>
            {
                WorkflowAction.Unpublish,
                WorkflowAction.Archive
            },
            WorkflowState.Rejected => new HashSet<WorkflowAction>
            {
                WorkflowAction.SubmitForReview,
                WorkflowAction.Archive
            },
            WorkflowState.Archived => new HashSet<WorkflowAction>
            {
                WorkflowAction.Restore
            },
            _ => throw new InvalidOperationException($"Unknown workflow state '{state}'.")
        };
    }

    private static PublishingWorkflowItem Apply(
        PublishingWorkflowItem item,
        WorkflowTransitionRequest request,
        DateTimeOffset occurredAt)
    {
        return request.Action switch
        {
            WorkflowAction.SubmitForReview => item with
            {
                State = WorkflowState.Review,
                UpdatedBy = request.ActorId,
                UpdatedAt = occurredAt,
                ReviewComment = null
            },
            WorkflowAction.Approve => item with
            {
                State = WorkflowState.Approved,
                UpdatedBy = request.ActorId,
                UpdatedAt = occurredAt,
                ApprovedAt = occurredAt,
                ReviewComment = request.Comment
            },
            WorkflowAction.Reject => item with
            {
                State = WorkflowState.Rejected,
                UpdatedBy = request.ActorId,
                UpdatedAt = occurredAt,
                ReviewComment = request.Comment
            },
            WorkflowAction.RequestChanges => item with
            {
                State = WorkflowState.Draft,
                UpdatedBy = request.ActorId,
                UpdatedAt = occurredAt,
                VersionNumber = item.VersionNumber + 1,
                ReviewComment = request.Comment
            },
            WorkflowAction.Publish => item with
            {
                State = WorkflowState.Published,
                UpdatedBy = request.ActorId,
                UpdatedAt = occurredAt,
                PublishedAt = occurredAt,
                ScheduledAt = null
            },
            WorkflowAction.SchedulePublish => item with
            {
                UpdatedBy = request.ActorId,
                UpdatedAt = occurredAt,
                ScheduledAt = request.ScheduledAt
            },
            WorkflowAction.Unpublish => item with
            {
                State = WorkflowState.Approved,
                UpdatedBy = request.ActorId,
                UpdatedAt = occurredAt,
                PublishedAt = null
            },
            WorkflowAction.Archive => item with
            {
                State = WorkflowState.Archived,
                UpdatedBy = request.ActorId,
                UpdatedAt = occurredAt,
                ArchivedAt = occurredAt
            },
            WorkflowAction.Restore => item with
            {
                State = WorkflowState.Draft,
                UpdatedBy = request.ActorId,
                UpdatedAt = occurredAt,
                VersionNumber = item.VersionNumber + 1,
                ArchivedAt = null
            },
            _ => throw new InvalidOperationException($"Unknown workflow action '{request.Action}'.")
        };
    }

    private Task RecordAuditAsync(
        PublishingWorkflowItem before,
        PublishingWorkflowItem after,
        WorkflowTransitionRequest request,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            Action = request.Action.ToString(),
            From = before.State.ToString(),
            To = after.State.ToString(),
            after.VersionNumber,
            occurredAt,
            request.Comment,
            request.ScheduledAt
        });

        return _auditRecorder.RecordAsync(new AuditRecordRequest(
            request.ActorId,
            $"Workflow{request.Action}",
            before.TargetType,
            before.TargetId,
            $"Workflow action '{request.Action}' moved '{before.TargetType}' from '{before.State}' to '{after.State}'.",
            metadata), cancellationToken);
    }

    private Task RecordApprovalAsync(
        PublishingWorkflowItem before,
        PublishingWorkflowItem after,
        WorkflowTransitionRequest request,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        if (_approvalRecordStore is null || !IsReviewAction(request.Action))
        {
            return Task.CompletedTask;
        }

        return _approvalRecordStore.AddAsync(new ApprovalRecord(
            Guid.NewGuid(),
            before.Id,
            before.TargetType,
            before.TargetId,
            request.ActorId,
            request.Action,
            before.State,
            after.State,
            request.Comment,
            occurredAt), cancellationToken);
    }

    private static bool IsReviewAction(WorkflowAction action)
    {
        return action is WorkflowAction.Approve or WorkflowAction.Reject or WorkflowAction.RequestChanges;
    }
}
