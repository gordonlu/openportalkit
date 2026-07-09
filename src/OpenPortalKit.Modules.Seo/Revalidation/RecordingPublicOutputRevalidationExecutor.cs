using System.Text.Json;
using OpenPortalKit.Kernel.Audit;

namespace OpenPortalKit.Modules.Seo.Revalidation;

public sealed class RecordingPublicOutputRevalidationExecutor : IPublicOutputRevalidationExecutor
{
    private readonly IPublicOutputRevalidationStore _store;
    private readonly Func<DateTimeOffset> _clock;
    private readonly AuditRecorder? _auditRecorder;
    private readonly IPublicOutputRegenerator? _regenerator;

    public RecordingPublicOutputRevalidationExecutor(
        IPublicOutputRevalidationStore store,
        Func<DateTimeOffset>? clock = null,
        AuditRecorder? auditRecorder = null,
        IPublicOutputRegenerator? regenerator = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _auditRecorder = auditRecorder;
        _regenerator = regenerator;
    }

    public async Task<PublicOutputRevalidationResult> ExecuteAsync(
        PublicOutputRevalidationPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var existing = await _store.FindByIdempotencyKeyAsync(plan.SourceIdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var startedAt = _clock();
        var regeneratedOutputs = GetRegeneratedOutputs(plan);
        if (_regenerator is not null)
        {
            foreach (var output in await _regenerator.RegenerateAsync(plan, cancellationToken))
            {
                if (!regeneratedOutputs.Contains(output, StringComparer.OrdinalIgnoreCase))
                {
                    regeneratedOutputs.Add(output);
                }
            }
        }

        var result = new PublicOutputRevalidationResult(
            Guid.NewGuid(),
            plan.SourceEventName,
            plan.SourceIdempotencyKey,
            startedAt,
            _clock(),
            plan.InvalidateRouteCache ? plan.Routes : Array.Empty<string>(),
            regeneratedOutputs,
            Succeeded: true,
            Error: null);

        await _store.AddAsync(result, cancellationToken);
        await RecordAuditAsync(result, cancellationToken);
        return result;
    }

    private async Task RecordAuditAsync(
        PublicOutputRevalidationResult result,
        CancellationToken cancellationToken)
    {
        if (_auditRecorder is null)
        {
            return;
        }

        var metadata = JsonSerializer.Serialize(new
        {
            result.SourceEventName,
            result.InvalidatedRoutes,
            result.RegeneratedOutputs,
            result.Succeeded
        });

        await _auditRecorder.RecordAsync(new AuditRecordRequest(
            ActorId: null,
            Action: "public-output.revalidated",
            TargetType: "PublicOutput",
            TargetId: result.SourceIdempotencyKey,
            Summary: $"Regenerated {result.RegeneratedOutputs.Count} public outputs.",
            MetadataJson: metadata), cancellationToken);
    }

    private static List<string> GetRegeneratedOutputs(PublicOutputRevalidationPlan plan)
    {
        var outputs = new List<string>();

        if (plan.RegenerateSitemap)
        {
            outputs.Add("sitemap.xml");
        }

        if (plan.RegenerateRss)
        {
            outputs.Add("rss.xml");
        }

        if (plan.RegenerateSnapshots)
        {
            foreach (var route in plan.SnapshotRoutes ?? Array.Empty<string>())
            {
                outputs.Add(route);
            }
        }

        if (plan.RegenerateLlmsText)
        {
            outputs.Add("llms.txt");
            outputs.Add("llms-full.txt");
        }

        return outputs;
    }
}
