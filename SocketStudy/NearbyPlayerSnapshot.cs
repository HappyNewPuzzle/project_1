// 주변 플레이어를 클라이언트에 알려줄 때 필요한 최소 상태 묶음입니다.
public readonly record struct NearbyPlayerSnapshot(
    // 화면에 표시할 현재 닉네임입니다.
    string Name,
    // 게임 서버가 식별하는 플레이어 ID입니다.
    long PlayerId,
    // 플레이어가 존재하는 게임 맵 ID입니다.
    int MapId,
    // 해당 맵 안에서의 현재 위치입니다.
    WorldPosition Position);
