// MMO 서버로 확장할 때 플레이어의 게임 상태를 담기 위한 세션 모델입니다.
public sealed class PlayerSession
{
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
        // 월드에 등장한 상태로 표시합니다.
        IsSpawned = true;
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
    }
}
