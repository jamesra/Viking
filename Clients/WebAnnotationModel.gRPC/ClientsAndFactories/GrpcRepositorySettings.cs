using System;
using Grpc.Net.Client;

namespace WebAnnotationModel.gRPC
{
    public class GrpcRepositorySettings
    {
        public Uri Endpoint { get; set; } 
        public string Token { get; set; }
    }
}