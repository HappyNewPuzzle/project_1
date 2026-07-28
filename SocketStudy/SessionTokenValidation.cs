public sealed record SessionTokenValidation(
    bool IsValid,
    long PlayerId,
    DateTimeOffset ExpiresAt)
{
    public static SessionTokenValidation Invalid { get; } =
        new(false, PlayerSession.AnonymousPlayerId, DateTimeOffset.MinValue);
}
