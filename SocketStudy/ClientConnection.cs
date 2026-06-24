using System.Net.Sockets;

// 서버가 클라이언트 한 명을 관리하기 위해 들고 있는 연결 정보입니다.
public sealed class ClientConnection
{
    // 클라이언트를 구분하기 위한 이름입니다.
    public string Name { get; private set; }

    // 클라이언트가 현재 들어가 있는 채팅방 이름입니다.
    public string RoomName { get; private set; } = ClientRegistry.DefaultRoomName;

    // 실제 TCP 연결 객체입니다.
    public TcpClient Client { get; }

    // 클라이언트에게 바이트를 보내기 위한 network stream입니다.
    private readonly NetworkStream stream;

    // 같은 클라이언트에게 동시에 여러 메시지를 쓰지 않도록 막는 비동기 lock입니다.
    private readonly SemaphoreSlim sendLock = new(1, 1);

    // 클라이언트 연결 정보를 초기화합니다.
    public ClientConnection(string name, TcpClient client, NetworkStream stream)
    {
        // 클라이언트 이름을 저장합니다.
        Name = name;
        // TCP 연결 객체를 저장합니다.
        Client = client;
        // 메시지 전송용 stream을 저장합니다.
        this.stream = stream;
    }

    // 이 클라이언트에게 문자열 한 줄을 보내는 메서드입니다.
    public async Task SendAsync(MessageType type, string message)
    {
        // 다른 전송 작업이 끝날 때까지 기다립니다.
        await sendLock.WaitAsync();

        // lock을 잡은 뒤에는 반드시 finally에서 풀어야 합니다.
        try
        {
            // 클라이언트에게 length-prefixed protocol로 문자열 메시지를 보냅니다.
            await MessageProtocol.WriteMessageAsync(stream, type, message);
        }
        // 전송이 끝났거나 실패해도 다음 전송이 막히지 않게 lock을 풉니다.
        finally
        {
            // 비동기 lock을 해제합니다.
            sendLock.Release();
        }
    }

    // 이 클라이언트의 TCP 연결을 닫는 메서드입니다.
    public void Close()
    {
        // TcpClient를 닫으면 내부 stream도 함께 닫힙니다.
        Client.Close();
    }

    // 클라이언트 표시 이름을 변경하는 메서드입니다.
    public void Rename(string name)
    {
        // 새 이름을 저장합니다.
        Name = name;
    }

    // 클라이언트가 속한 채팅방을 변경하는 메서드입니다.
    public void MoveToRoom(string roomName)
    {
        // 새 채팅방 이름을 저장합니다.
        RoomName = roomName;
    }
}
