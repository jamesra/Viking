using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// Kestrel host. Docker binds h2c :80 and HTTPS :443 separately — gRPC clients do not follow redirects.
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
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

                            options.ListenAnyIP(443, listen =>
                            {
                                listen.Protocols = HttpProtocols.Http1AndHttp2;
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
