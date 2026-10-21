using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace gRPCAnnotationService
{
    /// <summary>
    /// Kestrel host. Docker binds h2c :80 and HTTPS :443 separately — gRPC clients do not follow redirects.
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
                            // :80 stays h2c-only for gRPC. :443 serves gRPC over HTTPS.
                            options.ListenAnyIP(80, listen => listen.Protocols = HttpProtocols.Http2);

                            // The shared certificate from viking-certs when mounted, else the image's
                            // self-signed dev certificate (Kestrel's default certificate settings).
                            X509Certificate2 certificate = DockerTlsCertificate.TryLoad(
                                context.Configuration["SSL_CERT_PATH"],
                                context.Configuration["SSL_KEY_PATH"]);
                            Console.WriteLine(certificate is null
                                ? "SSL_CERT_PATH/SSL_KEY_PATH not found; HTTPS uses the image's development certificate."
                                : $"HTTPS uses {context.Configuration["SSL_CERT_PATH"]} ({certificate.Subject}, expires {certificate.NotAfter:u}).");

                            options.ListenAnyIP(443, listen =>
                            {
                                listen.Protocols = HttpProtocols.Http1AndHttp2;
                                if (certificate is null)
                                    listen.UseHttps();
                                else
                                    listen.UseHttps(certificate);
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
