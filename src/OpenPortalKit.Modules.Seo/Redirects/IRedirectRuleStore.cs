namespace OpenPortalKit.Modules.Seo.Redirects;

public interface IRedirectRuleStore
{
    Task AddAsync(RedirectRule rule, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RedirectRule>> ListAsync(CancellationToken cancellationToken = default);
    Task<RedirectRule?> FindBySourcePathAsync(string sourcePath, CancellationToken cancellationToken = default);
}
