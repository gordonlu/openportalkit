using OpenPortalKit.Modules.Seo.PublicResources;

namespace OpenPortalKit.Modules.Seo.Redirects;

public sealed class InMemoryRedirectRuleStore : IRedirectRuleStore
{
    private readonly List<RedirectRule> _rules = new();
    private readonly object _syncRoot = new();

    public Task AddAsync(RedirectRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var normalizedSource = CanonicalUrlBuilder.NormalizePath(rule.SourcePath);
        var normalizedRule = rule with { SourcePath = normalizedSource };

        lock (_syncRoot)
        {
            _rules.RemoveAll(existing => string.Equals(
                existing.SourcePath,
                normalizedSource,
                StringComparison.OrdinalIgnoreCase));
            _rules.Add(normalizedRule);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RedirectRule>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyList<RedirectRule>>(
                _rules
                    .OrderBy(rule => rule.SourcePath, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }
    }

    public Task<RedirectRule?> FindBySourcePathAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var normalizedSource = CanonicalUrlBuilder.NormalizePath(sourcePath);

        lock (_syncRoot)
        {
            return Task.FromResult(_rules.FirstOrDefault(rule => string.Equals(
                rule.SourcePath,
                normalizedSource,
                StringComparison.OrdinalIgnoreCase)));
        }
    }
}
