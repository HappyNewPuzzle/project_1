using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

public static class TlsCertificateValidator
{
    public static bool ValidatePinnedServer(
        X509Certificate? presentedCertificate,
        SslPolicyErrors policyErrors,
        X509Certificate2 pinnedCertificate)
    {
        if (presentedCertificate is null ||
            policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch) ||
            policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
        {
            return false;
        }

        byte[] presented = presentedCertificate.GetRawCertData();
        byte[] pinned = pinnedCertificate.RawData;
        return presented.Length == pinned.Length &&
            CryptographicOperations.FixedTimeEquals(presented, pinned);
    }
}
