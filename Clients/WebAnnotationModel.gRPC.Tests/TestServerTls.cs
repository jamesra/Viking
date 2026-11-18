using System;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// TLS trust for the Docker test stack, which serves gRPC only over HTTPS (never h2c) with the
    /// container's self-signed localhost certificate. The tests trust that one certificate, pinned by
    /// file, and nothing else: any other certificate error fails the handshake, as for a real server.
    /// </summary>
    internal static class TestServerTls
    {
        /// <summary>Where Start-AnnotationTestStack.ps1 saves the container's certificate.</summary>
        public static string DefaultCertificatePath =>
            Path.Combine(Path.GetTempPath(), "viking-annotation-test-stack.pem");

        /// <summary>
        /// The certificate file from <c>GrpcServer:CertificatePath</c> (or the default path), or null when
        /// none exists, in which case the server certificate must validate normally.
        /// </summary>
        public static X509Certificate2 LoadPinnedCertificate(IConfiguration config)
        {
            string path = config["GrpcServer:CertificatePath"];
            if (string.IsNullOrWhiteSpace(path))
                path = DefaultCertificatePath;
            return File.Exists(path) ? new X509Certificate2(path) : null;
        }

        /// <summary>
        /// True when the server presented the pinned certificate, or when it validated without errors.
        /// </summary>
        public static bool IsTrusted(X509Certificate2 pinned, X509Certificate presented, SslPolicyErrors errors)
        {
            if (errors == SslPolicyErrors.None)
                return true;
            return pinned is not null
                   && presented is not null
                   && string.Equals(pinned.GetCertHashString(), presented.GetCertHashString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A handler for the test stack: HTTP/2 with several connections, trusting only the pinned certificate.</summary>
        public static SocketsHttpHandler CreateHandler(IConfiguration config)
            => CreateHandler(LoadPinnedCertificate(config));

        /// <summary>
        /// The identity server's certificate, from <c>IdentityServer:CertificatePath</c> (or the default path); null when
        /// none exists, in which case identity must present a certificate that validates normally.
        /// </summary>
        public static X509Certificate2 LoadPinnedIdentityCertificate(IConfiguration config)
        {
            string path = config["IdentityServer:CertificatePath"];
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(Path.GetTempPath(), "viking-identity-test-stack.pem");
            return File.Exists(path) ? new X509Certificate2(path) : null;
        }

        /// <summary>A handler trusting only <paramref name="pinned"/> (plus certificates that validate normally).</summary>
        public static SocketsHttpHandler CreateHandler(X509Certificate2 pinned)
        {
            return new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                SslOptions =
                {
                    RemoteCertificateValidationCallback = (_, certificate, _, errors) => IsTrusted(pinned, certificate, errors)
                }
            };
        }
    }
}
