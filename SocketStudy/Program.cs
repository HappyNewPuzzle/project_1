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

        // 채팅 클라이언트 객체를 만들고 실행합니다.
        var client = new ChatClient();
        // 지정한 서버 host:port에 접속합니다.
        await client.RunAsync(host, clientPort, nickname, appCancellation.Token);
        // switch 문을 빠져나갑니다.
        break;

    // server/client가 아닌 값이 들어오면 사용법을 다시 보여줍니다.
    default:
        // 올바른 실행 예시를 출력합니다.
        CommandLineOptions.PrintUsage();
        // switch 문을 빠져나갑니다.
        break;
}
