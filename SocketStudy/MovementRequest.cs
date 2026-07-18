// 서버 tick에서 처리할 플레이어 이동 요청입니다.
public sealed record MovementRequest(
    // 클라이언트가 보낸 이동 순서 번호입니다.
    long Sequence,
    // 이동하려는 목표 위치입니다.
    WorldPosition TargetPosition,
    // 서버가 요청을 처리하는 기준 시각입니다.
    DateTimeOffset ServerTime);
