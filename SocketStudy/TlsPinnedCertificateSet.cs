using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

public sealed class TlsPinnedCertificateSet : IDisposable
{
    private readonly X509Certificate2[] certificates;

    public TlsPinnedCertificateSet(IEnumerable<string> certificatePaths)
    {
        certificates = certificatePaths
            .Select(Path.GetFullPath)
            .Select(path => File.Exists(path)
                ? new X509Certificate2(path)
                : throw new FileNotFoundException(
                    "Pinned server certificate was not found.",
                    path))
            .ToArray();
        if (certificates.Length == 0)
        {
            throw new ArgumentException("At least one pinned certificate is required.");
        }
    }

    public bool Validate(
        X509Certificate? presentedCertificate,
        SslPolicyErrors policyErrors)
    {
        return certificates.Any(certificate =>
            TlsCertificateValidator.ValidatePinnedServer(
                presentedCertificate,
                policyErrors,
                certificate));
    }

    public void Dispose()
    {
        foreach (X509Certificate2 certificate in certificates)
        {
            certificate.Dispose();
        }
    }
}
