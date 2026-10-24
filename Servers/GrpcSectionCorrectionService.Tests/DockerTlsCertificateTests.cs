using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Viking.GrpcSectionCorrectionService;

namespace GrpcSectionCorrectionService.Tests
{
    [TestClass]
    public class DockerTlsCertificateTests
    {
        [TestMethod]
        public void TryLoad_UnsetOrMissingFiles_ReturnsNull()
        {
            Assert.IsNull(DockerTlsCertificate.TryLoad(null, null));
            Assert.IsNull(DockerTlsCertificate.TryLoad("", "key.pem"));
            string missing = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N") + ".pem");
            Assert.IsNull(DockerTlsCertificate.TryLoad(missing, missing));
        }

        [TestMethod]
        public void TryLoad_PemPair_ReturnsCertificateWithPrivateKey()
        {
            string dir = Path.Combine(Path.GetTempPath(), "tls-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string certPath = Path.Combine(dir, "fullchain.pem");
            string keyPath = Path.Combine(dir, "privkey.pem");
            try
            {
                using RSA rsa = RSA.Create(2048);
                var request = new CertificateRequest("CN=sectioncorrection.test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using X509Certificate2 issued = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
                File.WriteAllText(certPath, issued.ExportCertificatePem());
                File.WriteAllText(keyPath, rsa.ExportPkcs8PrivateKeyPem());

                X509Certificate2 loaded = DockerTlsCertificate.TryLoad(certPath, keyPath);
                Assert.IsNotNull(loaded);
                Assert.IsTrue(loaded.HasPrivateKey);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// A fullchain.pem of leaf + intermediate must hand the intermediate to Kestrel, and the TLS context
        /// Kestrel builds from it (SslStreamCertificateContext, as HttpsConnectionMiddleware does) must carry the
        /// intermediate even though the root is not trusted on this machine. Production sent the leaf alone,
        /// which Grpc.Core clients reject with "Failed to pick subchannel".
        /// </summary>
        [TestMethod]
        public void LoadIntermediates_FullChain_ReturnsIntermediateForKestrelToSend()
        {
            string dir = Path.Combine(Path.GetTempPath(), "tls-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string certPath = Path.Combine(dir, "fullchain.pem");
            string keyPath = Path.Combine(dir, "privkey.pem");
            try
            {
                DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-1);
                DateTimeOffset to = DateTimeOffset.UtcNow.AddDays(2);

                using RSA rootKey = RSA.Create(2048);
                using X509Certificate2 root = CreateCa("CN=Test Root", rootKey, issuer: null, from, to);
                using RSA intermediateKey = RSA.Create(2048);
                using X509Certificate2 intermediate = CreateCa("CN=Test Intermediate", intermediateKey, root, from, to);

                using RSA leafKey = RSA.Create(2048);
                var leafRequest = new CertificateRequest("CN=sectioncorrection.test", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
                using X509Certificate2 leaf = leafRequest.Create(intermediate, from, to, Guid.NewGuid().ToByteArray());

                File.WriteAllText(certPath, leaf.ExportCertificatePem() + "\n" + intermediate.ExportCertificatePem() + "\n");
                File.WriteAllText(keyPath, leafKey.ExportPkcs8PrivateKeyPem());

                X509Certificate2 loaded = DockerTlsCertificate.TryLoad(certPath, keyPath);
                X509Certificate2Collection intermediates = DockerTlsCertificate.LoadIntermediates(certPath, loaded);

                Assert.AreEqual(1, intermediates.Count);
                Assert.AreEqual(intermediate.Thumbprint, intermediates[0].Thumbprint);

                SslStreamCertificateContext context = SslStreamCertificateContext.Create(loaded, intermediates, offline: true);
                CollectionAssert.Contains(
                    context.IntermediateCertificates.Select(c => c.Thumbprint).ToList(),
                    intermediate.Thumbprint);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void LoadIntermediates_LeafOnly_ReturnsEmpty()
        {
            string dir = Path.Combine(Path.GetTempPath(), "tls-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string certPath = Path.Combine(dir, "fullchain.pem");
            string keyPath = Path.Combine(dir, "privkey.pem");
            try
            {
                using RSA rsa = RSA.Create(2048);
                var request = new CertificateRequest("CN=sectioncorrection.test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using X509Certificate2 issued = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
                File.WriteAllText(certPath, issued.ExportCertificatePem());
                File.WriteAllText(keyPath, rsa.ExportPkcs8PrivateKeyPem());

                X509Certificate2 loaded = DockerTlsCertificate.TryLoad(certPath, keyPath);
                Assert.AreEqual(0, DockerTlsCertificate.LoadIntermediates(certPath, loaded).Count);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        static X509Certificate2 CreateCa(string subject, RSA key, X509Certificate2 issuer, DateTimeOffset from, DateTimeOffset to)
        {
            var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            if (issuer is null)
                return request.CreateSelfSigned(from, to);

            using X509Certificate2 publicOnly = request.Create(issuer, from, to, Guid.NewGuid().ToByteArray());
            return publicOnly.CopyWithPrivateKey(key);
        }
    }
}
