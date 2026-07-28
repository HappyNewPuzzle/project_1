public sealed record AuthenticationAttemptResult(
    bool Allowed,
    TimeSpan RetryAfter);
