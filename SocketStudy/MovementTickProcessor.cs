// 플레이어 이동 요청을 서버 tick 관점에서 검증하고 적용합니다.
public static class MovementTickProcessor
{
    // 이동 요청을 검증한 뒤 통과하면 세션 위치를 변경합니다.
    public static MovementTickResult Process(PlayerSession session, MovementRequest request)
    {
        // 오래되었거나 중복된 이동 순서 번호는 세션 상태를 바꾸지 않습니다.
        if (!session.CanAcceptMoveSequence(request.Sequence))
        {
            return MovementTickResult.Rejected($"Move sequence must be greater than {session.LastMoveSequence}.");
        }

        // 서버가 허용하는 월드 경계 밖의 위치는 거절합니다.
        if (!WorldRules.IsInsideWorld(request.TargetPosition))
        {
            return MovementTickResult.Rejected($"Position must be between {WorldRules.MinCoordinate} and {WorldRules.MaxCoordinate}.");
        }

        // 현재 위치에서 한 번에 이동 가능한 거리인지 확인합니다.
        if (!WorldRules.IsWithinMoveDistance(session.Position, request.TargetPosition))
        {
            return MovementTickResult.Rejected($"Move distance must be {WorldRules.MaxMoveDistance} or less.");
        }

        // 마지막 성공 이동 이후 충분한 서버 시간이 지났는지 확인합니다.
        if (!WorldRules.IsMoveCooldownElapsed(session.LastMoveAt, request.ServerTime))
        {
            return MovementTickResult.Rejected("You must wait 1 second between moves.");
        }

        // 모든 검증을 통과한 이동만 세션 상태에 적용합니다.
        session.MoveTo(request.TargetPosition, request.ServerTime, request.Sequence);
        // 성공 결과를 반환합니다.
        return MovementTickResult.Accepted();
    }
}
