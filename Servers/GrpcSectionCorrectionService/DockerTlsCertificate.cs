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
        /// <summary>
        /// Returns the leaf certificate with its private key, or null when either file is unset or absent.
        /// Pass the result and <see cref="LoadIntermediates"/> to Kestrel together; the leaf alone is not enough.
        /// </summary>
        /// <param name="certPath">fullchain.pem (leaf first, then intermediates).</param>
        /// <param name="keyPath">privkey.pem matching the leaf certificate.</param>
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

        /// <summary>
        /// The certificates in <paramref name="certPath"/> other than <paramref name="leaf"/>, for
        /// Kestrel's <c>HttpsConnectionAdapterOptions.ServerCertificateChain</c>. Empty when the file holds
        /// only the leaf.
        /// </summary>
        /// <remarks>
        /// <see cref="X509Certificate2.CreateFromPemFile(string, string)"/> keeps only the first certificate,
        /// so without this Kestrel rebuilt the chain from the container's own stores, could not, and sent the
        /// leaf alone. Browsers and Windows fetch a missing intermediate from the leaf's AIA URL; Grpc.Core in
        /// Viking does not, and every call failed with "Failed to pick subchannel".
        /// </remarks>
        /// <param name="certPath">The same fullchain.pem passed to <see cref="TryLoad"/>.</param>
        /// <param name="leaf">The certificate <see cref="TryLoad"/> returned.</param>
        public static X509Certificate2Collection LoadIntermediates(string certPath, X509Certificate2 leaf)
        {
            X509Certificate2Collection all = new();
            all.ImportFromPemFile(certPath);

            X509Certificate2Collection intermediates = new();
            foreach (X509Certificate2 certificate in all)
            {
                if (certificate.Thumbprint != leaf.Thumbprint)
                    intermediates.Add(certificate);
            }

            return intermediates;
        }
    }
}
