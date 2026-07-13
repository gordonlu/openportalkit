using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace OpenPortalKit.Infrastructure.Production;

public static class CspNonceAccessor
{
    private const string ItemKey = "OpenPortalKit.CspNonce";

    public static string Create(HttpContext context)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        context.Items[ItemKey] = nonce;
        return nonce;
    }

    public static string Get(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string nonce
            ? nonce
            : throw new InvalidOperationException("CSP nonce middleware has not run for this request.");
}
