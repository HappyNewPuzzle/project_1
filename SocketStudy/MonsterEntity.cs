// A server-owned non-player entity that can exist in the game world.
public sealed record MonsterEntity(
    long MonsterId,
    string MonsterType,
    int MapId,
    WorldPosition Position,
    bool IsSpawned = true) : WorldEntity(MonsterId, MapId, Position, IsSpawned);
