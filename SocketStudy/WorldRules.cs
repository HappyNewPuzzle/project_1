// MMO 월드에서 서버가 검증해야 하는 기본 규칙을 모아둡니다.
public static class WorldRules
{
    // 학습용 월드의 최소 좌표입니다.
    public const int MinCoordinate = -100;

    // 학습용 월드의 최대 좌표입니다.
    public const int MaxCoordinate = 100;

    // 위치가 학습용 월드 경계 안에 있는지 확인합니다.
    public static bool IsInsideWorld(WorldPosition position)
    {
        // x, y 좌표가 모두 허용 범위 안에 있어야 합니다.
        return position.X >= MinCoordinate &&
            position.X <= MaxCoordinate &&
            position.Y >= MinCoordinate &&
            position.Y <= MaxCoordinate;
    }
}
