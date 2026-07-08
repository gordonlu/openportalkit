namespace OpenPortalKit.Kernel.Events;

public sealed record RetryPolicy
{
    public static RetryPolicy Default { get; } = new(MaxAttemptCount: 3);

    public RetryPolicy(int MaxAttemptCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxAttemptCount);
        this.MaxAttemptCount = MaxAttemptCount;
    }

    public int MaxAttemptCount { get; }

    public bool CanAttempt(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return message.AttemptCount < MaxAttemptCount;
    }
}
