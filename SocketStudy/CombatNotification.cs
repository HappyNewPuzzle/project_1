// A combat message centered on a player for AOI delivery.
public sealed record CombatNotification(
    long CenterPlayerId,
    string Message);
