using System;
using Grpc.Net.Client;

namespace WebAnnotationModel.gRPC
{
    /// <summary>
    /// Annotation gRPC endpoint and optional static token. Prefer IAnnotationAccessTokenProvider over Token.
    /// </summary>
    public class GrpcRepositorySettings
    {
        public Uri Endpoint { get; set; }

        /// <summary>
        /// Static bearer token from config. Unused when a token provider is registered.
        /// </summary>
        public string Token { get; set; }
    }
}