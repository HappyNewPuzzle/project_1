// 서버 예제의 이름과 버전처럼 여러 곳에서 공유할 정보를 모아둡니다.
public static class ServerInfo
{
    // 서버 예제 이름입니다.
    public const string Name = "SocketStudy";

    // 서버 예제 버전입니다.
    public const string Version = "v1";

    // 사용자에게 보여줄 서버 버전 문구입니다.
    public const string VersionMessage = $"{Name} server {Version}";
}
