namespace OpenPortalKit.Kernel.Events;

public sealed record RetryPolicy
{
    public static RetryPolicy Default { get; } = new(MaxAttemptCount: 3, LeaseDuration: TimeSpan.FromMinutes(2));

    public RetryPolicy(int MaxAttemptCount, TimeSpan? LeaseDuration = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxAttemptCount);
        var leaseDuration = LeaseDuration ?? TimeSpan.FromMinutes(2);
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(LeaseDuration));
        }

        this.MaxAttemptCount = MaxAttemptCount;
        this.LeaseDuration = leaseDuration;
    }

    public int MaxAttemptCount { get; }

    public TimeSpan LeaseDuration { get; }

    public bool CanAttempt(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return message.AttemptCount < MaxAttemptCount;
    }
}
