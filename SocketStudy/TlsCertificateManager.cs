using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

public static class TlsCertificateManager
{
    public const string DevelopmentPassword = "socket-study-development";

    public static string DefaultDirectory =>
        Path.Combine(Environment.CurrentDirectory, "Data", "tls");

    public static string DefaultPfxPath =>
        Path.Combine(DefaultDirectory, "server.pfx");

    public static string DefaultCertificatePath =>
        Path.Combine(DefaultDirectory, "server.cer");

    public static TlsServerCertificateProvider CreateServerCertificateProvider(
        Action<string>? logInfo = null,
        Action<string>? logError = null)
    {
        string? configuredPfxPath =
            Environment.GetEnvironmentVariable("SOCKETSTUDY_TLS_PFX");
        bool isProduction = string.Equals(
            Environment.GetEnvironmentVariable("SOCKETSTUDY_ENVIRONMENT"),
            "Production",
            StringComparison.OrdinalIgnoreCase);
        if (isProduction && string.IsNullOrWhiteSpace(configuredPfxPath))
        {
            throw new InvalidOperationException(
                "Production requires SOCKETSTUDY_TLS_PFX and does not generate a development certificate.");
        }

        string pfxPath = configuredPfxPath ?? DefaultPfxPath;
        string password = Environment.GetEnvironmentVariable("SOCKETSTUDY_TLS_PASSWORD") ??
            DevelopmentPassword;
        if (!File.Exists(pfxPath))
        {
            if (isProduction || configuredPfxPath is not null)
            {
                throw new FileNotFoundException("Configured TLS PFX file was not found.", pfxPath);
            }

            CreateDevelopmentCertificate(pfxPath, DefaultCertificatePath, password);
            logInfo?.Invoke($"[server] Development TLS certificate created: {pfxPath}");
        }

        return new TlsServerCertificateProvider(
            pfxPath,
            password,
            WorldRules.TlsCertificateExpiryWarningThreshold,
            logInfo: logInfo,
            logError: logError);
    }

    public static TlsPinnedCertificateSet LoadPinnedServerCertificates()
    {
        string? configuredPaths =
            Environment.GetEnvironmentVariable("SOCKETSTUDY_TLS_CERT");
        string[] paths = string.IsNullOrWhiteSpace(configuredPaths)
            ? [DefaultCertificatePath]
            : configuredPaths.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new TlsPinnedCertificateSet(paths);
    }

    public static void CreateDevelopmentCertificate(
        string pfxPath,
        string certificatePath,
        string password = DevelopmentPassword)
    {
        string fullPfxPath = Path.GetFullPath(pfxPath);
        string fullCertificatePath = Path.GetFullPath(certificatePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPfxPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(fullCertificatePath)!);

        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                true));
        var enhancedKeyUsages = new OidCollection
        {
            new Oid("1.3.6.1.5.5.7.3.1")
        };
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(enhancedKeyUsages, true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName("localhost");
        subjectAlternativeNames.AddDnsName(Environment.MachineName);
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        subjectAlternativeNames.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());

        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(1));
        File.WriteAllBytes(
            fullPfxPath,
            certificate.Export(X509ContentType.Pfx, password));
        File.WriteAllBytes(
            fullCertificatePath,
            certificate.Export(X509ContentType.Cert));
    }
}
