// A server-owned non-player entity that can exist in the game world.
public sealed record MonsterEntity(
    long MonsterId,
    string MonsterType,
    int MapId,
    WorldPosition Position,
    bool IsSpawned = true,
    DateTimeOffset? LastMovedAt = null) : WorldEntity(MonsterId, MapId, Position, IsSpawned)
{
    public WorldPosition SpawnPosition { get; init; } = Position;

    public MonsterAiState AiState { get; init; } = MonsterAiState.Idle;

    public long? AggroTargetPlayerId { get; init; }

    public int MaxHealth { get; init; } = WorldRules.MonsterMaxHealth;

    public int CurrentHealth { get; init; } = WorldRules.MonsterMaxHealth;

    public DateTimeOffset? LastAttackedAt { get; init; }
}
