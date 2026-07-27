public sealed record CharacterSaveData(
    long PlayerId,
    int MapId,
    WorldPosition Position,
    int CurrentHealth,
    long Experience,
    IReadOnlyList<ItemStack> Inventory,
    IReadOnlyDictionary<string, string> Equipment,
    long Version = 0);
