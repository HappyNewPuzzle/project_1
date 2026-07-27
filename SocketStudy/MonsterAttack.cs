// One server-authoritative monster attack produced during a world tick.
public sealed record MonsterAttack(
    long MonsterId,
    long TargetPlayerId,
    int Damage,
    int RemainingHealth,
    bool IsFatal);
