// Chooses nearby player targets and moves monsters using server-owned state.
public sealed class MonsterAiTickProcessor
{
    private readonly MonsterRegistry monsters;
    private readonly Func<PlayerEntity[]> getPlayers;

    public MonsterAiTickProcessor(MonsterRegistry monsters, Func<PlayerEntity[]> getPlayers)
    {
        this.monsters = monsters;
        this.getPlayers = getPlayers;
    }

    public MonsterAiTickResult Process(DateTimeOffset serverTime)
    {
        PlayerEntity[] players = getPlayers()
            .Where(player => player.IsSpawned)
            .ToArray();
        var movements = new List<MonsterMovement>();

        foreach (MonsterEntity monster in monsters.Snapshot())
        {
            if (monster.LastMovedAt is not null &&
                serverTime - monster.LastMovedAt.Value < WorldRules.MonsterMoveInterval)
            {
                continue;
            }

            PlayerEntity? target = players
                .Where(player => player.MapId == monster.MapId)
                .OrderBy(player => WorldRules.GetDistance(monster.Position, player.Position))
                .ThenBy(player => player.PlayerId)
                .FirstOrDefault();

            if (target is null || target.Position == monster.Position)
            {
                continue;
            }

            WorldPosition nextPosition = GetNextPosition(monster.Position, target.Position);
            if (monsters.TryMove(
                monster.MonsterId,
                monster.Position,
                nextPosition,
                serverTime,
                out _))
            {
                movements.Add(new MonsterMovement(
                    monster.MonsterId,
                    target.PlayerId,
                    monster.Position,
                    nextPosition));
            }
        }

        return new MonsterAiTickResult(movements);
    }

    private static WorldPosition GetNextPosition(WorldPosition current, WorldPosition target)
    {
        if (current.X != target.X)
        {
            return current with { X = current.X + Math.Sign(target.X - current.X) };
        }

        return current with { Y = current.Y + Math.Sign(target.Y - current.Y) };
    }
}
