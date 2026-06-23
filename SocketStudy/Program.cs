using System.Net.Sockets;

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
    CommandLineOptions.PrintUsage();
    // 더 진행하지 않고 프로그램을 종료합니다.
    return;
}

// 첫 번째 실행 인자를 소문자로 바꿔서 server/client 명령을 구분합니다.
switch (args[0].ToLowerInvariant())
{
    // 사용자가 server를 입력하면 TCP 서버 모드로 실행합니다.
    case "server":
        // server 모드의 실행 인자에서 포트 번호를 읽습니다.
        if (!CommandLineOptions.TryReadServerPort(args, out int serverPort))
        {
            // 포트 번호가 올바르지 않으면 사용법을 다시 보여줍니다.
            CommandLineOptions.PrintUsage();
            // 잘못된 설정으로 서버를 실행하지 않고 종료합니다.
            return;
        }

        // 채팅 서버 객체를 만들고 실행합니다.
        var server = new ChatServer();
        // 사용자가 지정한 포트 또는 기본 포트로 서버를 시작합니다.
        await server.RunAsync(serverPort, appCancellation.Token);
        // switch 문을 빠져나갑니다.
        break;

    // 사용자가 client를 입력하면 TCP 클라이언트 모드로 실행합니다.
    case "client":
        // client 모드의 실행 인자에서 host, port, nickname을 읽습니다.
        if (!CommandLineOptions.TryReadClientOptions(args, out string host, out int clientPort, out string? nickname))
        {
            // 실행 인자가 올바르지 않으면 사용법을 다시 보여줍니다.
            CommandLineOptions.PrintUsage();
            // 잘못된 설정으로 클라이언트를 실행하지 않고 종료합니다.
            return;
        }

        // 지정한 서버 host:port에 접속합니다.
        await RunClientAsync(host, clientPort, nickname, appCancellation.Token);
        // switch 문을 빠져나갑니다.
        break;

    // server/client가 아닌 값이 들어오면 사용법을 다시 보여줍니다.
    default:
        // 올바른 실행 예시를 출력합니다.
        CommandLineOptions.PrintUsage();
        // switch 문을 빠져나갑니다.
        break;
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
