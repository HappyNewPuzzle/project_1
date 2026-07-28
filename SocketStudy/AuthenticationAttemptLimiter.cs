using System.Net;

public sealed class AuthenticationAttemptLimiter
{
    private sealed class FailureState
    {
        public int FailureCount { get; set; }
        public DateTimeOffset NextAllowedAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
    }

    private readonly object gate = new();
    private readonly TimeSpan baseDelay;
    private readonly TimeSpan maxDelay;
    private readonly TimeSpan idleRetention;
    private readonly Func<DateTimeOffset> getCurrentTime;
    private readonly Dictionary<IPAddress, FailureState> failuresByIp = new();
    private readonly Dictionary<string, FailureState> failuresByAccount =
        new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset lastCleanupAt;

    public AuthenticationAttemptLimiter(
        TimeSpan baseDelay,
        TimeSpan maxDelay,
        TimeSpan idleRetention,
        Func<DateTimeOffset>? getCurrentTime = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(baseDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDelay, baseDelay);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleRetention, maxDelay);

        this.baseDelay = baseDelay;
        this.maxDelay = maxDelay;
        this.idleRetention = idleRetention;
        this.getCurrentTime = getCurrentTime ?? (() => DateTimeOffset.UtcNow);
        lastCleanupAt = this.getCurrentTime();
    }

    public AuthenticationAttemptResult Check(IPAddress address, string? accountKey)
    {
        ArgumentNullException.ThrowIfNull(address);
        IPAddress normalizedAddress = Normalize(address);
        DateTimeOffset now = getCurrentTime();

        lock (gate)
        {
            CleanupIdleEntries(now);
            TimeSpan ipRetryAfter = GetRetryAfter(failuresByIp, normalizedAddress, now);
            TimeSpan accountRetryAfter = string.IsNullOrWhiteSpace(accountKey)
                ? TimeSpan.Zero
                : GetRetryAfter(failuresByAccount, accountKey, now);
            TimeSpan retryAfter = ipRetryAfter >= accountRetryAfter
                ? ipRetryAfter
                : accountRetryAfter;
            return new(retryAfter <= TimeSpan.Zero, retryAfter);
        }
    }

    public TimeSpan RecordFailure(IPAddress address, string? accountKey)
    {
        ArgumentNullException.ThrowIfNull(address);
        IPAddress normalizedAddress = Normalize(address);
        DateTimeOffset now = getCurrentTime();

        lock (gate)
        {
            TimeSpan ipDelay = IncreaseFailure(failuresByIp, normalizedAddress, now);
            TimeSpan accountDelay = string.IsNullOrWhiteSpace(accountKey)
                ? TimeSpan.Zero
                : IncreaseFailure(failuresByAccount, accountKey, now);
            return ipDelay >= accountDelay ? ipDelay : accountDelay;
        }
    }

    public void RecordSuccess(string accountKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);
        lock (gate)
        {
            failuresByAccount.Remove(accountKey);
        }
    }

    private TimeSpan IncreaseFailure<TKey>(
        Dictionary<TKey, FailureState> failures,
        TKey key,
        DateTimeOffset now)
        where TKey : notnull
    {
        if (!failures.TryGetValue(key, out FailureState? state))
        {
            state = new FailureState();
            failures.Add(key, state);
        }

        state.FailureCount++;
        state.LastSeenAt = now;
        double multiplier = Math.Pow(2, Math.Min(state.FailureCount - 1, 30));
        TimeSpan delay = TimeSpan.FromMilliseconds(
            Math.Min(maxDelay.TotalMilliseconds, baseDelay.TotalMilliseconds * multiplier));
        state.NextAllowedAt = now + delay;
        return delay;
    }

    private static TimeSpan GetRetryAfter<TKey>(
        Dictionary<TKey, FailureState> failures,
        TKey key,
        DateTimeOffset now)
        where TKey : notnull
    {
        if (!failures.TryGetValue(key, out FailureState? state))
        {
            return TimeSpan.Zero;
        }

        state.LastSeenAt = now;
        return state.NextAllowedAt > now
            ? state.NextAllowedAt - now
            : TimeSpan.Zero;
    }

    private void CleanupIdleEntries(DateTimeOffset now)
    {
        if (now - lastCleanupAt < idleRetention)
        {
            return;
        }

        RemoveIdleEntries(failuresByIp, now);
        RemoveIdleEntries(failuresByAccount, now);
        lastCleanupAt = now;
    }

    private void RemoveIdleEntries<TKey>(
        Dictionary<TKey, FailureState> failures,
        DateTimeOffset now)
        where TKey : notnull
    {
        TKey[] expiredKeys = failures
            .Where(pair => now - pair.Value.LastSeenAt >= idleRetention)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (TKey key in expiredKeys)
        {
            failures.Remove(key);
        }
    }

    private static IPAddress Normalize(IPAddress address)
    {
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }
}
