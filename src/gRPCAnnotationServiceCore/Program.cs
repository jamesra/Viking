using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;

namespace gRPCAnnotationServiceCore
{
    public class Program
    {
        private static bool _useHttp;

        public static void Main(string[] args)
        {
            // Option to use HTTP instead of HTTPS, for compatibility with .NET 4.x ChannelCredentials.Insecure
            _useHttp = args.Any(a => a.Equals("--http", StringComparison.OrdinalIgnoreCase));

            CreateHostBuilder(args).Build().Run();
        }

        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    if (_useHttp)
                    {
                        webBuilder.ConfigureKestrel(k =>
                        {
                            k.ListenLocalhost(5000, lo => lo.Protocols = HttpProtocols.Http2);
                        });
                    }

                    webBuilder.UseStartup<Startup>();
                });
    }
}

