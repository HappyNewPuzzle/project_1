using System.Net;

public sealed class ConnectionRateLimiter
{
    private sealed class Bucket
    {
        public double Tokens { get; set; }
        public DateTimeOffset LastRefillAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
        public int ConsecutiveViolations { get; set; }
        public DateTimeOffset? BlockedUntil { get; set; }
    }

    private readonly object gate = new();
    private readonly int capacity;
    private readonly double refillTokensPerSecond;
    private readonly int blockViolationThreshold;
    private readonly TimeSpan blockDuration;
    private readonly TimeSpan idleRetention;
    private readonly Func<DateTimeOffset> getCurrentTime;
    private readonly Dictionary<IPAddress, Bucket> buckets = new();
    private DateTimeOffset lastCleanupAt;

    public ConnectionRateLimiter(
        int capacity,
        double refillTokensPerSecond,
        int blockViolationThreshold,
        TimeSpan blockDuration,
        TimeSpan idleRetention,
        Func<DateTimeOffset>? getCurrentTime = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(refillTokensPerSecond);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockViolationThreshold);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(blockDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleRetention, blockDuration);

        this.capacity = capacity;
        this.refillTokensPerSecond = refillTokensPerSecond;
        this.blockViolationThreshold = blockViolationThreshold;
        this.blockDuration = blockDuration;
        this.idleRetention = idleRetention;
        this.getCurrentTime = getCurrentTime ?? (() => DateTimeOffset.UtcNow);
        lastCleanupAt = this.getCurrentTime();
    }

    public ConnectionRateLimitResult Check(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        IPAddress normalizedAddress = address.IsIPv4MappedToIPv6
            ? address.MapToIPv4()
            : address;
        DateTimeOffset now = getCurrentTime();

        lock (gate)
        {
            CleanupIdleBuckets(now);
            if (!buckets.TryGetValue(normalizedAddress, out Bucket? bucket))
            {
                bucket = new Bucket
                {
                    Tokens = capacity,
                    LastRefillAt = now,
                    LastSeenAt = now
                };
                buckets.Add(normalizedAddress, bucket);
            }

            bucket.LastSeenAt = now;
            if (bucket.BlockedUntil is DateTimeOffset blockedUntil && blockedUntil > now)
            {
                return new(
                    ConnectionRateLimitStatus.TemporarilyBlocked,
                    blockedUntil - now);
            }

            if (bucket.BlockedUntil is not null)
            {
                bucket.BlockedUntil = null;
                bucket.ConsecutiveViolations = 0;
            }

            Refill(bucket, now);
            if (bucket.Tokens >= 1)
            {
                bucket.Tokens -= 1;
                bucket.ConsecutiveViolations = 0;
                return new(ConnectionRateLimitStatus.Allowed, TimeSpan.Zero);
            }

            bucket.ConsecutiveViolations++;
            if (bucket.ConsecutiveViolations >= blockViolationThreshold)
            {
                bucket.BlockedUntil = now + blockDuration;
                return new(ConnectionRateLimitStatus.TemporarilyBlocked, blockDuration);
            }

            double missingTokens = 1 - bucket.Tokens;
            TimeSpan retryAfter = TimeSpan.FromSeconds(
                missingTokens / refillTokensPerSecond);
            return new(ConnectionRateLimitStatus.RateLimited, retryAfter);
        }
    }

    public int TrackedAddressCount
    {
        get
        {
            lock (gate)
            {
                return buckets.Count;
            }
        }
    }

    private void Refill(Bucket bucket, DateTimeOffset now)
    {
        double elapsedSeconds = Math.Max(0, (now - bucket.LastRefillAt).TotalSeconds);
        bucket.Tokens = Math.Min(
            capacity,
            bucket.Tokens + elapsedSeconds * refillTokensPerSecond);
        bucket.LastRefillAt = now;
    }

    private void CleanupIdleBuckets(DateTimeOffset now)
    {
        if (now - lastCleanupAt < idleRetention)
        {
            return;
        }

        IPAddress[] expiredAddresses = buckets
            .Where(pair =>
                (pair.Value.BlockedUntil is null || pair.Value.BlockedUntil <= now) &&
                now - pair.Value.LastSeenAt >= idleRetention)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (IPAddress address in expiredAddresses)
        {
            buckets.Remove(address);
        }

        lastCleanupAt = now;
    }
}
