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
}
