// IPAddress, IPEndPoint 같은 네트워크 주소 타입을 사용하기 위한 namespace입니다.
using System.Net;
// TcpListener, TcpClient, NetworkStream 같은 TCP 소켓 타입을 사용하기 위한 namespace입니다.
using System.Net.Sockets;
// UTF-8 인코딩으로 문자열을 바이트로 바꾸고, 바이트를 문자열로 되돌리기 위한 namespace입니다.
using System.Text;

// 사용자가 포트를 따로 지정하지 않았을 때 사용할 기본 TCP 포트입니다.
const int DefaultPort = 5000;

// Ctrl+C 같은 종료 요청을 프로그램 전체에 전달하기 위한 cancellation token source입니다.
using var appCancellation = new CancellationTokenSource();
// 콘솔에서 Ctrl+C를 누르면 프로그램을 즉시 강제 종료하지 않고 token만 취소하도록 이벤트를 등록합니다.
Console.CancelKeyPress += (_, eventArgs) =>
{
    // 기본 Ctrl+C 동작인 프로세스 즉시 종료를 막습니다.
    eventArgs.Cancel = true;
    // 서버/클라이언트 루프에 종료 요청을 전달합니다.
    appCancellation.Cancel();
    // 사용자에게 종료가 시작되었음을 알려줍니다.
    Console.WriteLine();
    // 종료 안내를 콘솔에 출력합니다.
    AppLogger.Info("[app] Shutdown requested...");
};

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

// 세 번째 실행 인자에 클라이언트 닉네임이 있으면 읽고, 없으면 null을 사용합니다.
string? nickname = TryReadNickname(args);

// 첫 번째 실행 인자를 소문자로 바꿔서 server/client 명령을 구분합니다.
switch (args[0].ToLowerInvariant())
{
    // 사용자가 server를 입력하면 TCP 서버 모드로 실행합니다.
    case "server":
        // 사용자가 지정한 포트 또는 기본 포트로 서버를 시작합니다.
        await RunServerAsync(port, appCancellation.Token);
        // switch 문을 빠져나갑니다.
        break;

    // 사용자가 client를 입력하면 TCP 클라이언트 모드로 실행합니다.
    case "client":
        // 로컬 PC의 서버 127.0.0.1:{port}에 접속합니다.
        await RunClientAsync("127.0.0.1", port, nickname, appCancellation.Token);
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
static async Task RunServerAsync(int port, CancellationToken cancellationToken)
{
    // IPAddress.Any는 이 PC의 모든 네트워크 인터페이스에서 접속을 받겠다는 뜻입니다.
    var listener = new TcpListener(IPAddress.Any, port);

    // 서버 종료 시 listener를 반드시 닫기 위해 try/finally를 사용합니다.
    try
    {
        // 지정한 포트에서 클라이언트 접속을 받을 준비를 시작합니다.
        listener.Start();

        // 서버가 어떤 주소와 포트에서 대기 중인지 콘솔에 출력합니다.
        AppLogger.Info($"[server] Listening on 0.0.0.0:{port}");
        // 실습자가 다음에 실행할 클라이언트 명령을 안내합니다.
        AppLogger.Info($"[server] Open another terminal and run: dotnet run -- client {port}");
        // 종료 방법을 안내합니다.
        AppLogger.Info("[server] Press Ctrl+C to stop the server.");

        // 서버는 종료 요청이 오기 전까지 계속 접속을 기다립니다.
        while (!cancellationToken.IsCancellationRequested)
        {
            // 클라이언트가 접속할 때까지 비동기로 기다렸다가, 접속하면 TcpClient 객체를 받습니다.
            TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
            // 각 클라이언트를 별도 작업으로 처리해서 다음 클라이언트 접속도 계속 받을 수 있게 합니다.
            _ = HandleClientAsync(client, cancellationToken);
        }
    }
    // Ctrl+C로 cancellationToken이 취소되면 accept 대기가 OperationCanceledException을 던질 수 있습니다.
    catch (OperationCanceledException)
    {
        // 정상 종료 흐름이므로 에러로 취급하지 않습니다.
        AppLogger.Info("[server] Accept loop stopped.");
    }
    // 성공/실패와 관계없이 서버 socket을 닫고 접속 중인 클라이언트를 정리합니다.
    finally
    {
        // 더 이상 새 접속을 받지 않도록 listener를 닫습니다.
        listener.Stop();
        // 현재 접속 중인 모든 클라이언트 연결을 닫습니다.
        CloseAllClients();
        // 서버 종료 완료를 콘솔에 출력합니다.
        AppLogger.Info("[server] Server stopped.");
    }
}

// 접속한 클라이언트 한 명과 메시지를 주고받는 비동기 메서드입니다.
static async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
{
    // 접속한 클라이언트의 IP와 포트 정보를 로그로 남기기 위해 가져옵니다.
    IPEndPoint? remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
    // 클라이언트 이름으로 사용할 문자열을 만듭니다.
    string clientName = remoteEndPoint?.ToString() ?? "unknown-client";
    // 클라이언트가 접속했다는 사실을 서버 콘솔에 출력합니다.
    AppLogger.Info($"[server] Client connected: {clientName}");

    // TcpClient에서 실제 데이터를 읽고 쓰는 NetworkStream을 가져옵니다.
    await using NetworkStream stream = client.GetStream();

    // 서버가 접속자 목록에서 관리할 클라이언트 연결 정보를 만듭니다.
    var connection = new ClientConnection(clientName, client, stream);
    // 현재 클라이언트를 서버의 접속자 목록에 추가합니다.
    AddClient(connection);
    // 새로 접속한 클라이언트 본인에게 환영 메시지와 현재 접속자 수를 알려줍니다.
    await SendToClientAsync(connection, MessageType.Notice, $"Welcome, {clientName}. Online clients: {GetClientCount()}");
    // 기존 클라이언트들에게 새 클라이언트가 들어왔다는 서버 공지를 보냅니다.
    await BroadcastServerNoticeAsync($"{clientName} joined. Online clients: {GetClientCount()}", except: connection);

    // 네트워크 연결은 중간에 끊길 수 있으므로 예외 처리를 준비합니다.
    try
    {
        // 클라이언트가 연결을 유지하는 동안 메시지를 계속 읽습니다.
        while (true)
        {
            // 클라이언트가 보낸 protocol 메시지 하나를 비동기로 읽습니다.
            NetworkMessage? message = await MessageProtocol.ReadMessageAsync(stream, cancellationToken);
            // null은 상대방이 메시지 시작 전에 연결을 정상 종료했다는 뜻입니다.
            if (message is null)
            {
                // 메시지 읽기 반복을 종료합니다.
                break;
            }

            // 서버 명령이면 채팅 broadcast 대신 명령을 처리합니다.
            if (await TryHandleServerCommandAsync(connection, message))
            {
                // 명령 처리가 끝났으므로 다음 메시지를 기다립니다.
                continue;
            }

            // 서버 콘솔에 누가 어떤 채팅 메시지를 보냈는지 기록합니다.
            AppLogger.Info($"[server] Chat from {connection.Name}: {message.Text}");
            // 받은 메시지를 접속 중인 모든 클라이언트에게 채팅 메시지로 보냅니다.
            await BroadcastChatMessageAsync(connection, message.Text);
        }
    }
    // 네트워크 입출력 중 연결 끊김 같은 문제가 발생하면 IOException이 날 수 있습니다.
    catch (IOException ex)
    {
        // 서버가 죽지 않도록 에러 내용을 로그로만 남깁니다.
        AppLogger.Error($"[server] Connection error: {ex.Message}");
    }
    // /quit 명령이나 서버 정리 과정에서 stream이 먼저 닫힐 수 있습니다.
    catch (ObjectDisposedException)
    {
        // 이미 닫힌 연결이므로 정상적인 연결 종료 흐름으로 처리합니다.
        AppLogger.Info($"[server] Connection closed: {connection.Name}");
    }
    // 서버 종료 요청 때문에 읽기 작업이 취소될 수 있습니다.
    catch (OperationCanceledException)
    {
        // 정상 종료 흐름이므로 에러로 취급하지 않습니다.
        AppLogger.Info($"[server] Connection canceled: {clientName}");
    }
    // 성공/실패와 관계없이 마지막 정리 작업을 수행합니다.
    finally
    {
        // 현재 클라이언트를 서버의 접속자 목록에서 제거합니다.
        RemoveClient(connection);
        // 클라이언트 소켓을 닫아서 운영체제 리소스를 반납합니다.
        client.Close();
        // 남아 있는 클라이언트들에게 이 클라이언트가 나갔다는 서버 공지를 보냅니다.
        await BroadcastServerNoticeAsync($"{connection.Name} left. Online clients: {GetClientCount()}");
        // 클라이언트 연결이 종료되었다는 사실을 서버 콘솔에 출력합니다.
        AppLogger.Info($"[server] Client disconnected: {connection.Name}");
    }
}

// TCP 클라이언트를 실행하는 비동기 메서드입니다.
static async Task RunClientAsync(
    string host,
    int port,
    string? nickname,
    CancellationToken cancellationToken)
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
    Task receiveTask = ReceiveServerMessagesAsync(stream, cancellationToken);

    // 실행 인자로 닉네임을 받았다면 접속 직후 서버에 이름 변경 명령을 보냅니다.
    if (!string.IsNullOrWhiteSpace(nickname))
    {
        // 서버가 이해하는 /name 명령 형식으로 닉네임을 전달합니다.
        await MessageProtocol.WriteMessageAsync(stream, MessageType.Command, $"/name {nickname}", cancellationToken);
    }

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

        // slash로 시작하면 명령 메시지로, 아니면 일반 채팅 메시지로 서버에 보냅니다.
        MessageType type = input.StartsWith('/') ? MessageType.Command : MessageType.Chat;
        // 사용자가 입력한 메시지를 typed length-prefixed protocol로 서버에 보냅니다.
        await MessageProtocol.WriteMessageAsync(stream, type, input, cancellationToken);
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
    Console.WriteLine("  dotnet run -- client [port] [nickname]");
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

// 실행 인자에서 클라이언트 닉네임을 읽는 메서드입니다.
static string? TryReadNickname(string[] args)
{
    // 세 번째 실행 인자가 없으면 닉네임을 지정하지 않은 것입니다.
    if (args.Length < 3)
    {
        // 닉네임 없음으로 처리합니다.
        return null;
    }

    // 앞뒤 공백을 제거한 닉네임을 반환합니다.
    return args[2].Trim();
}

// 서버에서 처리해야 하는 slash command인지 확인하고 처리하는 메서드입니다.
static async Task<bool> TryHandleServerCommandAsync(ClientConnection connection, NetworkMessage message)
{
    // command 타입이 아니고 slash로 시작하지 않으면 일반 채팅 메시지입니다.
    if (message.Type != MessageType.Command && !message.Text.StartsWith('/'))
    {
        // 명령이 아니라고 호출자에게 알려줍니다.
        return false;
    }

    // /name 명령은 클라이언트의 표시 이름을 바꿉니다.
    if (message.Text.StartsWith("/name ", StringComparison.OrdinalIgnoreCase))
    {
        // 명령 뒤쪽의 닉네임 부분만 잘라냅니다.
        string requestedName = message.Text["/name ".Length..].Trim();
        // 닉네임 변경을 처리합니다.
        await ChangeClientNameAsync(connection, requestedName);
        // 명령을 처리했다고 호출자에게 알려줍니다.
        return true;
    }

    // /users 명령은 현재 접속 중인 클라이언트 이름 목록을 보여줍니다.
    if (message.Text.Equals("/users", StringComparison.OrdinalIgnoreCase))
    {
        // 현재 접속자 이름 목록을 가져옵니다.
        string[] names = GetClientNames();
        // 접속자 목록을 보낸 사람에게만 알려줍니다.
        await SendToClientAsync(connection, MessageType.Notice, $"Online users ({names.Length}): {string.Join(", ", names)}");
        // 명령을 처리했다고 호출자에게 알려줍니다.
        return true;
    }

    // /quit 명령은 클라이언트가 서버에 연결 종료 의사를 명확히 전달하는 명령입니다.
    if (message.Text.Equals("/quit", StringComparison.OrdinalIgnoreCase))
    {
        // 보낸 사람에게 연결을 종료한다는 안내를 보냅니다.
        await SendToClientAsync(connection, MessageType.Notice, "Goodbye.");
        // 소켓을 닫아서 해당 클라이언트 처리 루프가 끝나도록 만듭니다.
        connection.Close();
        // 명령을 처리했다고 호출자에게 알려줍니다.
        return true;
    }

    // 알 수 없는 명령은 보낸 사람에게만 안내합니다.
    await SendToClientAsync(connection, MessageType.Notice, $"Unknown command: {message.Text}");
    // 명령을 처리했다고 호출자에게 알려줍니다.
    return true;
}

// 클라이언트 닉네임을 변경하는 메서드입니다.
static async Task ChangeClientNameAsync(ClientConnection connection, string requestedName)
{
    // 닉네임이 비어 있으면 변경하지 않습니다.
    if (string.IsNullOrWhiteSpace(requestedName))
    {
        // 보낸 사람에게만 실패 이유를 알려줍니다.
        await SendToClientAsync(connection, MessageType.Notice, "Nickname cannot be empty.");
        // 메서드를 종료합니다.
        return;
    }

    // 너무 긴 닉네임은 화면을 어지럽히므로 20자로 제한합니다.
    if (requestedName.Length > 20)
    {
        // 보낸 사람에게만 실패 이유를 알려줍니다.
        await SendToClientAsync(connection, MessageType.Notice, "Nickname must be 20 characters or fewer.");
        // 메서드를 종료합니다.
        return;
    }

    // 이전 이름을 공지에 쓰기 위해 보관합니다.
    string oldName = connection.Name;
    // 연결 객체의 이름을 새 닉네임으로 바꿉니다.
    connection.Rename(requestedName);
    // 서버 콘솔에 닉네임 변경을 기록합니다.
    AppLogger.Info($"[server] {oldName} is now {connection.Name}");
    // 모든 클라이언트에게 닉네임 변경을 공지합니다.
    await BroadcastServerNoticeAsync($"{oldName} is now {connection.Name}");
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
    string chatMessage = $"{sender.Name}: {message}";

    // 복사해 둔 클라이언트 목록을 돌면서 채팅 메시지를 보냅니다.
    foreach (ClientConnection client in clients)
    {
        // 보낸 사람을 포함한 모든 접속자에게 같은 채팅 메시지를 전달합니다.
        await SendToClientAsync(client, MessageType.Chat, chatMessage);
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
    AppLogger.Info($"[server] Online clients: {GetClientCount()}");
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
    AppLogger.Info($"[server] Online clients: {GetClientCount()}");
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

// 현재 접속자 이름 목록을 가져오는 메서드입니다.
static string[] GetClientNames()
{
    // 접속자 목록을 읽는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
    lock (ServerState.Gate)
    {
        // 현재 접속자 이름만 배열로 복사해서 반환합니다.
        return ServerState.Clients
            .Select(client => client.Name)
            .OrderBy(name => name)
            .ToArray();
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
        await SendToClientAsync(client, MessageType.Notice, message);
    }
}

// 클라이언트 한 명에게 메시지를 안전하게 보내는 메서드입니다.
static async Task SendToClientAsync(ClientConnection connection, MessageType type, string message)
{
    // 네트워크 전송은 실패할 수 있으므로 예외 처리를 준비합니다.
    try
    {
        // 한 클라이언트에게 여러 작업이 동시에 쓰는 일을 막기 위해 연결 객체의 전송 메서드를 사용합니다.
        await connection.SendAsync(type, message);
    }
    // 클라이언트 연결이 이미 끊긴 경우에는 IOException이 발생할 수 있습니다.
    catch (IOException ex)
    {
        // 서버 전체가 멈추지 않도록 전송 실패만 로그로 남깁니다.
        AppLogger.Error($"[server] Failed to send to {connection.Name}: {ex.Message}");
    }
    // writer나 socket이 이미 정리된 경우에는 ObjectDisposedException이 발생할 수 있습니다.
    catch (ObjectDisposedException)
    {
        // 이미 닫힌 연결이므로 별도 복구 없이 로그만 남깁니다.
        AppLogger.Error($"[server] Failed to send to {connection.Name}: connection closed");
    }
}

// 서버가 종료될 때 현재 접속 중인 모든 클라이언트 연결을 닫는 메서드입니다.
static void CloseAllClients()
{
    // lock 안에서 오래 작업하지 않기 위해 먼저 접속자 목록 복사본을 만듭니다.
    ClientConnection[] clients;

    // 접속자 목록을 복사하는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
    lock (ServerState.Gate)
    {
        // 현재 접속자 목록을 배열로 복사합니다.
        clients = ServerState.Clients.ToArray();
        // 서버 종료 중에는 목록을 비워서 중복 정리를 줄입니다.
        ServerState.Clients.Clear();
    }

    // 복사해 둔 클라이언트 목록을 돌면서 연결을 닫습니다.
    foreach (ClientConnection client in clients)
    {
        // 각 클라이언트 소켓을 닫아 pending read/write를 깨웁니다.
        client.Close();
    }
}

// 클라이언트가 서버에서 오는 메시지를 계속 읽는 메서드입니다.
static async Task ReceiveServerMessagesAsync(NetworkStream stream, CancellationToken cancellationToken)
{
    // 네트워크 수신은 실패할 수 있으므로 예외 처리를 준비합니다.
    try
    {
        // 서버가 연결을 유지하는 동안 메시지를 계속 읽습니다.
        while (true)
        {
            // 서버에서 보낸 typed length-prefixed 메시지 하나를 비동기로 읽습니다.
            NetworkMessage? message = await MessageProtocol.ReadMessageAsync(stream, cancellationToken);
            // null은 서버가 메시지 시작 전에 연결을 종료했다는 뜻입니다.
            if (message is null)
            {
                // 메시지 읽기 반복을 종료합니다.
                break;
            }

            // 입력 프롬프트와 겹치지 않도록 한 줄 내려서 서버 메시지를 출력합니다.
            Console.WriteLine();
            // 서버에서 받은 메시지를 클라이언트 콘솔에 출력합니다.
            Console.WriteLine($"< {FormatIncomingMessage(message)}");
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
    // Ctrl+C로 클라이언트 수신 작업이 취소될 수 있습니다.
    catch (OperationCanceledException)
    {
        // 정상 종료 흐름이므로 조용히 수신 작업을 끝냅니다.
    }
}

// 서버에서 받은 typed message를 사람이 읽기 좋은 콘솔 문자열로 바꿉니다.
static string FormatIncomingMessage(NetworkMessage message)
{
    // 메시지 타입에 따라 화면 prefix를 붙입니다.
    return message.Type switch
    {
        // 채팅 메시지는 [chat] prefix로 표시합니다.
        MessageType.Chat => $"[chat] {message.Text}",
        // 공지 메시지는 [notice] prefix로 표시합니다.
        MessageType.Notice => $"[notice] {message.Text}",
        // 서버가 command를 돌려보내는 일은 보통 없지만 디버깅을 위해 표시 형식을 둡니다.
        MessageType.Command => $"[command] {message.Text}",
        // 정의되지 않은 타입은 protocol에서 걸러지지만, 혹시 모르니 fallback을 둡니다.
        _ => message.Text
    };
}
