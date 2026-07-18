// 플레이어 세션을 월드 엔티티 관점으로 읽기 위한 모델입니다.
public sealed record PlayerEntity(
    // 게임 플레이어 ID입니다.
    long PlayerId,
    // 현재 표시 이름입니다.
    string Name,
    // 플레이어가 존재하는 맵 ID입니다.
    int MapId,
    // 플레이어의 현재 월드 위치입니다.
    WorldPosition Position,
    // 플레이어가 현재 월드에 등장해 있는지 나타냅니다.
    bool IsSpawned) : WorldEntity(PlayerId, MapId, Position, IsSpawned)
{
    // 접속 객체와 세션 상태를 플레이어 월드 엔티티 읽기 모델로 변환합니다.
    public static PlayerEntity FromConnection(ClientConnection connection)
    {
        // 네트워크 연결 객체에서 월드 복제에 필요한 플레이어 상태만 꺼냅니다.
        return new PlayerEntity(
            connection.Session.PlayerId,
            connection.Name,
            connection.Session.MapId,
            connection.Session.Position,
            connection.Session.IsSpawned);
    }
}
