namespace OpenPortalKit.Modules.Seo.Revalidation;

public sealed class RecordingPublicOutputRevalidationExecutor : IPublicOutputRevalidationExecutor
{
    private readonly IPublicOutputRevalidationStore _store;
    private readonly Func<DateTimeOffset> _clock;

    public RecordingPublicOutputRevalidationExecutor(
        IPublicOutputRevalidationStore store,
        Func<DateTimeOffset>? clock = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<PublicOutputRevalidationResult> ExecuteAsync(
        PublicOutputRevalidationPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var startedAt = _clock();
        var regeneratedOutputs = GetRegeneratedOutputs(plan);
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
        return result;
    }

    private static IReadOnlyList<string> GetRegeneratedOutputs(PublicOutputRevalidationPlan plan)
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
            outputs.Add("markdown-snapshot");
            outputs.Add("json-snapshot");
        }

        return outputs;
    }
}
