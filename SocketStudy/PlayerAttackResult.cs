// The result returned to the client after a player attack is processed.
public sealed record PlayerAttackResult(
    bool IsAccepted,
    string? RejectionReason,
    long MonsterId,
    string? MonsterType,
    int Damage,
    int RemainingHealth,
    bool IsFatal)
{
    public static PlayerAttackResult Rejected(string reason, long monsterId) =>
        new(false, reason, monsterId, null, 0, 0, false);

    public static PlayerAttackResult Accepted(
        MonsterEntity monster,
        int damage,
        int remainingHealth,
        bool isFatal) =>
        new(true, null, monster.MonsterId, monster.MonsterType, damage, remainingHealth, isFatal);
}
