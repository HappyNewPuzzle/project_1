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

    public static X509Certificate2 LoadOrCreateServerCertificate()
    {
        string pfxPath = Environment.GetEnvironmentVariable("SOCKETSTUDY_TLS_PFX") ??
            DefaultPfxPath;
        string password = Environment.GetEnvironmentVariable("SOCKETSTUDY_TLS_PASSWORD") ??
            DevelopmentPassword;
        if (!File.Exists(pfxPath))
        {
            if (Environment.GetEnvironmentVariable("SOCKETSTUDY_TLS_PFX") is not null)
            {
                throw new FileNotFoundException("Configured TLS PFX file was not found.", pfxPath);
            }

            CreateDevelopmentCertificate(
                pfxPath,
                DefaultCertificatePath,
                password);
        }

        return new X509Certificate2(
            pfxPath,
            password,
            X509KeyStorageFlags.DefaultKeySet);
    }

    public static X509Certificate2 LoadPinnedServerCertificate()
    {
        string certificatePath =
            Environment.GetEnvironmentVariable("SOCKETSTUDY_TLS_CERT") ??
            DefaultCertificatePath;
        if (!File.Exists(certificatePath))
        {
            throw new FileNotFoundException(
                "Pinned server certificate was not found. Start the local server first or set SOCKETSTUDY_TLS_CERT.",
                certificatePath);
        }

        return new X509Certificate2(certificatePath);
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
