using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using Viking.SSL;

namespace Viking.Identity.Server.Standalone
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
#if DEBUG
                .WriteTo.Console()
#endif
                .WriteTo.File("IdentityServerLogs.json", Serilog.Events.LogEventLevel.Verbose, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureKestrel(options =>
                    {
                        var configuration = new ConfigurationBuilder()
                                .SetBasePath(Directory.GetCurrentDirectory())
                                .AddJsonFile("appsettings.json", optional: true)
                                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                                .AddEnvironmentVariables()
                                .Build();

                        var sslOptions = configuration.GetSection("SSL").Get<SSLOptions>();

                        // Configure HTTP and HTTPS endpoints
                        options.ListenLocalhost(5000); // HTTP port
                        options.ListenLocalhost(sslOptions.Port, listenOptions => // HTTPS port
                        {
                            // Configure HTTPS with custom certificate 
                            var cert = Certs.LoadSSLCertificate(sslOptions);
                            if(cert != null)
                            {
                                try
                                {
                                    Log.Information("Certificate found");
                                    listenOptions.UseHttps(cert);
                                    Log.Information("Successfully configured Kestrel HTTPS with certificate: {Subject}", cert.Subject);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Failed to load certificate into Kestrel.");
                                    listenOptions.UseHttps(); // Fallback to development certificate
                                }
                            } 
                            else
                            {
                                Log.Warning("SSL configuration not found or certificate path is empty - using development certificate");
                                listenOptions.UseHttps(); // Use development certificate
                            }
                        });
                    });
                })
                ;
    }
}
