using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// Kestrel host. Docker binds h2c :80 and HTTPS :443 separately — gRPC clients do not follow redirects.
    /// <c>menu</c> as the first argument runs <see cref="CorrectionMenu"/> instead.
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            // "menu" runs the console client of the already-running service instead of starting a second host.
            if (args.Length > 0 && args[0].Equals("menu", StringComparison.OrdinalIgnoreCase))
            {
                Environment.Exit(CorrectionMenu.RunConsoleAsync(args[1..]).GetAwaiter().GetResult());
                return;
            }

            // Release builds strip Trace/Debug; Console keeps the version visible without a debugger.
            Console.WriteLine($"GrpcSectionCorrectionService starting, service_version={RebuildStatusStore.ServiceVersion}");
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel((context, options) =>
                    {
                        if (context.Configuration.GetValue("Kestrel:UseDockerPorts", false))
                        {
                            options.ListenAnyIP(80, listen => listen.Protocols = HttpProtocols.Http2);
                            X509Certificate2 certificate = DockerTlsCertificate.TryLoad(
                                context.Configuration["SSL_CERT_PATH"],
                                context.Configuration["SSL_KEY_PATH"]);
                            if (certificate is null)
                            {
                                Console.WriteLine(
                                    "SSL certificate files not found; serving cleartext on port 80 until certbot enrolls.");
                                return;
                            }

                            X509Certificate2Collection intermediates = DockerTlsCertificate.LoadIntermediates(
                                context.Configuration["SSL_CERT_PATH"], certificate);
                            Console.WriteLine(
                                $"HTTPS uses {certificate.Subject} (expires {certificate.NotAfter:u}) with {intermediates.Count} intermediate certificate(s).");

                            options.ListenAnyIP(443, listen =>
                            {
                                listen.Protocols = HttpProtocols.Http1AndHttp2;
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
