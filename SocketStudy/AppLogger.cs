// 콘솔과 파일에 동시에 로그를 남기는 간단한 logger입니다.
static class AppLogger
{
    // 여러 작업이 동시에 파일에 쓰지 못하도록 막는 lock 객체입니다.
    private static readonly object Gate = new();

    // 로그 파일이 저장될 폴더입니다.
    private static readonly string LogDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

    // 로그 파일 경로입니다.
    private static readonly string LogFilePath = Path.Combine(LogDirectory, "socket-study.log");

    // 일반 정보 로그를 남깁니다.
    public static void Info(string message)
    {
        // info 레벨로 로그를 씁니다.
        Write("info", message);
    }

    // 오류 로그를 남깁니다.
    public static void Error(string message)
    {
        // error 레벨로 로그를 씁니다.
        Write("error", message);
    }

    // 실제 로그 한 줄을 콘솔과 파일에 씁니다.
    private static void Write(string level, string message)
    {
        // 시간, 레벨, 메시지를 한 줄로 조합합니다.
        string line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}";

        // 콘솔에는 기존 메시지 스타일을 살리기 위해 message만 출력합니다.
        Console.WriteLine(message);

        // 파일 쓰기는 동시에 여러 작업이 들어올 수 있으므로 lock으로 보호합니다.
        lock (Gate)
        {
            // 로그 폴더가 없으면 만듭니다.
            Directory.CreateDirectory(LogDirectory);
            // 로그 파일 끝에 한 줄을 추가합니다.
            File.AppendAllText(LogFilePath, line + Environment.NewLine);
        }
    }
}
