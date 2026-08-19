public sealed class ServerHealthService
{
    private readonly Func<ServerLifecycleState> getLifecycle;
    private readonly Func<DateTimeOffset> getCertificateExpiry;
    private readonly string databasePath;
    private readonly Func<DateTimeOffset> getCurrentTime;

    public ServerHealthService(Func<ServerLifecycleState> getLifecycle,
        Func<DateTimeOffset> getCertificateExpiry, string databasePath,
        Func<DateTimeOffset>? getCurrentTime = null)
    {
        this.getLifecycle = getLifecycle;
        this.getCertificateExpiry = getCertificateExpiry;
        this.databasePath = databasePath;
        this.getCurrentTime = getCurrentTime ?? (() => DateTimeOffset.UtcNow);
    }

    public ServerHealthReport Check()
    {
        ServerLifecycleState state = getLifecycle();
        bool live = state != ServerLifecycleState.Stopped;
        var reasons = new List<string>();
        if (state != ServerLifecycleState.Running) reasons.Add($"lifecycle={state}");
        string? directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (directory is null || !Directory.Exists(directory)) reasons.Add("database-directory-unavailable");
        if (getCertificateExpiry() <= getCurrentTime()) reasons.Add("tls-certificate-expired");
        return new(live, live && reasons.Count == 0, reasons.ToArray());
    }
}
