public static class ItemCatalog
{
    private static readonly IReadOnlyDictionary<string, ItemDefinition> Items =
        new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["slime-gel"] = new("slime-gel", ItemCategory.Material),
            ["bone"] = new("bone", ItemCategory.Material),
            ["orc-tusk"] = new("orc-tusk", ItemCategory.Material),
            ["monster-token"] = new("monster-token", ItemCategory.Material),
            ["health-potion"] = new("health-potion", ItemCategory.Consumable, HealAmount: 30),
            ["iron-sword"] = new("iron-sword", ItemCategory.Equipment, EquipmentSlot.Weapon, AttackBonus: 5),
            ["leather-armor"] = new("leather-armor", ItemCategory.Equipment, EquipmentSlot.Armor)
        };

    public static ItemDefinition? Find(string itemId) => Items.GetValueOrDefault(itemId);
}
