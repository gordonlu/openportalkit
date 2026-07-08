using OpenPortalKit.Kernel.Audit;
using OpenPortalKit.Kernel.Events;

namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed record ContentPublishResult(
    bool Succeeded,
    ContentItem? Item,
    ContentVersion? Version,
    AuditLog? AuditLog,
    OutboxMessage? OutboxMessage,
    IReadOnlyList<string> Errors)
{
    public static ContentPublishResult Failed(IReadOnlyList<string> errors)
    {
        return new ContentPublishResult(
            Succeeded: false,
            Item: null,
            Version: null,
            AuditLog: null,
            OutboxMessage: null,
            Errors: errors);
    }
}
