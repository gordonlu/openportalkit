using OpenPortalKit.Modules.Identity.Authentication;
using OpenPortalKit.AdminHost.Security;

var hasher = new PasswordCredentialHasher();
var encoded = hasher.Hash("correct horse battery staple", 100_000);

Assert(encoded.StartsWith("pbkdf2-sha512$100000$", StringComparison.Ordinal), "Credential format is not versioned.");
Assert(hasher.Verify("correct horse battery staple", encoded), "Valid password was rejected.");
Assert(!hasher.Verify("wrong password", encoded), "Invalid password was accepted.");
Assert(!hasher.Verify("password", "pbkdf2-sha512$100000$invalid$invalid"), "Malformed credential was accepted.");
Assert(!hasher.Verify("password", "pbkdf2-sha512$99999$YWJj$YWJj"), "Weak credential parameters were accepted.");
Assert(hasher.Hash("correct horse battery staple", 100_000) != encoded, "Password hashes must use unique salts.");

Console.WriteLine("PASS password credentials use salted PBKDF2-SHA512");
Console.WriteLine("PASS password verification rejects invalid and malformed credentials");

var guard = new AdminLoginAttemptGuard(new AdminAuthenticationOptions
{
    MaxFailedAttempts = 2,
    LockoutMinutes = 10
});
var now = DateTimeOffset.UtcNow;
guard.RecordFailure("127.0.0.1", now);
Assert(!guard.IsLocked("127.0.0.1", now, out _), "Source locked before threshold.");
guard.RecordFailure("127.0.0.1", now);
Assert(guard.IsLocked("127.0.0.1", now, out var retryAfter) && retryAfter == TimeSpan.FromMinutes(10),
    "Source was not locked at threshold.");
guard.RecordSuccess("127.0.0.1");
Assert(!guard.IsLocked("127.0.0.1", now, out _), "Successful login did not clear failures.");
Console.WriteLine("PASS login attempt guard locks and resets sources");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
