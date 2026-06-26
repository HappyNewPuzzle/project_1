// MMO 월드에서 서버가 검증해야 하는 기본 규칙을 모아둡니다.
public static class WorldRules
{
    // 학습용 월드의 최소 좌표입니다.
    public const int MinCoordinate = -100;

    // 학습용 월드의 최대 좌표입니다.
    public const int MaxCoordinate = 100;

    // 주변 플레이어로 판단할 학습용 시야 거리입니다.
    public const int ViewDistance = 25;

    // 위치가 학습용 월드 경계 안에 있는지 확인합니다.
    public static bool IsInsideWorld(WorldPosition position)
    {
        // x, y 좌표가 모두 허용 범위 안에 있어야 합니다.
        return position.X >= MinCoordinate &&
            position.X <= MaxCoordinate &&
            position.Y >= MinCoordinate &&
            position.Y <= MaxCoordinate;
    }

    // 두 위치가 서로 볼 수 있는 거리 안에 있는지 확인합니다.
    public static bool IsNearby(WorldPosition first, WorldPosition second)
    {
        // 학습용 서버에서는 계산이 단순한 맨해튼 거리를 사용합니다.
        int distance = Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
        // 거리가 시야 거리 이하이면 nearby로 봅니다.
        return distance <= ViewDistance;
    }
}
