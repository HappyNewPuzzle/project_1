// MMO 월드에서 서버가 검증해야 하는 기본 규칙을 모아둡니다.
public static class WorldRules
{
    // 새 플레이어 세션이 시작할 기본 맵 ID입니다.
    public const int DefaultMapId = 1;

    // 학습용 월드의 최소 좌표입니다.
    public const int MinCoordinate = -100;

    // 학습용 월드의 최대 좌표입니다.
    public const int MaxCoordinate = 100;

    // 주변 플레이어로 판단할 학습용 시야 거리입니다.
    public const int ViewDistance = 25;

    // 한 번의 이동 명령으로 허용할 최대 맨해튼 거리입니다.
    public const int MaxMoveDistance = 10;

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
        // 두 위치 사이의 맨해튼 거리를 계산합니다.
        long distance = GetDistance(first, second);
        // 거리가 시야 거리 이하이면 nearby로 봅니다.
        return distance <= ViewDistance;
    }

    // 한 번의 이동으로 도착할 수 있는 거리인지 확인합니다.
    public static bool IsWithinMoveDistance(WorldPosition current, WorldPosition next)
    {
        // 현재 위치와 다음 위치 사이의 거리를 이동 제한과 비교합니다.
        return GetDistance(current, next) <= MaxMoveDistance;
    }

    // 두 월드 위치 사이의 맨해튼 거리를 계산합니다.
    private static long GetDistance(WorldPosition first, WorldPosition second)
    {
        // long으로 변환해 큰 좌표의 뺄셈에서도 int 오버플로를 피합니다.
        return Math.Abs((long)first.X - second.X) + Math.Abs((long)first.Y - second.Y);
    }
}
