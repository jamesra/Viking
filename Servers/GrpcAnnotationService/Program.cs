using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace gRPCAnnotationService
{
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
