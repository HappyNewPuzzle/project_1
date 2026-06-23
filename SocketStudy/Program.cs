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

// 첫 번째 실행 인자를 소문자로 바꿔서 server/client 명령을 구분합니다.
switch (args[0].ToLowerInvariant())
{
    // 사용자가 server를 입력하면 TCP 서버 모드로 실행합니다.
    case "server":
        // 기본 포트로 서버를 시작하고, 서버 작업이 끝날 때까지 기다립니다.
        await RunServerAsync(DefaultPort);
        // switch 문을 빠져나갑니다.
        break;

    // 사용자가 client를 입력하면 TCP 클라이언트 모드로 실행합니다.
    case "client":
        // 로컬 PC의 서버 127.0.0.1:5000에 접속합니다.
        await RunClientAsync("127.0.0.1", DefaultPort);
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
    Console.WriteLine("[server] Open another terminal and run: dotnet run -- client");

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
    // 클라이언트가 접속했다는 사실을 서버 콘솔에 출력합니다.
    Console.WriteLine($"[server] Client connected: {remoteEndPoint}");

    // TcpClient에서 실제 데이터를 읽고 쓰는 NetworkStream을 가져옵니다.
    await using NetworkStream stream = client.GetStream();
    // stream에서 UTF-8 문자열을 한 줄씩 읽기 위한 reader입니다.
    using var reader = new StreamReader(stream, Encoding.UTF8);
    // stream에 UTF-8 문자열을 한 줄씩 쓰기 위한 writer입니다.
    await using var writer = new StreamWriter(stream, Encoding.UTF8)
    {
        // WriteLineAsync를 호출할 때마다 버퍼를 바로 비워서 상대방에게 즉시 전송합니다.
        AutoFlush = true
    };

    // 네트워크 연결은 중간에 끊길 수 있으므로 예외 처리를 준비합니다.
    try
    {
        // 클라이언트가 연결을 유지하는 동안 메시지를 계속 읽습니다.
        while (true)
        {
            // 클라이언트가 보낸 문자열 한 줄을 비동기로 읽습니다.
            string? message = await reader.ReadLineAsync();
            // null은 상대방이 연결을 종료했거나 더 이상 읽을 데이터가 없다는 뜻입니다.
            if (message is null)
            {
                // 메시지 읽기 반복을 종료합니다.
                break;
            }

            // 서버 콘솔에 받은 메시지를 기록합니다.
            Console.WriteLine($"[server] Received: {message}");
            // 받은 메시지 앞에 echo:를 붙여 클라이언트에게 다시 보냅니다.
            await writer.WriteLineAsync($"echo: {message}");
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
        // 클라이언트 소켓을 닫아서 운영체제 리소스를 반납합니다.
        client.Close();
        // 클라이언트 연결이 종료되었다는 사실을 서버 콘솔에 출력합니다.
        Console.WriteLine($"[server] Client disconnected: {remoteEndPoint}");
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
    // 서버 응답을 UTF-8 문자열 한 줄 단위로 읽기 위한 reader입니다.
    using var reader = new StreamReader(stream, Encoding.UTF8);
    // 서버로 UTF-8 문자열 한 줄을 보내기 위한 writer입니다.
    await using var writer = new StreamWriter(stream, Encoding.UTF8)
    {
        // 사용자가 입력한 메시지를 즉시 서버로 보내기 위해 자동 flush를 켭니다.
        AutoFlush = true
    };

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

        // 사용자가 입력한 메시지를 서버로 보냅니다.
        await writer.WriteLineAsync(input);

        // 서버가 돌려준 응답 한 줄을 기다립니다.
        string? response = await reader.ReadLineAsync();
        // 서버 응답을 클라이언트 콘솔에 출력합니다.
        Console.WriteLine($"< {response}");
    }
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
    Console.WriteLine("  dotnet run -- server");
    // 클라이언트 실행 명령을 출력합니다.
    Console.WriteLine("  dotnet run -- client");
}
