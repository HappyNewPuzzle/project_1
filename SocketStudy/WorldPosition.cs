// MMO 월드에서 플레이어의 2D 위치를 표현하는 값입니다.
public readonly record struct WorldPosition(int X, int Y)
{
    // 새 세션이 시작할 기본 위치입니다.
    public static WorldPosition Origin { get; } = new(0, 0);

    // 사용자에게 보여줄 좌표 문자열을 만듭니다.
    public override string ToString()
    {
        // 로그와 명령 응답에서 같은 형식으로 쓰기 위해 한곳에서 문자열을 만듭니다.
        return $"x={X}, y={Y}";
    }
}
