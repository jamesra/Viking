using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;
using Viking.SSL;

namespace Viking.Identity.Server.WebManagement
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
                .WriteTo.File("IdentityServerManagement.json", Serilog.Events.LogEventLevel.Verbose, rollingInterval: RollingInterval.Day)
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
                        // Configure HTTPS with custom certificate
                        var configuration = new ConfigurationBuilder()
                            .SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile("appsettings.json", optional: true)
                            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                            .AddEnvironmentVariables()
                            .Build();

                        var sslOptions = configuration.GetSection("SSL").Get<SSLOptions>();

                        options.ListenLocalhost(4000); // HTTP port
                        options.ListenLocalhost(sslOptions.Port, listenOptions => // HTTPS port
                        {
                            var sslCert = Certs.LoadSSLCertificate(sslOptions);
                            if (sslCert != null)
                            {
                                try
                                {
                                    Log.Information("Certificate found");
                                    listenOptions.UseHttps(sslCert);
                                    Log.Information("Successfully configured Kestrel HTTPS with certificate: {Subject}", sslCert.Subject);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Failed to configure Kestrel HTTPS with certificate: {Subject}", sslCert.Subject);
                                }
                            }
                            else
                            {
                                Log.Warning("SSL certificate not found");
                            }
                        });
                    });
                });
    }
}
                
