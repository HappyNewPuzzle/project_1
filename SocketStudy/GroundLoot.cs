public sealed record GroundLoot(
    long LootId,
    ItemDrop Item,
    int MapId,
    WorldPosition Position,
    long OwnerPlayerId,
    DateTimeOffset ExclusiveUntil,
    DateTimeOffset ExpiresAt);
