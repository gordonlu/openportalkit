using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenPortalKit.AdminHost.Security;
using Microsoft.AspNetCore.RateLimiting;
using OpenPortalKit.Kernel.Audit;
using System.Text.Json;

namespace OpenPortalKit.AdminHost.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("admin-login")]
public sealed class LoginModel : PageModel
{
    private readonly AdminCredentialValidator _validator;
    private readonly AdminAuthenticationOptions _options;
    private readonly AdminLoginAttemptGuard _attemptGuard;
    private readonly AuditRecorder _auditRecorder;

    public LoginModel(
        AdminCredentialValidator validator,
        AdminAuthenticationOptions options,
        AdminLoginAttemptGuard attemptGuard,
        AuditRecorder auditRecorder)
    {
        _validator = validator;
        _options = options;
        _attemptGuard = attemptGuard;
        _auditRecorder = auditRecorder;
    }

    [BindProperty] public LoginInput Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (_options.Mode == AdminAuthenticationMode.OpenIdConnect)
        {
            return Challenge(new AuthenticationProperties { RedirectUri = SafeReturnUrl(ReturnUrl) }, "OpenIdConnect");
        }
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(SafeReturnUrl(ReturnUrl));
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var source = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (_attemptGuard.IsLocked(source, DateTimeOffset.UtcNow, out _))
        {
            await RecordAttemptAsync("admin.login.locked", source, cancellationToken);
            ModelState.AddModelError(string.Empty, "The supplied administrator credentials are invalid.");
            return Page();
        }
        if (!ModelState.IsValid || !_validator.Validate(Input.UserName, Input.Password))
        {
            _attemptGuard.RecordFailure(source, DateTimeOffset.UtcNow);
            await RecordAttemptAsync("admin.login.failed", source, cancellationToken);
            ModelState.AddModelError(string.Empty, "The supplied administrator credentials are invalid.");
            return Page();
        }

        _attemptGuard.RecordSuccess(source);
        await RecordAttemptAsync("admin.login.succeeded", source, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _options.UserName),
            new Claim(ClaimTypes.Name, _options.DisplayName),
            new Claim(ClaimTypes.Role, "Administrator"),
            new Claim("opk:session_version", _options.SessionVersion),
            new Claim("opk:session_started", now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture))
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, IssuedUtc = now, ExpiresUtc = now.AddHours(_options.AbsoluteTimeoutHours) });
        return LocalRedirect(SafeReturnUrl(ReturnUrl));
    }

    private string SafeReturnUrl(string? returnUrl) => Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";

    private Task RecordAttemptAsync(string action, string source, CancellationToken cancellationToken) =>
        _auditRecorder.RecordAsync(new AuditRecordRequest(
            null,
            action,
            "AdminAccount",
            _options.UserName,
            MetadataJson: JsonSerializer.Serialize(new
            {
                SourceAddress = source,
                TraceId = HttpContext.TraceIdentifier
            })), cancellationToken);

    public sealed class LoginInput
    {
        [Required, Display(Name = "Username")]
        public string UserName { get; set; } = string.Empty;
        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
