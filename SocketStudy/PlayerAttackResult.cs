// The result returned to the client after a player attack is processed.
public sealed record PlayerAttackResult(
    bool IsAccepted,
    string? RejectionReason,
    long MonsterId,
    string? MonsterType,
    int Damage,
    int RemainingHealth,
    bool IsFatal,
    int ExperienceAwarded,
    int CurrentLevel,
    bool LeveledUp,
    IReadOnlyList<ItemDrop> ItemDrops)
{
    public static PlayerAttackResult Rejected(string reason, long monsterId) =>
        new(false, reason, monsterId, null, 0, 0, false, 0, 0, false, []);

    public static PlayerAttackResult Accepted(
        MonsterEntity monster,
        int damage,
        int remainingHealth,
        bool isFatal,
        int experienceAwarded,
        int currentLevel,
        bool leveledUp,
        IReadOnlyList<ItemDrop> itemDrops) =>
        new(
            true,
            null,
            monster.MonsterId,
            monster.MonsterType,
            damage,
            remainingHealth,
            isFatal,
            experienceAwarded,
            currentLevel,
            leveledUp,
            itemDrops);
}
