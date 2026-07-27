// Player attacks and monster respawns completed during one world tick.
public sealed record CombatTickResult(
    IReadOnlyList<PlayerAttackResult> Attacks,
    IReadOnlyList<MonsterEntity> RespawnedMonsters);
