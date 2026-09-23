using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// Loads the PEM pair written by the certbot sidecar (SSL_CERT_PATH / SSL_KEY_PATH).
    /// Called from Kestrel startup. Missing files return null so the process keeps the
    /// cleartext listener and does not block until the first enrollment restart.
    /// </summary>
    public static class DockerTlsCertificate
    {
        public static X509Certificate2 TryLoad(string certPath, string keyPath)
        {
            if (string.IsNullOrWhiteSpace(certPath) || string.IsNullOrWhiteSpace(keyPath))
                return null;
            if (!File.Exists(certPath) || !File.Exists(keyPath))
                return null;

            using X509Certificate2 pem = X509Certificate2.CreateFromPemFile(certPath, keyPath);
            byte[] pfx = pem.Export(X509ContentType.Pfx);
            return X509CertificateLoader.LoadPkcs12(pfx, password: null);
        }
    }
}
