// 게임 월드 안에 존재할 수 있는 엔티티의 공통 상태입니다.
public abstract record WorldEntity(
    // 월드 안에서 엔티티를 식별하는 ID입니다.
    long EntityId,
    // 엔티티가 존재하는 맵 ID입니다.
    int MapId,
    // 맵 안에서의 현재 위치입니다.
    WorldPosition Position,
    // 현재 월드에 실제로 등장한 상태인지 나타냅니다.
    bool IsSpawned);
