using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using IdentityModel.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using WebAnnotationModel;
using WebAnnotationModel.gRPC;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Headless DI host mirroring Viking/VikingAU composition: ConfigureAnnotationModel
    /// + bearer auth against the Docker test stack (identity :5020, gRPC :5010).
    /// </summary>
    internal sealed class AnnotationStoresTestHost : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly string _accessToken;

        public IAnnotationStores Stores { get; }
        public string GrpcEndpoint { get; }
        public string IdentityEndpoint { get; }
        public string UserName { get; }

        private AnnotationStoresTestHost(
            ServiceProvider serviceProvider,
            IAnnotationStores stores,
            string accessToken,
            string grpcEndpoint,
            string identityEndpoint,
            string userName)
        {
            _serviceProvider = serviceProvider;
            Stores = stores;
            _accessToken = accessToken;
            GrpcEndpoint = grpcEndpoint;
            IdentityEndpoint = identityEndpoint;
            UserName = userName;
        }

        public static async Task<AnnotationStoresTestHost> CreateAsync()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            var identityUrl = config["IdentityServer:Endpoint"];
            var grpcUrl = config["GrpcServer:Endpoint"];
            var identityClient = config.GetSection("IdentityClient").Get<IdentityClientSettings>();
            var userIdentity = config.GetSection("TestIdentity").Get<UserIdentity>();

            Assert.That(identityUrl, Is.Not.Null.And.Not.Empty);
            Assert.That(grpcUrl, Is.Not.Null.And.Not.Empty);
            Assert.That(identityClient, Is.Not.Null);
            Assert.That(userIdentity, Is.Not.Null);

            var accessToken = await RequestAccessTokenAsync(identityUrl, identityClient, userIdentity)
                .ConfigureAwait(false);

            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            var services = new ServiceCollection();
            // Auth via HTTP handler (works with Insecure credentials). Do not register
            // IAnnotationAccessTokenProvider — CallCredentials cannot compose with Insecure.
            services.ConfigureAnnotationModel(
                opts => opts.Endpoint = new Uri(grpcUrl),
                channelOpts =>
                {
                    var socketsHandler = new SocketsHttpHandler
                    {
                        EnableMultipleHttp2Connections = true,
                        SslOptions =
                        {
                            RemoteCertificateValidationCallback = static (_, _, _, _) => true
                        }
                    };
                    channelOpts.HttpHandler = new BearerTokenHandler(accessToken)
                    {
                        InnerHandler = socketsHandler
                    };
                    channelOpts.Credentials = ChannelCredentials.Insecure;
                });

            var sp = services.BuildServiceProvider();
            sp.GetRequiredService<IOptions<GrpcRepositorySettings>>().Value.Endpoint = new Uri(grpcUrl);

            var stores = sp.GetRequiredService<IAnnotationStores>();
            // Bind static Store so LocationStore.Create(... linked ...) and similar paths work.
            Store.Initialize(stores);

            return new AnnotationStoresTestHost(
                sp, stores, accessToken, grpcUrl, identityUrl, userIdentity.UserName);
        }

        private static async Task<string> RequestAccessTokenAsync(
            string identityServerUrl,
            IdentityClientSettings identityClient,
            UserIdentity userIdentity)
        {
            using var http = new HttpClient { BaseAddress = new Uri(identityServerUrl) };
            var disco = await http.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
            {
                Address = identityServerUrl,
                Policy =
                {
                    RequireHttps = false,
                    ValidateIssuerName = false
                }
            }).ConfigureAwait(false);
            Assert.That(disco.IsError, Is.False, $"{disco.Error} ({disco.Exception?.Message})");

            var token = await http.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = disco.TokenEndpoint,
                UserName = userIdentity.UserName,
                Password = userIdentity.Password,
                ClientId = identityClient.ClientId,
                ClientSecret = identityClient.ClientSecret,
                Scope = identityClient.Scope,
            }).ConfigureAwait(false);

            Assert.That(token.IsError, Is.False, token.Error);
            Assert.That(token.AccessToken, Is.Not.Null.And.Not.Empty);
            return token.AccessToken;
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
        }

        private sealed class BearerTokenHandler : DelegatingHandler
        {
            private readonly string _accessToken;

            public BearerTokenHandler(string accessToken) => _accessToken = accessToken;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                System.Threading.CancellationToken cancellationToken)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
