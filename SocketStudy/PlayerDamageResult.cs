// The authoritative result of applying damage to one player session.
public sealed record PlayerDamageResult(
    int DamageApplied,
    int RemainingHealth,
    bool IsFatal);
