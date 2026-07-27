// The result of applying player damage to a monster.
public sealed record MonsterDamageResult(
    MonsterEntity Monster,
    int DamageApplied,
    int RemainingHealth,
    bool IsFatal);
