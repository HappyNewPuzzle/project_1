public sealed class GroundLootRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<long, GroundLoot> loot = new();
    private long nextLootId;

    public GroundLoot Spawn(ItemDrop item, int mapId, WorldPosition position, long ownerPlayerId, DateTimeOffset time)
    {
        lock (gate)
        {
            var entry = new GroundLoot(
                ++nextLootId,
                item,
                mapId,
                position,
                ownerPlayerId,
                time + WorldRules.LootExclusiveDuration,
                time + WorldRules.LootLifetime);
            loot.Add(entry.LootId, entry);
            return entry;
        }
    }

    public GroundLoot[] SnapshotNearby(PlayerSession player, DateTimeOffset time)
    {
        lock (gate)
        {
            RemoveExpiredCore(time);
            return loot.Values
                .Where(entry =>
                    entry.MapId == player.MapId &&
                    WorldRules.GetDistance(entry.Position, player.Position) <= WorldRules.ViewDistance)
                .OrderBy(entry => entry.LootId)
                .ToArray();
        }
    }

    public LootPickupResult TryPickup(long lootId, PlayerSession player, DateTimeOffset time)
    {
        lock (gate)
        {
            RemoveExpiredCore(time);
            if (!loot.TryGetValue(lootId, out GroundLoot? entry))
            {
                return new(false, $"Loot not found: {lootId}");
            }

            if (!player.IsSpawned || entry.MapId != player.MapId ||
                WorldRules.GetDistance(entry.Position, player.Position) > WorldRules.LootPickupRange)
            {
                return new(false, "Loot is out of pickup range.");
            }

            if (time < entry.ExclusiveUntil && player.PlayerId != entry.OwnerPlayerId)
            {
                return new(false, $"Loot belongs to player {entry.OwnerPlayerId} for now.");
            }

            loot.Remove(lootId);
            player.AddItem(entry.Item);
            return new(true, $"Picked up {entry.Item.ItemId} x{entry.Item.Quantity}.", entry.Item);
        }
    }

    public int RemoveExpired(DateTimeOffset time)
    {
        lock (gate)
        {
            return RemoveExpiredCore(time);
        }
    }

    private int RemoveExpiredCore(DateTimeOffset time)
    {
        long[] expired = loot.Values.Where(entry => entry.ExpiresAt <= time).Select(entry => entry.LootId).ToArray();
        foreach (long lootId in expired)
        {
            loot.Remove(lootId);
        }

        return expired.Length;
    }
}
