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

    // 플레이어가 월드에 스폰되었는지 여부입니다.
    public bool IsSpawned { get; private set; }

    // 세션을 기본 익명 상태로 시작합니다.
    public PlayerSession()
    {
        // 처음에는 로그인되지 않은 상태입니다.
        PlayerId = AnonymousPlayerId;
        // 처음 위치는 월드 원점입니다.
        Position = WorldPosition.Origin;
        // 처음에는 아직 월드에 스폰되지 않았습니다.
        IsSpawned = false;
    }

    // 로그인 성공 후 플레이어 ID를 세션에 연결합니다.
    public void Authenticate(long playerId)
    {
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
}
