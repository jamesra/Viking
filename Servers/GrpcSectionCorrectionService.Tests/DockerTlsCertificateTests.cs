using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
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
    }
}
