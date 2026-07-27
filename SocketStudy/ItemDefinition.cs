public sealed record ItemDefinition(
    string ItemId,
    ItemCategory Category,
    EquipmentSlot? EquipmentSlot = null,
    int AttackBonus = 0,
    int HealAmount = 0);
