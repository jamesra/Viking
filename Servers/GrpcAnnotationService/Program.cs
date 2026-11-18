using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace gRPCAnnotationService
{
    /// <summary>
    /// Kestrel host. Docker binds only HTTPS :443; there is no plain HTTP/h2c listener (https-only-servers rule).
    /// </summary>
    public class Program
    {
        /// <summary>Builds and runs the host; blocks until shutdown.</summary>
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        /// <summary>Creates the host builder: Kestrel endpoints (Docker or launch-settings) and <see cref="Startup"/>.</summary>
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel((context, options) =>
                    {
                        // UseDockerPorts is set in the container. Local launch uses launchSettings endpoints.
                        if (context.Configuration.GetValue("Kestrel:UseDockerPorts", false))
                        {
                            // Only :443, over TLS. No plain listener, not even for tests.
                            // The shared certificate from viking-certs when mounted, else the image's
                            // self-signed dev certificate (Kestrel's default certificate settings).
                            X509Certificate2 certificate = DockerTlsCertificate.TryLoad(
                                context.Configuration["SSL_CERT_PATH"],
                                context.Configuration["SSL_KEY_PATH"]);
                            Console.WriteLine(certificate is null
                                ? "SSL_CERT_PATH/SSL_KEY_PATH not found; HTTPS uses the image's development certificate."
                                : $"HTTPS uses {context.Configuration["SSL_CERT_PATH"]} ({certificate.Subject}, expires {certificate.NotAfter:u}) with its intermediate certificates.");

                            X509Certificate2Collection intermediates = certificate is null
                                ? null
                                : DockerTlsCertificate.LoadIntermediates(context.Configuration["SSL_CERT_PATH"], certificate);

                            options.ListenAnyIP(443, listen =>
                            {
                                listen.Protocols = HttpProtocols.Http1AndHttp2;
                                if (certificate is null)
                                    listen.UseHttps();
                                else
                                    listen.UseHttps(new HttpsConnectionAdapterOptions
                                    {
                                        ServerCertificate = certificate,
                                        ServerCertificateChain = intermediates,
                                    });
                            });
                            return;
                        }

                        options.ConfigureEndpointDefaults(listen =>
                        {
                            listen.Protocols = HttpProtocols.Http1AndHttp2;
                        });
                    });
                    webBuilder.UseStartup<Startup>();
                });
    }
}
