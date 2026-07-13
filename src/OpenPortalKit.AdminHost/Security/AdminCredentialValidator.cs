using OpenPortalKit.Modules.Identity.Authentication;

namespace OpenPortalKit.AdminHost.Security;

public sealed class AdminCredentialValidator
{
    private readonly AdminAuthenticationOptions _options;
    private readonly PasswordCredentialHasher _hasher;

    public AdminCredentialValidator(AdminAuthenticationOptions options, PasswordCredentialHasher hasher)
    {
        _options = options;
        _hasher = hasher;
    }

    public bool Validate(string userName, string password) =>
        _options.RequireAuthentication &&
        string.Equals(userName?.Trim(), _options.UserName, StringComparison.OrdinalIgnoreCase) &&
        _hasher.Verify(password, _options.PasswordHash);
}
