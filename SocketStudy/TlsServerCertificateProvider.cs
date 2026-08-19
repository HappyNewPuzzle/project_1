using System.Security.Cryptography.X509Certificates;

public sealed class TlsServerCertificateProvider : IDisposable
{
    private readonly object gate = new();
    private readonly string pfxPath;
    private readonly string password;
    private readonly TimeSpan expiryWarningThreshold;
    private readonly Func<DateTimeOffset> getCurrentTime;
    private readonly Action<string> logInfo;
    private readonly Action<string> logError;
    private readonly List<X509Certificate2> retiredCertificates = new();
    private X509Certificate2 currentCertificate;
    private DateTime lastWriteTimeUtc;
    private long fileLength;
    private DateTimeOffset? lastExpiryWarningAt;

    public TlsServerCertificateProvider(
        string pfxPath,
        string password,
        TimeSpan expiryWarningThreshold,
        Func<DateTimeOffset>? getCurrentTime = null,
        Action<string>? logInfo = null,
        Action<string>? logError = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pfxPath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            expiryWarningThreshold,
            TimeSpan.Zero);

        this.pfxPath = Path.GetFullPath(pfxPath);
        this.password = password;
        this.expiryWarningThreshold = expiryWarningThreshold;
        this.getCurrentTime = getCurrentTime ?? (() => DateTimeOffset.UtcNow);
        this.logInfo = logInfo ?? (_ => { });
        this.logError = logError ?? (_ => { });
        currentCertificate = LoadAndValidate();
        UpdateFileIdentity();
        InspectExpiry(currentCertificate);
    }

    public X509Certificate2 Current
    {
        get
        {
            lock (gate)
            {
                return currentCertificate;
            }
        }
    }

    public string Thumbprint
    {
        get
        {
            lock (gate)
            {
                return currentCertificate.Thumbprint;
            }
        }
    }

    public DateTimeOffset ExpiresAt
    {
        get { lock (gate) { return currentCertificate.NotAfter.ToUniversalTime(); } }
    }

    public bool RefreshIfChanged()
    {
        var file = new FileInfo(pfxPath);
        if (!file.Exists)
        {
            logError($"[server] TLS certificate file is missing: {pfxPath}");
            return false;
        }

        lock (gate)
        {
            if (file.LastWriteTimeUtc == lastWriteTimeUtc &&
                file.Length == fileLength)
            {
                InspectExpiry(currentCertificate);
                return false;
            }

            X509Certificate2 replacement;
            try
            {
                replacement = LoadAndValidate();
            }
            catch (Exception ex)
            {
                logError($"[server] TLS certificate reload failed: {ex.Message}");
                return false;
            }

            if (replacement.Thumbprint == currentCertificate.Thumbprint)
            {
                replacement.Dispose();
                UpdateFileIdentity();
                InspectExpiry(currentCertificate);
                return false;
            }

            retiredCertificates.Add(currentCertificate);
            currentCertificate = replacement;
            lastExpiryWarningAt = null;
            UpdateFileIdentity();
            InspectExpiry(currentCertificate);
            logInfo(
                $"[server] TLS certificate rotated. Thumbprint: {currentCertificate.Thumbprint}");
            return true;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            currentCertificate.Dispose();
            foreach (X509Certificate2 certificate in retiredCertificates)
            {
                certificate.Dispose();
            }

            retiredCertificates.Clear();
        }
    }

    private X509Certificate2 LoadAndValidate()
    {
        var certificate = new X509Certificate2(
            pfxPath,
            password,
            X509KeyStorageFlags.DefaultKeySet);
        DateTimeOffset now = getCurrentTime();
        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new InvalidDataException("TLS certificate does not contain a private key.");
        }

        if (certificate.NotBefore.ToUniversalTime() > now.UtcDateTime ||
            certificate.NotAfter.ToUniversalTime() <= now.UtcDateTime)
        {
            certificate.Dispose();
            throw new InvalidDataException("TLS certificate is not currently valid.");
        }

        return certificate;
    }

    private void InspectExpiry(X509Certificate2 certificate)
    {
        TimeSpan remaining = certificate.NotAfter.ToUniversalTime() -
            getCurrentTime().UtcDateTime;
        DateTimeOffset now = getCurrentTime();
        if (remaining <= expiryWarningThreshold &&
            (lastExpiryWarningAt is null ||
                now - lastExpiryWarningAt >= TimeSpan.FromDays(1)))
        {
            lastExpiryWarningAt = now;
            logError(
                $"[server] TLS certificate expires in {Math.Max(0, remaining.TotalDays):F1} days at {certificate.NotAfter.ToUniversalTime():O}.");
        }
    }

    private void UpdateFileIdentity()
    {
        var file = new FileInfo(pfxPath);
        lastWriteTimeUtc = file.LastWriteTimeUtc;
        fileLength = file.Length;
    }
}
