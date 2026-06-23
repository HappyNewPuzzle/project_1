// 서버 전체에서 공유하는 접속자 목록과 lock 객체를 담는 클래스입니다.
static class ServerState
{
    // 접속자 목록을 동시에 읽고 쓸 때 보호하기 위한 lock 객체입니다.
    public static readonly object Gate = new();

    // 현재 서버에 접속해 있는 클라이언트 목록입니다.
    public static readonly List<ClientConnection> Clients = new();
}
