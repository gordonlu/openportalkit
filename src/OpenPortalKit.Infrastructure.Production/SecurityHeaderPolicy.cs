namespace OpenPortalKit.Infrastructure.Production;

public static class SecurityHeaderPolicy
{
    public static IReadOnlyDictionary<string, string> Headers { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Security-Policy"] = "default-src 'self'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'; object-src 'none'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'nonce-{nonce}'",
            ["Cross-Origin-Opener-Policy"] = "same-origin",
            ["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()",
            ["Referrer-Policy"] = "strict-origin-when-cross-origin",
            ["X-Content-Type-Options"] = "nosniff",
            ["X-Frame-Options"] = "DENY"
        };
}
