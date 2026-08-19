using System.Text.Json;

// 콘솔과 JSON Lines 파일에 구조화 로그를 남깁니다.
public static class AppLogger
{
    // 여러 작업이 동시에 파일에 쓰지 못하도록 막는 lock 객체입니다.
    private static readonly object Gate = new();

    // 로그 파일이 저장될 폴더입니다.
    private static readonly string LogDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

    // 로그 파일 경로입니다.
    private static readonly string LogFilePath = Path.Combine(LogDirectory, "socket-study.jsonl");
    public static AppLogLevel MinimumLevel { get; set; } = AppLogLevel.Information;

    public static void Debug(string message, string eventName = "application") =>
        Write(AppLogLevel.Debug, eventName, message);

    // 일반 정보 로그를 남깁니다.
    public static void Info(string message, string eventName = "application")
    {
        // info 레벨로 로그를 씁니다.
        Write(AppLogLevel.Information, eventName, message);
    }

    public static void Warning(string message, string eventName = "application") =>
        Write(AppLogLevel.Warning, eventName, message);

    // 오류 로그를 남깁니다.
    public static void Error(string message, string eventName = "application")
    {
        // error 레벨로 로그를 씁니다.
        Write(AppLogLevel.Error, eventName, message);
    }

    // 실제 로그 한 줄을 콘솔과 파일에 씁니다.
    public static void Write(
        AppLogLevel level,
        string eventName,
        string message,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (level < MinimumLevel) return;
        string line = Serialize(level, eventName, message, properties, DateTimeOffset.UtcNow);

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

    public static string Serialize(
        AppLogLevel level,
        string eventName,
        string message,
        IReadOnlyDictionary<string, object?>? properties,
        DateTimeOffset timestamp) => JsonSerializer.Serialize(new
        {
            timestamp,
            level = level.ToString(),
            @event = eventName,
            message,
            properties = properties ?? new Dictionary<string, object?>()
        });
}
