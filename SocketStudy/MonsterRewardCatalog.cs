public static class MonsterRewardCatalog
{
    private static readonly IReadOnlyDictionary<string, MonsterRewardDefinition> Rewards =
        new Dictionary<string, MonsterRewardDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["slime"] = new(25,
            [
                new(new ItemDrop("slime-gel", 1), 1.0),
                new(new ItemDrop("health-potion", 1), 0.25)
            ]),
            ["skeleton"] = new(30,
            [
                new(new ItemDrop("bone", 1), 1.0),
                new(new ItemDrop("health-potion", 1), 0.10)
            ]),
            ["orc"] = new(40,
            [
                new(new ItemDrop("orc-tusk", 1), 1.0),
                new(new ItemDrop("iron-sword", 1), 0.20)
            ])
        };

    private static readonly MonsterRewardDefinition DefaultReward = new(20,
    [
        new(new ItemDrop("monster-token", 1), 1.0)
    ]);

    public static MonsterRewardDefinition Get(string monsterType) =>
        Rewards.GetValueOrDefault(monsterType, DefaultReward);

    public static ItemDrop[] RollDrops(MonsterRewardDefinition reward, IRandomSource random)
    {
        return reward.Drops
            .Where(entry => entry.Probability >= 1.0 || random.NextDouble() < entry.Probability)
            .Select(entry => entry.Drop)
            .ToArray();
    }
}
