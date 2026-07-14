using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace OpenPortalKit.AdminHost.Security;

public sealed class AdminActorContext
{
    private readonly AdminAuthenticationOptions _options;

    public AdminActorContext(AdminAuthenticationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Guid GetActorId(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal.FindFirstValue("sub") ??
            principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(subject) && !_options.RequireAuthentication)
            subject = "development:anonymous-administrator";
        if (string.IsNullOrWhiteSpace(subject))
            throw new InvalidOperationException("The authenticated administrator has no stable subject identifier.");
        if (Guid.TryParse(subject, out var actorId)) return actorId;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("OpenPortalKit.AdminActor:" + subject));
        Span<byte> guidBytes = bytes.AsSpan(0, 16);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes);
    }
}
