using IdentityModel.Client;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using NUnit;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotationModel.gRPC;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;


namespace WebAnnotationModel.gRPC.Tests
{
    public static class ConfigurationExtensions
    {
        public static IConfigurationBuilder UseTestConfigurationBuilder(this IConfigurationBuilder builder)
        {
            builder
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddUserSecrets<UserIdentity>()
                .AddEnvironmentVariables();
            return builder;
        }
    }

    public class IdentityClientSettings
    {
        public string ClientId { get; set; }
        public string Scope { get; set; }
        public string ClientSecret { get; set; }
    }

    public class UserIdentity
    {
        public string UserName { get; set; }
        public string Password { get; set; } 
    }

    public class Tests
    {
        private IHost host;

        private string IdentityServerURL;
        private string GrpcServerURL;

        private IdentityClientSettings IdentityClient;
        private UserIdentity UserIdentity;

        [SetUp]
        public void Setup()
        {
            host = CreateHostBuilder().Build();
        }

        IHostBuilder CreateHostBuilder()
        { 
            var endpoint = new Uri("http://webdev.connectomes.utah.edu/Test/");
            return Host.CreateDefaultBuilder() 
                .ConfigureAppConfiguration((appContext) => appContext.UseTestConfigurationBuilder())
                .ConfigureServices((context, services) =>
                    { 
                        var config = context.Configuration;
                        services.AddOptions<GrpcChannelOptions>()
                            .Bind(config.GetSection(nameof(GrpcChannelOptions)));

                        IdentityServerURL = config.GetValue<string>("IdentityServer:Endpoint");
                        GrpcServerURL = config.GetValue<string>("GrpcServer:Endpoint");
                        IdentityClient = config.GetValue<IdentityClientSettings>("IdentityClient");
                        UserIdentity = config.GetValue<UserIdentity>("TestIdentity");

                        services.AddGrpcChannelManager();

                        services.AddOptions<GrpcRepositorySettings>()
                            .Bind(config.GetSection(nameof(GrpcRepositorySettings)));

                        _ = services.AddStandardStructureLinkConverters()
                            .AddStandardLocationConverters()
                            .AddStandardLocationLinkConverters()
                            .AddStandardStructureConverters()
                            .AddStandardStructureTypeConverters()
                            .AddStandardPermittedStructureLinkConverters()
                            .AddStandardQueryLogger()
                            .AddStandardQueryConverters()
                            .AddSingleton<IGrpcChannelManager, GrpcChannelManager>()
                            .AddSingleton<ILocationStore, LocationStore>()
                            .AddSingleton<IStructureStore, StructureStore>()
                            .AddSingleton<IStructureTypeStore, StructureTypeStore>()
                            .AddGrpcLocationRepository((o) => { return; })
                            //.AddStructureServer(endpoint)
                            //.AddStructureTypeServer(endpoint)
                            .AddDefaultStructureLinkToStructureUpdater()
                            .AddDefaultLocationLinkToLocationUpdater()
                            .AddDefaultPermittedStructureLinkToStructureTypeUpdater();
                    }
                );
        }
          
        [Test]
        public async Task TestGetLastModifiedLocation()
        { 
            HttpClient c = new HttpClient() { 
                BaseAddress = new Uri(IdentityServerURL), 
                DefaultRequestVersion = new Version(2,0)
            };
            
            var disco_response = await c.GetDiscoveryDocumentAsync(IdentityServerURL);
            Assert.False(disco_response.IsError);

            PasswordTokenRequest request = new PasswordTokenRequest()
            {
                Address = disco_response.TokenEndpoint,
                UserName = "jamesan",
                Password = "Wat>com3",
                ClientId = "ro.viking",
                ClientSecret = "CorrectHorseBatteryStaple",
                Scope = "openid Viking.Annotation",
            };

            var token = await c.RequestPasswordTokenAsync(request);
            Assert.NotNull(token, "No token returned");

            using (HttpClient userInfoClient = new HttpClient())
            {
                var userInfo = await userInfoClient.GetUserInfoAsync(new UserInfoRequest()
                {
                    Address = disco_response.UserInfoEndpoint,
                    Token = token.AccessToken
                });

                if (userInfo.IsError)
                {
                    Console.WriteLine($"Error: {userInfo.Error}");
                    return;
                }

                Trace.WriteLine("\nClaims");
                foreach (var claim in userInfo.Claims)
                {
                    Trace.WriteLine(claim.ToString());
                }

                Trace.WriteLine("\n");
            }

            HttpClient grpcClient = new HttpClient()
            {
                BaseAddress = new Uri(GrpcServerURL),
                DefaultRequestVersion = new Version(2, 0)
            };

            grpcClient.SetBearerToken(token.IdentityToken);
            grpcClient.SetToken("access_token", token.AccessToken);
            grpcClient.SetToken("id_token", token.IdentityToken);

            var credentials = CallCredentials.FromInterceptor((context, metadata) =>
            {
                if (!string.IsNullOrEmpty(token.AccessToken))
                {
                    metadata.Add("Authorization", $"Bearer {token.AccessToken}");
                }
                return Task.CompletedTask;
            });

            GrpcChannelManager channelManager =
                new GrpcChannelManager(Options.Create<GrpcChannelOptions>(new GrpcChannelOptions()
                {
                    HttpClient = grpcClient,
                    Credentials = ChannelCredentials.Create(new SslCredentials(), credentials) 
                }));

            IObjectConverter<ILocation, Location> locConverter = new LocationToLocationServerConverter();

            LocationsClientFactory clientFactory = new LocationsClientFactory(channelManager,  locConverter,
                Options.Create<gRPC.GrpcRepositorySettings>(new GrpcRepositorySettings {Endpoint = new Uri(GrpcServerURL) }));
            var client = clientFactory.GetOrCreate();
            var loc = await client.GetLastModifiedLocation();

            var mwkt = loc.MosaicGeometryWKT;
            Assert.NotNull(mwkt);
            Assert.NotNull(loc);
            
            Assert.Pass();
        }
    }
}