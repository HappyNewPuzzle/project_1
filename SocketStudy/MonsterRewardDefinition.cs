// Server configuration for experience and item rewards by monster type.
public sealed record MonsterRewardDefinition(
    int Experience,
    IReadOnlyList<DropTableEntry> Drops);
