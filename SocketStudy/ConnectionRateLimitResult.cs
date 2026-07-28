public sealed record ConnectionRateLimitResult(
    ConnectionRateLimitStatus Status,
    TimeSpan RetryAfter)
{
    public bool Allowed => Status == ConnectionRateLimitStatus.Allowed;
}
