// Validates player attacks and advances monster combat lifecycle on a world tick.
public sealed class CombatTickProcessor
{
    private readonly PlayerAttackRequestQueue attackRequests;
    private readonly MonsterRegistry monsters;
    private readonly IRandomSource random;
    private readonly GroundLootRegistry groundLoot;

    public CombatTickProcessor(
        PlayerAttackRequestQueue attackRequests,
        MonsterRegistry monsters,
        GroundLootRegistry groundLoot,
        IRandomSource? random = null)
    {
        this.attackRequests = attackRequests;
        this.monsters = monsters;
        this.random = random ?? SystemRandomSource.Shared;
        this.groundLoot = groundLoot;
    }

    public CombatTickResult Process(DateTimeOffset serverTime)
    {
        MonsterEntity[] respawnedMonsters = monsters.RespawnReady(serverTime);
        groundLoot.RemoveExpired(serverTime);
        var attackResults = new List<PlayerAttackResult>();

        while (attackRequests.TryDequeue(out QueuedPlayerAttackRequest? queuedRequest))
        {
            if (queuedRequest is null)
            {
                continue;
            }

            PlayerAttackResult result = ProcessAttack(queuedRequest.Request, serverTime);
            queuedRequest.TryComplete(result);
            attackResults.Add(result);
        }

        return new CombatTickResult(attackResults, respawnedMonsters);
    }

    private PlayerAttackResult ProcessAttack(PlayerAttackRequest request, DateTimeOffset serverTime)
    {
        PlayerSession attacker = request.Attacker;
        if (!attacker.IsSpawned || !attacker.IsAlive)
        {
            return PlayerAttackResult.Rejected("You must be alive and spawned before attacking.", request.MonsterId);
        }

        MonsterEntity? monster = monsters.Find(request.MonsterId);
        if (monster is null || !monster.IsSpawned)
        {
            return PlayerAttackResult.Rejected($"Monster is not spawned: {request.MonsterId}", request.MonsterId);
        }

        if (monster.MapId != attacker.MapId)
        {
            return PlayerAttackResult.Rejected("Monster is in a different map.", request.MonsterId);
        }

        if (WorldRules.GetDistance(attacker.Position, monster.Position) > WorldRules.PlayerAttackRange)
        {
            return PlayerAttackResult.Rejected(
                $"Monster must be within attack range {WorldRules.PlayerAttackRange}.",
                request.MonsterId);
        }

        if (!attacker.IsAttackCooldownElapsed(serverTime))
        {
            return PlayerAttackResult.Rejected("Player attack is on cooldown.", request.MonsterId);
        }

        MonsterDamageResult? damageResult = monsters.ApplyDamage(
            request.MonsterId,
            attacker.AttackPower,
            attacker.PlayerId,
            serverTime);
        if (damageResult is null)
        {
            return PlayerAttackResult.Rejected($"Monster is not spawned: {request.MonsterId}", request.MonsterId);
        }

        attacker.RecordAttack(serverTime);
        int experienceAwarded = 0;
        int currentLevel = attacker.Level;
        bool leveledUp = false;
        IReadOnlyList<ItemDrop> itemDrops = [];
        if (damageResult.IsFatal)
        {
            MonsterRewardDefinition reward = MonsterRewardCatalog.Get(damageResult.Monster.MonsterType);
            ExperienceGainResult experienceResult = attacker.AddExperience(reward.Experience);
            itemDrops = MonsterRewardCatalog.RollDrops(reward, random);
            foreach (ItemDrop drop in itemDrops)
            {
                groundLoot.Spawn(
                    drop,
                    damageResult.Monster.MapId,
                    damageResult.Monster.Position,
                    attacker.PlayerId,
                    serverTime);
            }
            experienceAwarded = experienceResult.ExperienceAwarded;
            currentLevel = experienceResult.CurrentLevel;
            leveledUp = experienceResult.LeveledUp;
        }
        return PlayerAttackResult.Accepted(
            damageResult.Monster,
            damageResult.DamageApplied,
            damageResult.RemainingHealth,
            damageResult.IsFatal,
            experienceAwarded,
            currentLevel,
            leveledUp,
            itemDrops);
    }
}
