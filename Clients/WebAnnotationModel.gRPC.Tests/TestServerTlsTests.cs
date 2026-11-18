using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NUnit.Framework;

namespace WebAnnotationModel.gRPC.Tests
{
    [TestFixture]
    public class TestServerTlsTests
    {
        private static X509Certificate2 MakeCertificate(string name)
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return request.CreateSelfSigned(System.DateTimeOffset.UtcNow.AddDays(-1), System.DateTimeOffset.UtcNow.AddDays(1));
        }

        [Test]
        public void ValidatedCertificateIsTrustedWithoutPinning()
        {
            Assert.That(TestServerTls.IsTrusted(null, null, SslPolicyErrors.None), Is.True);
        }

        [Test]
        public void SelfSignedCertificateIsRejectedWhenNothingIsPinned()
        {
            using var presented = MakeCertificate("a");
            Assert.That(TestServerTls.IsTrusted(null, presented, SslPolicyErrors.RemoteCertificateChainErrors), Is.False);
        }

        [Test]
        public void PinnedCertificateIsTrustedDespiteChainErrors()
        {
            using var pinned = MakeCertificate("a");
            Assert.That(TestServerTls.IsTrusted(pinned, pinned, SslPolicyErrors.RemoteCertificateChainErrors), Is.True);
        }

        [Test]
        public void AnotherCertificateIsRejectedEvenWhenOneIsPinned()
        {
            using var pinned = MakeCertificate("a");
            using var other = MakeCertificate("b");
            Assert.That(TestServerTls.IsTrusted(pinned, other, SslPolicyErrors.RemoteCertificateChainErrors), Is.False);
        }
    }
}
