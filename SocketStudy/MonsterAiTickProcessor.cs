// Runs the idle, chasing, and returning states for server-owned monsters.
public sealed class MonsterAiTickProcessor
{
    private readonly MonsterRegistry monsters;
    private readonly Func<PlayerEntity[]> getPlayers;
    private readonly Func<long, int, PlayerDamageResult?> applyPlayerDamage;

    public MonsterAiTickProcessor(
        MonsterRegistry monsters,
        Func<PlayerEntity[]> getPlayers,
        Func<long, int, PlayerDamageResult?> applyPlayerDamage)
    {
        this.monsters = monsters;
        this.getPlayers = getPlayers;
        this.applyPlayerDamage = applyPlayerDamage;
    }

    public MonsterAiTickResult Process(DateTimeOffset serverTime)
    {
        PlayerEntity[] players = getPlayers()
            .Where(player => player.IsSpawned)
            .ToArray();
        var movements = new List<MonsterMovement>();
        var attacks = new List<MonsterAttack>();

        foreach (MonsterEntity monster in monsters.Snapshot())
        {
            MonsterEntity updated = monster;
            PlayerEntity? target = null;
            WorldPosition? destination = null;
            bool attacked = false;

            switch (monster.AiState)
            {
                case MonsterAiState.Idle:
                    target = FindNearestDetectedPlayer(monster, players);
                    if (target is not null)
                    {
                        updated = updated with
                        {
                            AiState = MonsterAiState.Chasing,
                            AggroTargetPlayerId = target.PlayerId
                        };
                        destination = target.Position;
                    }
                    break;

                case MonsterAiState.Chasing:
                    target = players.FirstOrDefault(player =>
                        player.PlayerId == monster.AggroTargetPlayerId &&
                        player.MapId == monster.MapId);

                    if (target is null ||
                        WorldRules.GetDistance(monster.SpawnPosition, target.Position) > WorldRules.MonsterLeashDistance)
                    {
                        updated = updated with
                        {
                            AiState = MonsterAiState.Returning,
                            AggroTargetPlayerId = null
                        };
                        destination = monster.SpawnPosition;
                    }
                    else
                    {
                        destination = target.Position;
                    }
                    break;

                case MonsterAiState.Returning:
                    if (monster.Position == monster.SpawnPosition)
                    {
                        updated = updated with { AiState = MonsterAiState.Idle };
                    }
                    else
                    {
                        destination = monster.SpawnPosition;
                    }
                    break;
            }

            bool isInAttackRange = updated.AiState == MonsterAiState.Chasing &&
                target is not null &&
                WorldRules.GetDistance(monster.Position, target.Position) <= WorldRules.MonsterAttackRange;
            bool attackCooldownElapsed = monster.LastAttackedAt is null ||
                serverTime - monster.LastAttackedAt.Value >= WorldRules.MonsterAttackInterval;
            if (isInAttackRange && attackCooldownElapsed)
            {
                PlayerDamageResult? damageResult = applyPlayerDamage(
                    target!.PlayerId,
                    WorldRules.MonsterAttackDamage);
                if (damageResult is not null && damageResult.DamageApplied > 0)
                {
                    attacked = true;
                    updated = updated with { LastAttackedAt = serverTime };
                    attacks.Add(new MonsterAttack(
                        monster.MonsterId,
                        target.PlayerId,
                        damageResult.DamageApplied,
                        damageResult.RemainingHealth,
                        damageResult.IsFatal));
                }
            }

            bool canMove = !attacked && destination is not null &&
                destination.Value != monster.Position &&
                (monster.LastMovedAt is null ||
                    serverTime - monster.LastMovedAt.Value >= WorldRules.MonsterMoveInterval);

            if (canMove)
            {
                WorldPosition nextPosition = GetNextPosition(monster.Position, destination!.Value);
                updated = updated with
                {
                    Position = nextPosition,
                    LastMovedAt = serverTime,
                    AiState = updated.AiState == MonsterAiState.Returning && nextPosition == monster.SpawnPosition
                        ? MonsterAiState.Idle
                        : updated.AiState
                };
            }

            if (updated == monster || !monsters.TryUpdate(monster, updated))
            {
                continue;
            }

            if (updated.Position != monster.Position)
            {
                movements.Add(new MonsterMovement(
                    monster.MonsterId,
                    updated.AggroTargetPlayerId,
                    monster.Position,
                    updated.Position));
            }
        }

        return new MonsterAiTickResult(movements, attacks);
    }

    private static PlayerEntity? FindNearestDetectedPlayer(
        MonsterEntity monster,
        IEnumerable<PlayerEntity> players)
    {
        return players
            .Where(player =>
                player.MapId == monster.MapId &&
                WorldRules.GetDistance(monster.Position, player.Position) <= WorldRules.MonsterDetectionDistance)
            .OrderBy(player => WorldRules.GetDistance(monster.Position, player.Position))
            .ThenBy(player => player.PlayerId)
            .FirstOrDefault();
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
