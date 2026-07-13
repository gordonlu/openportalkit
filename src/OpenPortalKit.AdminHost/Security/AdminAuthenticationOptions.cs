namespace OpenPortalKit.AdminHost.Security;

public sealed class AdminAuthenticationOptions
{
    public const string SectionName = "OpenPortalKit:AdminAuthentication";

    public bool RequireAuthentication { get; set; } = true;
    public AdminAuthenticationMode Mode { get; set; } = AdminAuthenticationMode.Local;
    public string UserName { get; set; } = "administrator";
    public string DisplayName { get; set; } = "Portal Administrator";
    public string PasswordHash { get; set; } = string.Empty;
    public int IdleTimeoutMinutes { get; set; } = 30;
    public int AbsoluteTimeoutHours { get; set; } = 12;
    public string SessionVersion { get; set; } = "1";
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = "/signin-oidc";
    public string RequiredRole { get; set; } = "OpenPortalKit.Administrator";
    public string DataProtectionKeyPath { get; set; } = string.Empty;
    public string KeyEncryptionCertificatePath { get; set; } = string.Empty;
    public string KeyEncryptionCertificatePassword { get; set; } = string.Empty;
}

public enum AdminAuthenticationMode
{
    Local,
    OpenIdConnect
}
