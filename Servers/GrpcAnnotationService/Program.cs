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
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel((context, options) =>
                    {
                        // UseDockerPorts is set in the container. Local launch uses launchSettings endpoints.
                        if (context.Configuration.GetValue("Kestrel:UseDockerPorts", false))
                        {
                            options.ListenAnyIP(80, listen => listen.Protocols = HttpProtocols.Http2);
                            options.ListenAnyIP(443, listen =>
                            {
                                listen.Protocols = HttpProtocols.Http1AndHttp2;
                                listen.UseHttps();
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
