namespace OpenPortalKit.Modules.Seo.Revalidation;

public sealed class InMemoryPublicOutputRevalidationStore : IPublicOutputRevalidationStore
{
    private readonly List<PublicOutputRevalidationResult> _results = new();
    private readonly object _syncRoot = new();

    public Task AddAsync(PublicOutputRevalidationResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_syncRoot)
        {
            if (_results.Any(existing => string.Equals(
                existing.SourceIdempotencyKey,
                result.SourceIdempotencyKey,
                StringComparison.Ordinal)))
            {
                return Task.CompletedTask;
            }

            _results.Add(result);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PublicOutputRevalidationResult>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyList<PublicOutputRevalidationResult>>(
                _results
                    .OrderBy(result => result.StartedAt)
                    .ToArray());
        }
    }

    public Task<PublicOutputRevalidationResult?> FindByIdempotencyKeyAsync(
        string sourceIdempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIdempotencyKey);

        lock (_syncRoot)
        {
            return Task.FromResult(_results.FirstOrDefault(result => string.Equals(
                result.SourceIdempotencyKey,
                sourceIdempotencyKey,
                StringComparison.Ordinal)));
        }
    }
}
