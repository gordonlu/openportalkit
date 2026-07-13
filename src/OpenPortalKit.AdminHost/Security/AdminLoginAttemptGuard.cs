using System.Collections.Concurrent;

namespace OpenPortalKit.AdminHost.Security;

public sealed class AdminLoginAttemptGuard
{
    private readonly ConcurrentDictionary<string, AttemptState> _attempts = new(StringComparer.Ordinal);
    private readonly AdminAuthenticationOptions _options;

    public AdminLoginAttemptGuard(AdminAuthenticationOptions options) => _options = options;

    public bool IsLocked(string source, DateTimeOffset now, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (!_attempts.TryGetValue(source, out var state) || state.LockedUntil is null)
        {
            return false;
        }
        retryAfter = state.LockedUntil.Value - now;
        if (retryAfter > TimeSpan.Zero)
        {
            return true;
        }
        _attempts.TryRemove(source, out _);
        retryAfter = TimeSpan.Zero;
        return false;
    }

    public void RecordFailure(string source, DateTimeOffset now)
    {
        _attempts.AddOrUpdate(source,
            _ => new AttemptState(1, null),
            (_, state) =>
            {
                var failures = state.Failures + 1;
                var lockedUntil = failures >= Math.Max(1, _options.MaxFailedAttempts)
                    ? now.AddMinutes(Math.Max(1, _options.LockoutMinutes))
                    : state.LockedUntil;
                return new AttemptState(failures, lockedUntil);
            });
    }

    public void RecordSuccess(string source) => _attempts.TryRemove(source, out _);

    private sealed record AttemptState(int Failures, DateTimeOffset? LockedUntil);
}
