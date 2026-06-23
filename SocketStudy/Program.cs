// IPAddress, IPEndPoint 같은 네트워크 주소 타입을 사용하기 위한 namespace입니다.
using System.Net;
// TcpListener, TcpClient, NetworkStream 같은 TCP 소켓 타입을 사용하기 위한 namespace입니다.
using System.Net.Sockets;
// UTF-8 인코딩으로 문자열을 바이트로 바꾸고, 바이트를 문자열로 되돌리기 위한 namespace입니다.
using System.Text;

// 사용자가 포트를 따로 지정하지 않았을 때 사용할 기본 TCP 포트입니다.
const int DefaultPort = 5000;

// 실행 인자가 하나도 없으면 서버/클라이언트 중 무엇을 실행할지 알 수 없습니다.
if (args.Length == 0)
{
    // 사용법을 출력해서 사용자가 실행 방법을 알 수 있게 합니다.
    PrintUsage();
    // 더 진행하지 않고 프로그램을 종료합니다.
    return;
}

// 두 번째 실행 인자에 포트 번호가 있으면 읽고, 없으면 기본 포트를 사용합니다.
if (!TryReadPort(args, out int port))
{
    // 포트 번호가 올바르지 않으면 사용법을 다시 보여줍니다.
    PrintUsage();
    // 잘못된 설정으로 서버/클라이언트를 실행하지 않고 종료합니다.
    return;
}

// 첫 번째 실행 인자를 소문자로 바꿔서 server/client 명령을 구분합니다.
switch (args[0].ToLowerInvariant())
{
    // 사용자가 server를 입력하면 TCP 서버 모드로 실행합니다.
    case "server":
        // 사용자가 지정한 포트 또는 기본 포트로 서버를 시작합니다.
        await RunServerAsync(port);
        // switch 문을 빠져나갑니다.
        break;

    // 사용자가 client를 입력하면 TCP 클라이언트 모드로 실행합니다.
    case "client":
        // 로컬 PC의 서버 127.0.0.1:{port}에 접속합니다.
        await RunClientAsync("127.0.0.1", port);
        // switch 문을 빠져나갑니다.
        break;

    // server/client가 아닌 값이 들어오면 사용법을 다시 보여줍니다.
    default:
        // 올바른 실행 예시를 출력합니다.
        PrintUsage();
        // switch 문을 빠져나갑니다.
        break;
}

// TCP 서버를 실행하는 비동기 메서드입니다.
static async Task RunServerAsync(int port)
{
    // IPAddress.Any는 이 PC의 모든 네트워크 인터페이스에서 접속을 받겠다는 뜻입니다.
    var listener = new TcpListener(IPAddress.Any, port);
    // 지정한 포트에서 클라이언트 접속을 받을 준비를 시작합니다.
    listener.Start();

    // 서버가 어떤 주소와 포트에서 대기 중인지 콘솔에 출력합니다.
    Console.WriteLine($"[server] Listening on 0.0.0.0:{port}");
    // 실습자가 다음에 실행할 클라이언트 명령을 안내합니다.
    Console.WriteLine($"[server] Open another terminal and run: dotnet run -- client {port}");

    // 서버는 보통 계속 켜져 있어야 하므로 무한 반복으로 접속을 기다립니다.
    while (true)
    {
        // 클라이언트가 접속할 때까지 비동기로 기다렸다가, 접속하면 TcpClient 객체를 받습니다.
        TcpClient client = await listener.AcceptTcpClientAsync();
        // 각 클라이언트를 별도 작업으로 처리해서 다음 클라이언트 접속도 계속 받을 수 있게 합니다.
        _ = HandleClientAsync(client);
    }
}

// 접속한 클라이언트 한 명과 메시지를 주고받는 비동기 메서드입니다.
static async Task HandleClientAsync(TcpClient client)
{
    // 접속한 클라이언트의 IP와 포트 정보를 로그로 남기기 위해 가져옵니다.
    IPEndPoint? remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
    // 클라이언트 이름으로 사용할 문자열을 만듭니다.
    string clientName = remoteEndPoint?.ToString() ?? "unknown-client";
    // 클라이언트가 접속했다는 사실을 서버 콘솔에 출력합니다.
    Console.WriteLine($"[server] Client connected: {clientName}");

    // TcpClient에서 실제 데이터를 읽고 쓰는 NetworkStream을 가져옵니다.
    await using NetworkStream stream = client.GetStream();

    // 서버가 접속자 목록에서 관리할 클라이언트 연결 정보를 만듭니다.
    var connection = new ClientConnection(clientName, client, stream);
    // 현재 클라이언트를 서버의 접속자 목록에 추가합니다.
    AddClient(connection);
    // 새로 접속한 클라이언트 본인에게 환영 메시지와 현재 접속자 수를 알려줍니다.
    await SendToClientAsync(connection, $"[notice] Welcome, {clientName}. Online clients: {GetClientCount()}");
    // 기존 클라이언트들에게 새 클라이언트가 들어왔다는 서버 공지를 보냅니다.
    await BroadcastServerNoticeAsync($"{clientName} joined. Online clients: {GetClientCount()}", except: connection);

    // 네트워크 연결은 중간에 끊길 수 있으므로 예외 처리를 준비합니다.
    try
    {
        // 클라이언트가 연결을 유지하는 동안 메시지를 계속 읽습니다.
        while (true)
        {
            // 클라이언트가 보낸 length-prefixed 메시지 하나를 비동기로 읽습니다.
            string? message = await MessageProtocol.ReadMessageAsync(stream);
            // null은 상대방이 메시지 시작 전에 연결을 정상 종료했다는 뜻입니다.
            if (message is null)
            {
                // 메시지 읽기 반복을 종료합니다.
                break;
            }

            // 서버 콘솔에 누가 어떤 채팅 메시지를 보냈는지 기록합니다.
            Console.WriteLine($"[server] Chat from {clientName}: {message}");
            // 받은 메시지를 접속 중인 모든 클라이언트에게 채팅 메시지로 보냅니다.
            await BroadcastChatMessageAsync(connection, message);
        }
    }
    // 네트워크 입출력 중 연결 끊김 같은 문제가 발생하면 IOException이 날 수 있습니다.
    catch (IOException ex)
    {
        // 서버가 죽지 않도록 에러 내용을 로그로만 남깁니다.
        Console.WriteLine($"[server] Connection error: {ex.Message}");
    }
    // 성공/실패와 관계없이 마지막 정리 작업을 수행합니다.
    finally
    {
        // 현재 클라이언트를 서버의 접속자 목록에서 제거합니다.
        RemoveClient(connection);
        // 클라이언트 소켓을 닫아서 운영체제 리소스를 반납합니다.
        client.Close();
        // 남아 있는 클라이언트들에게 이 클라이언트가 나갔다는 서버 공지를 보냅니다.
        await BroadcastServerNoticeAsync($"{clientName} left. Online clients: {GetClientCount()}");
        // 클라이언트 연결이 종료되었다는 사실을 서버 콘솔에 출력합니다.
        Console.WriteLine($"[server] Client disconnected: {clientName}");
    }
}

// TCP 클라이언트를 실행하는 비동기 메서드입니다.
static async Task RunClientAsync(string host, int port)
{
    // 서버에 접속할 TcpClient 객체를 만듭니다.
    using var client = new TcpClient();
    // 지정한 host와 port로 TCP 연결을 시도합니다.
    await client.ConnectAsync(host, port);

    // 서버에 연결되었다는 사실을 클라이언트 콘솔에 출력합니다.
    Console.WriteLine($"[client] Connected to {host}:{port}");
    // 사용자가 메시지를 입력하는 방법과 종료 방법을 안내합니다.
    Console.WriteLine("[client] Type a message and press Enter. Empty line exits.");

    // TcpClient에서 실제 데이터를 읽고 쓰는 NetworkStream을 가져옵니다.
    await using NetworkStream stream = client.GetStream();

    // 서버가 보내는 chat과 notice를 사용자의 입력과 별개로 계속 읽는 작업을 시작합니다.
    Task receiveTask = ReceiveServerMessagesAsync(stream);

    // 사용자가 빈 줄을 입력하기 전까지 계속 메시지를 보냅니다.
    while (true)
    {
        // 입력 프롬프트를 출력합니다.
        Console.Write("> ");
        // 사용자가 콘솔에 입력한 한 줄을 읽습니다.
        string? input = Console.ReadLine();

        // 빈 문자열이나 공백만 입력하면 클라이언트를 종료합니다.
        if (string.IsNullOrWhiteSpace(input))
        {
            // 메시지 입력 반복을 종료합니다.
            break;
        }

        // 사용자가 입력한 메시지를 length-prefixed protocol로 서버에 보냅니다.
        await MessageProtocol.WriteMessageAsync(stream, input);
    }

    // 사용자가 종료하면 소켓을 닫아서 receiveTask의 ReadLineAsync도 끝날 수 있게 합니다.
    client.Close();
    // 백그라운드 수신 작업이 정리될 때까지 기다립니다.
    await receiveTask;
}

// 잘못 실행했을 때 보여줄 사용법 출력 메서드입니다.
static void PrintUsage()
{
    // 프로그램 이름을 출력합니다.
    Console.WriteLine("SocketStudy");
    // 가독성을 위해 빈 줄을 출력합니다.
    Console.WriteLine();
    // 사용법 섹션 제목을 출력합니다.
    Console.WriteLine("Usage:");
    // 서버 실행 명령을 출력합니다.
    Console.WriteLine("  dotnet run -- server [port]");
    // 클라이언트 실행 명령을 출력합니다.
    Console.WriteLine("  dotnet run -- client [port]");
    // 포트를 생략했을 때 사용할 기본값을 출력합니다.
    Console.WriteLine();
    // 기본 포트 번호를 안내합니다.
    Console.WriteLine($"Default port: {DefaultPort}");
}

// 실행 인자에서 포트 번호를 읽는 메서드입니다.
static bool TryReadPort(string[] args, out int port)
{
    // 먼저 기본 포트를 넣어 두면, 사용자가 포트를 생략한 경우 그대로 사용할 수 있습니다.
    port = DefaultPort;

    // 두 번째 실행 인자가 없으면 포트를 생략한 것이므로 기본 포트를 사용합니다.
    if (args.Length < 2)
    {
        // 포트 읽기에 성공한 것으로 처리합니다.
        return true;
    }

    // 문자열로 들어온 포트 값을 int로 바꿔 봅니다.
    bool parsed = int.TryParse(args[1], out int parsedPort);
    // 파싱에 실패했거나 TCP 포트 범위인 1~65535를 벗어나면 잘못된 값입니다.
    if (!parsed || parsedPort < 1 || parsedPort > 65535)
    {
        // 어떤 값이 잘못되었는지 콘솔에 출력합니다.
        Console.WriteLine($"Invalid port: {args[1]}");
        // 포트 읽기에 실패했다고 호출자에게 알려줍니다.
        return false;
    }

    // 검증을 통과한 포트 값을 실제로 사용할 포트 변수에 넣습니다.
    port = parsedPort;
    // 포트 읽기에 성공했다고 호출자에게 알려줍니다.
    return true;
}

// 채팅 메시지를 접속 중인 모든 클라이언트에게 보내는 메서드입니다.
static async Task BroadcastChatMessageAsync(ClientConnection sender, string message)
{
    // lock 안에서 await를 하지 않기 위해 먼저 보낼 대상 목록의 복사본을 만듭니다.
    ClientConnection[] clients;

    // 접속자 목록을 복사하는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
    lock (ServerState.Gate)
    {
        // 현재 접속해 있는 모든 클라이언트를 전송 대상으로 복사합니다.
        clients = ServerState.Clients.ToArray();
    }

    // 클라이언트 화면에 표시할 채팅 메시지 형식을 만듭니다.
    string chatMessage = $"[chat] {sender.Name}: {message}";

    // 복사해 둔 클라이언트 목록을 돌면서 채팅 메시지를 보냅니다.
    foreach (ClientConnection client in clients)
    {
        // 보낸 사람을 포함한 모든 접속자에게 같은 채팅 메시지를 전달합니다.
        await SendToClientAsync(client, chatMessage);
    }
}

// 현재 클라이언트를 접속자 목록에 추가하는 메서드입니다.
static void AddClient(ClientConnection connection)
{
    // 여러 클라이언트 작업이 동시에 목록을 바꾸지 못하도록 lock으로 보호합니다.
    lock (ServerState.Gate)
    {
        // 접속자 목록에 새 연결을 추가합니다.
        ServerState.Clients.Add(connection);
    }

    // 서버 콘솔에 현재 접속자 수를 출력합니다.
    Console.WriteLine($"[server] Online clients: {GetClientCount()}");
}

// 현재 클라이언트를 접속자 목록에서 제거하는 메서드입니다.
static void RemoveClient(ClientConnection connection)
{
    // 여러 클라이언트 작업이 동시에 목록을 바꾸지 못하도록 lock으로 보호합니다.
    lock (ServerState.Gate)
    {
        // 접속자 목록에서 연결을 제거합니다.
        ServerState.Clients.Remove(connection);
    }

    // 서버 콘솔에 현재 접속자 수를 출력합니다.
    Console.WriteLine($"[server] Online clients: {GetClientCount()}");
}

// 현재 접속자 수를 가져오는 메서드입니다.
static int GetClientCount()
{
    // 접속자 목록을 읽는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
    lock (ServerState.Gate)
    {
        // 현재 접속자 목록의 개수를 반환합니다.
        return ServerState.Clients.Count;
    }
}

// 서버 공지를 여러 클라이언트에게 보내는 메서드입니다.
static async Task BroadcastServerNoticeAsync(string message, ClientConnection? except = null)
{
    // lock 안에서 await를 하지 않기 위해 먼저 보낼 대상 목록의 복사본을 만듭니다.
    ClientConnection[] clients;

    // 접속자 목록을 복사하는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
    lock (ServerState.Gate)
    {
        // except로 전달된 클라이언트는 공지 대상에서 제외합니다.
        clients = ServerState.Clients
            .Where(client => client != except)
            .ToArray();
    }

    // 복사해 둔 클라이언트 목록을 돌면서 공지 메시지를 보냅니다.
    foreach (ClientConnection client in clients)
    {
        // notice prefix를 붙여서 일반 chat 메시지와 구분합니다.
        await SendToClientAsync(client, $"[notice] {message}");
    }
}

// 클라이언트 한 명에게 메시지를 안전하게 보내는 메서드입니다.
static async Task SendToClientAsync(ClientConnection connection, string message)
{
    // 네트워크 전송은 실패할 수 있으므로 예외 처리를 준비합니다.
    try
    {
        // 한 클라이언트에게 여러 작업이 동시에 쓰는 일을 막기 위해 연결 객체의 전송 메서드를 사용합니다.
        await connection.SendAsync(message);
    }
    // 클라이언트 연결이 이미 끊긴 경우에는 IOException이 발생할 수 있습니다.
    catch (IOException ex)
    {
        // 서버 전체가 멈추지 않도록 전송 실패만 로그로 남깁니다.
        Console.WriteLine($"[server] Failed to send to {connection.Name}: {ex.Message}");
    }
    // writer나 socket이 이미 정리된 경우에는 ObjectDisposedException이 발생할 수 있습니다.
    catch (ObjectDisposedException)
    {
        // 이미 닫힌 연결이므로 별도 복구 없이 로그만 남깁니다.
        Console.WriteLine($"[server] Failed to send to {connection.Name}: connection closed");
    }
}

// 클라이언트가 서버에서 오는 메시지를 계속 읽는 메서드입니다.
static async Task ReceiveServerMessagesAsync(NetworkStream stream)
{
    // 네트워크 수신은 실패할 수 있으므로 예외 처리를 준비합니다.
    try
    {
        // 서버가 연결을 유지하는 동안 메시지를 계속 읽습니다.
        while (true)
        {
            // 서버에서 보낸 length-prefixed 메시지 하나를 비동기로 읽습니다.
            string? message = await MessageProtocol.ReadMessageAsync(stream);
            // null은 서버가 메시지 시작 전에 연결을 종료했다는 뜻입니다.
            if (message is null)
            {
                // 메시지 읽기 반복을 종료합니다.
                break;
            }

            // 입력 프롬프트와 겹치지 않도록 한 줄 내려서 서버 메시지를 출력합니다.
            Console.WriteLine();
            // 서버에서 받은 메시지를 클라이언트 콘솔에 출력합니다.
            Console.WriteLine($"< {message}");
            // 사용자가 계속 입력할 수 있게 프롬프트를 다시 보여줍니다.
            Console.Write("> ");
        }
    }
    // 클라이언트가 직접 종료하면서 소켓을 닫으면 IOException이 발생할 수 있습니다.
    catch (IOException)
    {
        // 사용자가 종료한 상황에서는 조용히 수신 작업을 끝냅니다.
    }
    // stream이 이미 정리된 경우에도 수신 작업을 조용히 끝냅니다.
    catch (ObjectDisposedException)
    {
        // 이미 닫힌 연결이므로 추가 작업 없이 종료합니다.
    }
}

// 서버 전체에서 공유하는 접속자 목록과 lock 객체를 담는 클래스입니다.
static class ServerState
{
    // 접속자 목록을 동시에 읽고 쓸 때 보호하기 위한 lock 객체입니다.
    public static readonly object Gate = new();

    // 현재 서버에 접속해 있는 클라이언트 목록입니다.
    public static readonly List<ClientConnection> Clients = new();
}

// 서버가 클라이언트 한 명을 관리하기 위해 들고 있는 연결 정보입니다.
sealed class ClientConnection
{
    // 클라이언트를 구분하기 위한 이름입니다.
    public string Name { get; }

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
    public async Task SendAsync(string message)
    {
        // 다른 전송 작업이 끝날 때까지 기다립니다.
        await sendLock.WaitAsync();

        // lock을 잡은 뒤에는 반드시 finally에서 풀어야 합니다.
        try
        {
            // 클라이언트에게 length-prefixed protocol로 문자열 메시지를 보냅니다.
            await MessageProtocol.WriteMessageAsync(stream, message);
        }
        // 전송이 끝났거나 실패해도 다음 전송이 막히지 않게 lock을 풉니다.
        finally
        {
            // 비동기 lock을 해제합니다.
            sendLock.Release();
        }
    }
}

// TCP 바이트 흐름 위에 "메시지"라는 단위를 얹기 위한 protocol helper입니다.
static class MessageProtocol
{
    // 메시지 길이를 담는 header 크기입니다. int 하나를 4바이트 big-endian으로 보냅니다.
    private const int HeaderSize = 4;
    // 한 번에 받을 수 있는 메시지 본문의 최대 크기입니다. 너무 큰 메시지로 서버가 메모리를 많이 쓰는 일을 막습니다.
    private const int MaxMessageBytes = 1024 * 1024;

    // 문자열 메시지를 "4바이트 길이 + UTF-8 본문" 형식으로 stream에 씁니다.
    public static async Task WriteMessageAsync(NetworkStream stream, string message)
    {
        // 문자열을 UTF-8 바이트 배열로 변환합니다.
        byte[] body = Encoding.UTF8.GetBytes(message);
        // 본문이 너무 크면 protocol 위반으로 보고 보내지 않습니다.
        if (body.Length > MaxMessageBytes)
        {
            // 호출자가 문제를 알 수 있도록 예외를 발생시킵니다.
            throw new InvalidOperationException($"Message is too large: {body.Length} bytes");
        }

        // 4바이트 header 배열을 준비합니다.
        byte[] header = new byte[HeaderSize];
        // 메시지 본문 길이를 네트워크 바이트 순서(big-endian)로 header에 기록합니다.
        WriteInt32BigEndian(header, body.Length);

        // 먼저 header를 보냅니다.
        await stream.WriteAsync(header);
        // 그 다음 실제 메시지 본문을 보냅니다.
        await stream.WriteAsync(body);
        // 버퍼에 남은 데이터가 있다면 즉시 네트워크로 밀어냅니다.
        await stream.FlushAsync();
    }

    // stream에서 "4바이트 길이 + UTF-8 본문" 형식의 메시지 하나를 읽습니다.
    public static async Task<string?> ReadMessageAsync(NetworkStream stream)
    {
        // 4바이트 header를 담을 배열을 준비합니다.
        byte[] header = new byte[HeaderSize];
        // header를 정확히 4바이트 읽습니다. 시작 전에 연결이 닫히면 null이 돌아옵니다.
        bool hasHeader = await ReadExactOrEndAsync(stream, header);
        // header를 읽기 전에 연결이 끝났으면 메시지가 없다는 뜻입니다.
        if (!hasHeader)
        {
            // 호출자에게 정상 연결 종료를 알립니다.
            return null;
        }

        // header의 4바이트를 int 길이 값으로 바꿉니다.
        int bodyLength = ReadInt32BigEndian(header);
        // 길이가 음수거나 너무 크면 잘못된 protocol 데이터입니다.
        if (bodyLength < 0 || bodyLength > MaxMessageBytes)
        {
            // 잘못된 길이를 받은 연결은 더 읽지 않고 예외로 처리합니다.
            throw new IOException($"Invalid message length: {bodyLength}");
        }

        // 길이가 0인 메시지는 빈 문자열로 처리합니다.
        if (bodyLength == 0)
        {
            // 빈 메시지를 반환합니다.
            return string.Empty;
        }

        // 본문 길이만큼 바이트 배열을 준비합니다.
        byte[] body = new byte[bodyLength];
        // 본문을 정확히 bodyLength 바이트만큼 읽습니다.
        bool hasBody = await ReadExactOrEndAsync(stream, body);
        // header는 왔는데 body가 중간에 끊기면 protocol이 깨진 상태입니다.
        if (!hasBody)
        {
            // 연결이 중간에 끊겼다는 예외를 발생시킵니다.
            throw new IOException("Connection closed before message body was fully received.");
        }

        // UTF-8 바이트를 문자열로 변환해 호출자에게 반환합니다.
        return Encoding.UTF8.GetString(body);
    }

    // 요청한 크기만큼 정확히 읽거나, 읽기 시작 전에 연결 종료를 감지합니다.
    private static async Task<bool> ReadExactOrEndAsync(NetworkStream stream, byte[] buffer)
    {
        // 지금까지 읽은 바이트 수입니다.
        int totalRead = 0;

        // buffer 전체가 채워질 때까지 반복합니다.
        while (totalRead < buffer.Length)
        {
            // 아직 채워야 하는 구간을 stream에서 읽습니다.
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead));
            // read가 0이면 상대방이 연결을 닫았다는 뜻입니다.
            if (read == 0)
            {
                // 한 바이트도 못 읽은 상태라면 정상적인 연결 종료로 볼 수 있습니다.
                if (totalRead == 0)
                {
                    // 호출자에게 데이터 없이 종료되었다고 알려줍니다.
                    return false;
                }

                // 일부만 읽고 끊긴 것은 메시지가 깨진 상황입니다.
                throw new IOException("Connection closed before the message was fully received.");
            }

            // 이번에 읽은 바이트 수를 누적합니다.
            totalRead += read;
        }

        // 요청한 바이트 수를 모두 읽었습니다.
        return true;
    }

    // int 값을 4바이트 big-endian 배열에 씁니다.
    private static void WriteInt32BigEndian(byte[] buffer, int value)
    {
        // 가장 높은 8비트를 첫 번째 바이트에 씁니다.
        buffer[0] = (byte)(value >> 24);
        // 다음 8비트를 두 번째 바이트에 씁니다.
        buffer[1] = (byte)(value >> 16);
        // 다음 8비트를 세 번째 바이트에 씁니다.
        buffer[2] = (byte)(value >> 8);
        // 가장 낮은 8비트를 네 번째 바이트에 씁니다.
        buffer[3] = (byte)value;
    }

    // 4바이트 big-endian 배열에서 int 값을 읽습니다.
    private static int ReadInt32BigEndian(byte[] buffer)
    {
        // 각 바이트를 int로 올린 뒤 자리수에 맞게 shift해서 합칩니다.
        return (buffer[0] << 24)
            | (buffer[1] << 16)
            | (buffer[2] << 8)
            | buffer[3];
    }
}
