// Owns the monster entities currently spawned in the world.
public sealed class MonsterRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<long, MonsterEntity> monsters = new();

    public int Count
    {
        get
        {
            lock (gate)
            {
                return monsters.Count;
            }
        }
    }

    public bool TrySpawn(MonsterEntity monster)
    {
        ArgumentNullException.ThrowIfNull(monster);

        lock (gate)
        {
            if (monsters.ContainsKey(monster.MonsterId))
            {
                return false;
            }

            monsters.Add(monster.MonsterId, monster);
            return true;
        }
    }

    public MonsterEntity[] SnapshotMap(int mapId)
    {
        lock (gate)
        {
            return monsters.Values
                .Where(monster => monster.IsSpawned && monster.MapId == mapId)
                .OrderBy(monster => monster.MonsterId)
                .ToArray();
        }
    }

    public MonsterEntity[] Snapshot()
    {
        lock (gate)
        {
            return monsters.Values
                .Where(monster => monster.IsSpawned)
                .OrderBy(monster => monster.MonsterId)
                .ToArray();
        }
    }

    public MonsterEntity? Find(long monsterId)
    {
        lock (gate)
        {
            return monsters.GetValueOrDefault(monsterId);
        }
    }

    public MonsterDamageResult? ApplyDamage(long monsterId, int damage, DateTimeOffset serverTime)
    {
        if (damage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(damage), "Damage must be positive.");
        }

        lock (gate)
        {
            if (!monsters.TryGetValue(monsterId, out MonsterEntity? current) || !current.IsSpawned)
            {
                return null;
            }

            int appliedDamage = Math.Min(damage, current.CurrentHealth);
            int remainingHealth = current.CurrentHealth - appliedDamage;
            bool isFatal = remainingHealth == 0;
            MonsterEntity updated = current with
            {
                CurrentHealth = remainingHealth,
                IsSpawned = !isFatal,
                AiState = isFatal ? MonsterAiState.Idle : current.AiState,
                AggroTargetPlayerId = isFatal ? null : current.AggroTargetPlayerId,
                RespawnAt = isFatal ? serverTime + WorldRules.MonsterRespawnDelay : null
            };
            monsters[monsterId] = updated;
            return new MonsterDamageResult(updated, appliedDamage, remainingHealth, isFatal);
        }
    }

    public MonsterEntity[] RespawnReady(DateTimeOffset serverTime)
    {
        lock (gate)
        {
            MonsterEntity[] ready = monsters.Values
                .Where(monster =>
                    !monster.IsSpawned &&
                    monster.RespawnAt is not null &&
                    monster.RespawnAt.Value <= serverTime)
                .ToArray();

            var respawned = new List<MonsterEntity>(ready.Length);
            foreach (MonsterEntity monster in ready)
            {
                MonsterEntity updated = monster with
                {
                    Position = monster.SpawnPosition,
                    IsSpawned = true,
                    CurrentHealth = monster.MaxHealth,
                    AiState = MonsterAiState.Idle,
                    AggroTargetPlayerId = null,
                    LastMovedAt = null,
                    LastAttackedAt = null,
                    RespawnAt = null
                };
                monsters[monster.MonsterId] = updated;
                respawned.Add(updated);
            }

            return respawned
                .OrderBy(monster => monster.MonsterId)
                .ToArray();
        }
    }

    public bool TryMove(
        long monsterId,
        WorldPosition expectedPosition,
        WorldPosition nextPosition,
        DateTimeOffset movedAt,
        out MonsterEntity? movedMonster)
    {
        lock (gate)
        {
            if (!monsters.TryGetValue(monsterId, out MonsterEntity? current) ||
                !current.IsSpawned ||
                current.Position != expectedPosition ||
                !WorldRules.IsInsideWorld(nextPosition))
            {
                movedMonster = null;
                return false;
            }

            movedMonster = current with
            {
                Position = nextPosition,
                LastMovedAt = movedAt
            };
            monsters[monsterId] = movedMonster;
            return true;
        }
    }

    public bool TryUpdate(MonsterEntity expected, MonsterEntity updated)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(updated);

        lock (gate)
        {
            if (!monsters.TryGetValue(expected.MonsterId, out MonsterEntity? current) ||
                current != expected ||
                updated.MonsterId != expected.MonsterId ||
                !WorldRules.IsInsideWorld(updated.Position))
            {
                return false;
            }

            monsters[expected.MonsterId] = updated;
            return true;
        }
    }
}
