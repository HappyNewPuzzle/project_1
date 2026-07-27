// Resolves trusted kill rewards from the monster type stored by the server.
public static class MonsterRewardCatalog
{
    private static readonly IReadOnlyDictionary<string, MonsterRewardDefinition> Rewards =
        new Dictionary<string, MonsterRewardDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["slime"] = new(25, new ItemDrop("slime-gel", 1)),
            ["skeleton"] = new(30, new ItemDrop("bone", 1)),
            ["orc"] = new(40, new ItemDrop("orc-tusk", 1))
        };

    private static readonly MonsterRewardDefinition DefaultReward =
        new(20, new ItemDrop("monster-token", 1));

    public static MonsterRewardDefinition Get(string monsterType) =>
        Rewards.GetValueOrDefault(monsterType, DefaultReward);
}
