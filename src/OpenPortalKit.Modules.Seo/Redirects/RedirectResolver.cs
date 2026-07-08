using OpenPortalKit.Modules.Seo.PublicResources;

namespace OpenPortalKit.Modules.Seo.Redirects;

public sealed class RedirectResolver
{
    private readonly IRedirectRuleStore _store;

    public RedirectResolver(IRedirectRuleStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<RedirectResolution?> ResolveAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var normalizedSource = CanonicalUrlBuilder.NormalizePath(sourcePath);
        var rule = await _store.FindBySourcePathAsync(normalizedSource, cancellationToken);

        if (rule is null || !rule.IsEnabled)
        {
            return null;
        }

        var normalizedTarget = NormalizeTarget(rule.Target);

        if (IsLoop(normalizedSource, normalizedTarget))
        {
            return null;
        }

        return new RedirectResolution(
            rule.SourcePath,
            normalizedTarget,
            rule.Status == RedirectStatus.Permanent ? 301 : 302,
            IsHttpUrl(normalizedTarget));
    }

    private static string NormalizeTarget(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        if (Uri.TryCreate(target.Trim(), UriKind.Absolute, out var absoluteUri) &&
            IsHttpUrl(absoluteUri))
        {
            return absoluteUri.ToString();
        }

        return CanonicalUrlBuilder.NormalizePath(target);
    }

    private static bool IsLoop(string normalizedSource, string normalizedTarget)
    {
        if (Uri.TryCreate(normalizedTarget, UriKind.Absolute, out var absoluteTarget) &&
            IsHttpUrl(absoluteTarget))
        {
            normalizedTarget = CanonicalUrlBuilder.NormalizePath(absoluteTarget.AbsolutePath);
        }

        return string.Equals(normalizedSource, normalizedTarget, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && IsHttpUrl(uri);
    }

    private static bool IsHttpUrl(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
