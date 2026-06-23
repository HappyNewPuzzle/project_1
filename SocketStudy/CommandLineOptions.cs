// 콘솔 실행 인자를 해석하는 helper입니다.
public static class CommandLineOptions
{
    // 사용자가 포트를 따로 지정하지 않았을 때 사용할 기본 TCP 포트입니다.
    public const int DefaultPort = 5000;

    // 잘못 실행했을 때 보여줄 사용법 출력 메서드입니다.
    public static void PrintUsage()
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
        // 원격 서버 접속 명령을 출력합니다.
        Console.WriteLine("  dotnet run -- client [host] [port] [nickname]");
        // 포트를 생략했을 때 사용할 기본값을 출력합니다.
        Console.WriteLine();
        // 기본 포트 번호를 안내합니다.
        Console.WriteLine($"Default port: {DefaultPort}");
    }

    // server 모드 실행 인자에서 포트 번호를 읽는 메서드입니다.
    public static bool TryReadServerPort(string[] args, out int port)
    {
        // 먼저 기본 포트를 넣어 두면, 사용자가 포트를 생략한 경우 그대로 사용할 수 있습니다.
        port = DefaultPort;

        // 두 번째 실행 인자가 없으면 포트를 생략한 것이므로 기본 포트를 사용합니다.
        if (args.Length < 2)
        {
            // 포트 읽기에 성공한 것으로 처리합니다.
            return true;
        }

        // 두 번째 실행 인자를 포트 번호로 해석합니다.
        return TryParsePort(args[1], out port);
    }

    // client 모드 실행 인자에서 host, port, nickname을 읽는 메서드입니다.
    public static bool TryReadClientOptions(string[] args, out string host, out int port, out string? nickname)
    {
        // 기본 host는 같은 PC를 의미하는 127.0.0.1입니다.
        host = "127.0.0.1";
        // 기본 포트를 먼저 넣어 둡니다.
        port = DefaultPort;
        // 기본 닉네임은 없습니다.
        nickname = null;

        // client만 입력했다면 모든 기본값을 사용합니다.
        if (args.Length == 1)
        {
            // 옵션 읽기에 성공한 것으로 처리합니다.
            return true;
        }

        // 두 번째 인자가 숫자라면 기존 방식인 client [port] [nickname]으로 해석합니다.
        if (int.TryParse(args[1], out _))
        {
            // 숫자처럼 생긴 값은 반드시 올바른 포트 범위 안에 있어야 합니다.
            if (!TryParsePort(args[1], out int parsedPort))
            {
                // 옵션 읽기에 실패했다고 호출자에게 알려줍니다.
                return false;
            }

            // 포트 번호를 저장합니다.
            port = parsedPort;
            // 세 번째 인자가 있으면 닉네임으로 사용합니다.
            nickname = args.Length >= 3 ? args[2].Trim() : null;
            // 옵션 읽기에 성공한 것으로 처리합니다.
            return true;
        }

        // 두 번째 인자가 숫자가 아니라면 client [host] [port] [nickname] 형식으로 해석합니다.
        host = args[1].Trim();
        // host가 비어 있으면 잘못된 값입니다.
        if (string.IsNullOrWhiteSpace(host))
        {
            // 어떤 값이 잘못되었는지 콘솔에 출력합니다.
            Console.WriteLine("Invalid host.");
            // 옵션 읽기에 실패했다고 호출자에게 알려줍니다.
            return false;
        }

        // host를 직접 지정한 경우 세 번째 인자에는 포트가 와야 합니다.
        if (args.Length < 3)
        {
            // 어떤 값이 빠졌는지 콘솔에 출력합니다.
            Console.WriteLine("Missing port.");
            // 옵션 읽기에 실패했다고 호출자에게 알려줍니다.
            return false;
        }

        // 세 번째 인자를 포트로 해석합니다.
        if (!TryParsePort(args[2], out port))
        {
            // 포트 읽기에 실패했다고 호출자에게 알려줍니다.
            return false;
        }

        // 네 번째 인자가 있으면 닉네임으로 사용합니다.
        nickname = args.Length >= 4 ? args[3].Trim() : null;
        // 옵션 읽기에 성공한 것으로 처리합니다.
        return true;
    }

    // 문자열 포트 값을 검증하고 int로 변환하는 메서드입니다.
    private static bool TryParsePort(string value, out int port)
    {
        // 기본값을 넣어 둡니다.
        port = DefaultPort;

        // 문자열로 들어온 포트 값을 int로 바꿔 봅니다.
        bool parsed = int.TryParse(value, out int parsedPort);
        // 파싱에 실패했거나 TCP 포트 범위인 1~65535를 벗어나면 잘못된 값입니다.
        if (!parsed || parsedPort < 1 || parsedPort > 65535)
        {
            // 어떤 값이 잘못되었는지 콘솔에 출력합니다.
            Console.WriteLine($"Invalid port: {value}");
            // 포트 읽기에 실패했다고 호출자에게 알려줍니다.
            return false;
        }

        // 검증을 통과한 포트 값을 실제로 사용할 포트 변수에 넣습니다.
        port = parsedPort;
        // 포트 읽기에 성공했다고 호출자에게 알려줍니다.
        return true;
    }
}
