using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace gRPCAnnotationService
{
    /// <summary>
    /// Loads the PEM pair the viking-certs renewer publishes under /certs (SSL_CERT_PATH / SSL_KEY_PATH).
    /// Same approach as Viking.GrpcSectionCorrectionService.DockerTlsCertificate.
    /// </summary>
    /// <remarks>
    /// Missing or unset paths return null, so the caller keeps the image's self-signed dev certificate
    /// (ASPNETCORE_Kestrel__Certificates__Default__*) and a box without certificates still starts.
    /// </remarks>
    public static class DockerTlsCertificate
    {
        /// <summary>
        /// Returns the certificate with its private key, or null when either file is unset or absent.
        /// </summary>
        /// <param name="certPath">fullchain.pem (leaf first, then intermediates).</param>
        /// <param name="keyPath">privkey.pem matching the leaf certificate.</param>
        public static X509Certificate2 TryLoad(string certPath, string keyPath)
        {
            if (string.IsNullOrWhiteSpace(certPath) || string.IsNullOrWhiteSpace(keyPath))
                return null;
            if (!File.Exists(certPath) || !File.Exists(keyPath))
                return null;

            // Round-trip through PKCS#12: SslStream on Linux needs a persisted key, which a
            // PEM-loaded ephemeral key is not.
            using X509Certificate2 pem = X509Certificate2.CreateFromPemFile(certPath, keyPath);
            byte[] pfx = pem.Export(X509ContentType.Pfx);
            return X509CertificateLoader.LoadPkcs12(pfx, password: null);
        }
    }
}
