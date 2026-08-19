public sealed record ServerOptions(
    string EnvironmentName, int Port, string DatabasePath,
    int MaxConcurrentConnections, int MaxConnectionsPerIp,
    TimeSpan ShutdownTimeout, TimeSpan TlsHandshakeTimeout,
    string TlsPfxPath, string TlsPassword, bool AllowDevelopmentCertificate,
    AppLogLevel MinimumLogLevel)
{
    public bool IsProduction => EnvironmentName.Equals("Production", StringComparison.OrdinalIgnoreCase);

    public static ServerOptions Load(int commandLinePort)
    {
        string environment = Read("SOCKETSTUDY_ENVIRONMENT") ?? "Development";
        string? configuredPfx = Read("SOCKETSTUDY_TLS_PFX");
        return new ServerOptions(
            environment,
            ReadInt("SOCKETSTUDY_PORT", commandLinePort),
            Path.GetFullPath(Read("SOCKETSTUDY_DATABASE") ?? Path.Combine(Environment.CurrentDirectory, "Data", "characters.db")),
            ReadInt("SOCKETSTUDY_MAX_CONNECTIONS", WorldRules.MaxConcurrentConnections),
            ReadInt("SOCKETSTUDY_MAX_CONNECTIONS_PER_IP", WorldRules.MaxConnectionsPerIp),
            TimeSpan.FromSeconds(ReadInt("SOCKETSTUDY_SHUTDOWN_SECONDS", 10)),
            TimeSpan.FromSeconds(ReadInt("SOCKETSTUDY_TLS_HANDSHAKE_SECONDS", 10)),
            Path.GetFullPath(configuredPfx ?? TlsCertificateManager.DefaultPfxPath),
            Read("SOCKETSTUDY_TLS_PASSWORD") ?? TlsCertificateManager.DevelopmentPassword,
            !environment.Equals("Production", StringComparison.OrdinalIgnoreCase) && configuredPfx is null,
            ReadLogLevel());
    }

    public string[] Validate()
    {
        var errors = new List<string>();
        if (Port is < 1 or > 65535) errors.Add("Port must be between 1 and 65535.");
        if (MaxConcurrentConnections <= 0) errors.Add("Max connections must be positive.");
        if (MaxConnectionsPerIp <= 0 || MaxConnectionsPerIp > MaxConcurrentConnections)
            errors.Add("Per-IP connections must be positive and not exceed the global limit.");
        if (ShutdownTimeout <= TimeSpan.Zero) errors.Add("Shutdown timeout must be positive.");
        if (TlsHandshakeTimeout <= TimeSpan.Zero) errors.Add("TLS handshake timeout must be positive.");
        if (IsProduction && AllowDevelopmentCertificate) errors.Add("Production cannot generate a development certificate.");
        if (!AllowDevelopmentCertificate && !File.Exists(TlsPfxPath)) errors.Add($"TLS PFX file was not found: {TlsPfxPath}");
        return errors.ToArray();
    }

    public string ToSafeSummary() =>
        $"environment={EnvironmentName}, port={Port}, database={DatabasePath}, max-connections={MaxConcurrentConnections}, " +
        $"max-per-ip={MaxConnectionsPerIp}, shutdown={ShutdownTimeout.TotalSeconds}s, " +
        $"tls-handshake={TlsHandshakeTimeout.TotalSeconds}s, tls-pfx={TlsPfxPath}";

    private static string? Read(string name) =>
        Environment.GetEnvironmentVariable(name)?.Trim() is { Length: > 0 } value ? value : null;

    private static int ReadInt(string name, int defaultValue)
    {
        string? value = Read(name);
        if (value is null) return defaultValue;
        return int.TryParse(value, out int parsed) ? parsed :
            throw new InvalidOperationException($"{name} must be an integer.");
    }

    private static AppLogLevel ReadLogLevel()
    {
        string value = Read("SOCKETSTUDY_LOG_LEVEL") ?? "Information";
        return Enum.TryParse(value, true, out AppLogLevel level) ? level :
            throw new InvalidOperationException("SOCKETSTUDY_LOG_LEVEL must be Debug, Information, Warning, or Error.");
    }
}
