// MMO 서버로 확장할 때 플레이어의 게임 상태를 담기 위한 세션 모델입니다.
public sealed class PlayerSession
{
    private readonly Dictionary<string, int> inventory = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<EquipmentSlot, string> equipment = new();
    // 아직 로그인하지 않은 연결에 붙일 임시 플레이어 ID입니다.
    public const long AnonymousPlayerId = 0;

    // 플레이어를 구분하는 ID입니다.
    public long PlayerId { get; private set; }

    // 플레이어가 로그인했는지 여부입니다.
    public bool IsAuthenticated => PlayerId != AnonymousPlayerId;

    // 플레이어의 현재 월드 위치입니다.
    public WorldPosition Position { get; private set; }

    // 플레이어가 속한 현재 게임 맵 ID입니다.
    public int MapId { get; private set; }

    // 서버가 승인한 마지막 일반 이동 시각입니다.
    public DateTimeOffset? LastMoveAt { get; private set; }

    // 서버가 승인한 마지막 일반 이동 순서 번호입니다.
    public long LastMoveSequence { get; private set; }

    // 플레이어가 월드에 스폰되었는지 여부입니다.
    public bool IsSpawned { get; private set; }

    public int MaxHealth => WorldRules.PlayerMaxHealth;

    public int CurrentHealth { get; private set; }

    public bool IsAlive => CurrentHealth > 0;

    public DateTimeOffset? LastAttackAt { get; private set; }

    public long Experience { get; private set; }
    public long SaveVersion { get; private set; }

    public int Level => checked((int)Math.Min(int.MaxValue, Experience / WorldRules.ExperiencePerLevel + 1));

    public long ExperienceToNextLevel => checked((long)Level * WorldRules.ExperiencePerLevel - Experience);

    public int AttackPower => WorldRules.PlayerAttackDamage + equipment.Values
        .Select(ItemCatalog.Find)
        .Where(item => item is not null)
        .Sum(item => item!.AttackBonus);

    public int Defense => equipment.Values
        .Select(ItemCatalog.Find)
        .Where(item => item is not null)
        .Sum(item => item!.DefenseBonus);

    // 세션을 기본 익명 상태로 시작합니다.
    public PlayerSession()
    {
        // 처음에는 로그인되지 않은 상태입니다.
        PlayerId = AnonymousPlayerId;
        // 처음 위치는 월드 원점입니다.
        Position = WorldPosition.Origin;
        // 처음 맵은 학습용 기본 맵입니다.
        MapId = WorldRules.DefaultMapId;
        // 새 세션에는 아직 승인된 이동 기록이 없습니다.
        LastMoveAt = null;
        // 첫 이동은 0보다 큰 순서 번호부터 시작합니다.
        LastMoveSequence = 0;
        // 처음에는 아직 월드에 스폰되지 않았습니다.
        IsSpawned = false;
        CurrentHealth = MaxHealth;
        LastAttackAt = null;
        Experience = 0;
        SaveVersion = 0;
    }

    // 로그인 성공 후 플레이어 ID를 세션에 연결합니다.
    public void Authenticate(long playerId)
    {
        // 이미 인증된 세션의 플레이어 ID가 바뀌지 않도록 막습니다.
        if (IsAuthenticated)
        {
            // 세션의 정체성은 인증 후 다시 설정할 수 없습니다.
            throw new InvalidOperationException("Player session is already authenticated.");
        }

        // MMO 서버에서는 보통 1 이상의 ID를 실제 플레이어 ID로 사용합니다.
        if (playerId <= AnonymousPlayerId)
        {
            // 잘못된 ID를 세션에 넣지 않도록 막습니다.
            throw new ArgumentOutOfRangeException(nameof(playerId), "Player id must be positive.");
        }

        // 세션에 플레이어 ID를 저장합니다.
        PlayerId = playerId;
    }

    // 플레이어의 현재 위치를 변경합니다.
    public void MoveTo(WorldPosition position)
    {
        // 새 위치를 세션에 저장합니다.
        Position = position;
    }

    // 새 이동 순서 번호가 마지막 승인 번호보다 큰지 확인합니다.
    public bool CanAcceptMoveSequence(long sequence)
    {
        // 중복되거나 과거에 처리한 번호는 허용하지 않습니다.
        return sequence > LastMoveSequence;
    }

    // 서버가 승인한 일반 이동의 위치, 시각, 순서 번호를 함께 저장합니다.
    public void MoveTo(WorldPosition position, DateTimeOffset movedAt, long sequence)
    {
        // 오래되거나 중복된 이동 번호가 내부 코드에서 저장되지 않도록 막습니다.
        if (!CanAcceptMoveSequence(sequence))
        {
            // 마지막 승인 번호 이하의 이동은 세션 상태를 변경할 수 없습니다.
            throw new ArgumentOutOfRangeException(nameof(sequence), "Move sequence must increase.");
        }

        // 새 위치를 세션에 저장합니다.
        Position = position;
        // 이동 빈도 검증에 사용할 서버 시각을 저장합니다.
        LastMoveAt = movedAt;
        // 중복 이동 검증에 사용할 순서 번호를 저장합니다.
        LastMoveSequence = sequence;
    }

    // 스폰 전에 플레이어가 입장할 게임 맵을 변경합니다.
    public void ChangeMap(int mapId)
    {
        // 맵 ID는 양수만 허용합니다.
        if (mapId <= 0)
        {
            // 잘못된 맵 ID가 세션에 저장되지 않도록 막습니다.
            throw new ArgumentOutOfRangeException(nameof(mapId), "Map id must be positive.");
        }

        // 스폰된 플레이어는 현재 단계에서 맵을 직접 바꿀 수 없습니다.
        if (IsSpawned)
        {
            // 맵 이동 중 기존 AOI에 엔티티가 남는 상태를 막습니다.
            throw new InvalidOperationException("Spawned player session cannot change maps.");
        }

        // 세션에 새 게임 맵 ID를 저장합니다.
        MapId = mapId;
        // 새 맵에서는 이전 맵의 일반 이동 쿨다운을 이어받지 않습니다.
        LastMoveAt = null;
        // 새 맵에서는 이동 순서 번호를 처음부터 다시 시작합니다.
        LastMoveSequence = 0;
    }

    // 플레이어를 현재 위치에 스폰된 상태로 바꿉니다.
    public void Spawn()
    {
        if (!IsAlive)
        {
            CurrentHealth = MaxHealth;
        }
        // 월드에 등장한 상태로 표시합니다.
        IsSpawned = true;
    }

    public PlayerDamageResult ApplyDamage(int damage)
    {
        if (damage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(damage), "Damage must be positive.");
        }

        if (!IsSpawned || !IsAlive)
        {
            return new PlayerDamageResult(0, CurrentHealth, !IsAlive);
        }

        int reducedDamage = Math.Max(1, damage - Defense);
        int appliedDamage = Math.Min(reducedDamage, CurrentHealth);
        CurrentHealth -= appliedDamage;
        bool isFatal = CurrentHealth == 0;
        if (isFatal)
        {
            IsSpawned = false;
        }

        return new PlayerDamageResult(appliedDamage, CurrentHealth, isFatal);
    }

    public bool IsAttackCooldownElapsed(DateTimeOffset serverTime) =>
        LastAttackAt is null || serverTime - LastAttackAt.Value >= WorldRules.PlayerAttackInterval;

    public void RecordAttack(DateTimeOffset serverTime)
    {
        LastAttackAt = serverTime;
    }

    public ExperienceGainResult AddExperience(int amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Experience amount must be positive.");
        }

        int previousLevel = Level;
        Experience = checked(Experience + amount);
        return new ExperienceGainResult(amount, Experience, previousLevel, Level);
    }

    public void AddItem(ItemDrop drop)
    {
        ArgumentNullException.ThrowIfNull(drop);
        if (string.IsNullOrWhiteSpace(drop.ItemId) || drop.Quantity <= 0)
        {
            throw new ArgumentException("Item drop must have an id and positive quantity.", nameof(drop));
        }

        inventory.TryGetValue(drop.ItemId, out int currentQuantity);
        inventory[drop.ItemId] = checked(currentQuantity + drop.Quantity);
    }

    public ItemStack[] SnapshotInventory() => inventory
        .Select(item => new ItemStack(item.Key, item.Value))
        .OrderBy(item => item.ItemId, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public CharacterSaveData CreateSaveData()
    {
        if (!IsAuthenticated)
        {
            throw new InvalidOperationException("Anonymous sessions cannot be saved.");
        }

        return new CharacterSaveData(
            PlayerId,
            MapId,
            Position,
            CurrentHealth,
            Experience,
            SnapshotInventory(),
            equipment.ToDictionary(entry => entry.Key.ToString(), entry => entry.Value),
            SaveVersion);
    }

    public void Restore(CharacterSaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!IsAuthenticated || data.PlayerId != PlayerId || IsSpawned)
        {
            throw new InvalidOperationException("Character data can only restore its matching unspawned session.");
        }

        if (data.MapId <= 0 || !WorldRules.IsInsideWorld(data.Position) || data.Experience < 0)
        {
            throw new InvalidDataException("Character save contains invalid world state.");
        }

        MapId = data.MapId;
        Position = data.Position;
        CurrentHealth = Math.Clamp(data.CurrentHealth, 0, MaxHealth);
        Experience = data.Experience;
        SaveVersion = data.Version;
        LastMoveAt = null;
        LastMoveSequence = 0;
        LastAttackAt = null;
        inventory.Clear();
        equipment.Clear();

        foreach (ItemStack item in data.Inventory)
        {
            AddItem(new ItemDrop(item.ItemId, item.Quantity));
        }

        foreach ((string slotName, string itemId) in data.Equipment)
        {
            if (!Enum.TryParse(slotName, true, out EquipmentSlot slot) ||
                ItemCatalog.Find(itemId)?.EquipmentSlot != slot)
            {
                throw new InvalidDataException($"Invalid equipped item: {slotName}={itemId}");
            }

            equipment[slot] = itemId;
        }
    }

    public void MarkSaved(long version)
    {
        if (version <= SaveVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Save version must increase.");
        }

        SaveVersion = version;
    }

    public IReadOnlyDictionary<EquipmentSlot, string> SnapshotEquipment() =>
        new Dictionary<EquipmentSlot, string>(equipment);

    public ItemActionResult Equip(string itemId)
    {
        ItemDefinition? item = ItemCatalog.Find(itemId);
        if (item?.Category != ItemCategory.Equipment || item.EquipmentSlot is null)
        {
            return new(false, $"Item cannot be equipped: {itemId}");
        }

        if (!RemoveItem(item.ItemId, 1))
        {
            return new(false, $"Item not found in inventory: {itemId}");
        }

        EquipmentSlot slot = item.EquipmentSlot.Value;
        if (equipment.Remove(slot, out string? previousItem))
        {
            AddItem(new ItemDrop(previousItem, 1));
        }

        equipment[slot] = item.ItemId;
        return new(true, $"Equipped {item.ItemId} in {slot}.");
    }

    public ItemActionResult Unequip(EquipmentSlot slot)
    {
        if (!equipment.Remove(slot, out string? itemId))
        {
            return new(false, $"Nothing is equipped in {slot}.");
        }

        AddItem(new ItemDrop(itemId, 1));
        return new(true, $"Unequipped {itemId} from {slot}.");
    }

    public ItemActionResult UseItem(string itemId)
    {
        ItemDefinition? item = ItemCatalog.Find(itemId);
        if (item?.Category != ItemCategory.Consumable)
        {
            return new(false, $"Item cannot be used: {itemId}");
        }

        if (CurrentHealth >= MaxHealth)
        {
            return new(false, "Health is already full.");
        }

        if (!RemoveItem(item.ItemId, 1))
        {
            return new(false, $"Item not found in inventory: {itemId}");
        }

        int healed = Math.Min(item.HealAmount, MaxHealth - CurrentHealth);
        CurrentHealth += healed;
        return new(true, $"Used {item.ItemId}. Restored {healed} HP. Health: {CurrentHealth}/{MaxHealth}");
    }

    private bool RemoveItem(string itemId, int quantity)
    {
        if (!inventory.TryGetValue(itemId, out int current) || current < quantity)
        {
            return false;
        }

        if (current == quantity)
        {
            inventory.Remove(itemId);
        }
        else
        {
            inventory[itemId] = current - quantity;
        }

        return true;
    }

    // 플레이어를 현재 월드에서 사라진 상태로 바꿉니다.
    public void Despawn()
    {
        // 월드에 등장하지 않은 상태로 표시합니다.
        IsSpawned = false;
    }

    // 인증된 플레이어 정보를 세션에서 제거합니다.
    public void Logout()
    {
        // 월드에 스폰된 플레이어는 먼저 despawn해야 합니다.
        if (IsSpawned)
        {
            // 월드 엔티티가 남은 채 인증 정보만 사라지는 상태를 막습니다.
            throw new InvalidOperationException("Spawned player session cannot logout.");
        }

        // 세션을 익명 플레이어 ID로 되돌립니다.
        PlayerId = AnonymousPlayerId;
        // 다음 로그인에 이전 위치가 이어지지 않도록 원점으로 초기화합니다.
        Position = WorldPosition.Origin;
        // 다음 로그인에 이전 맵이 이어지지 않도록 기본 맵으로 초기화합니다.
        MapId = WorldRules.DefaultMapId;
        // 다음 로그인에 이전 이동 시각이 이어지지 않도록 초기화합니다.
        LastMoveAt = null;
        // 다음 로그인에 이전 이동 순서가 이어지지 않도록 초기화합니다.
        LastMoveSequence = 0;
        CurrentHealth = MaxHealth;
        LastAttackAt = null;
        Experience = 0;
        SaveVersion = 0;
        inventory.Clear();
        equipment.Clear();
    }
}
