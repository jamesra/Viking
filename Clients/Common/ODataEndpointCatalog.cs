using System;
using System.Collections.Generic;

namespace Viking.Common
{
    public enum Endpoint
    {
        TEST,
        RC1,
        RC2,
        RC3,
        TEMPORALMONKEY,
        INFERIORMONKEY,
        RPC1,
        RPC2,
        RPC3,
        CPED
    }

    public static class ODataEndpointCatalog
    {
        public static Dictionary<Endpoint, Uri> EndpointMap { get; } = new()
        {
            { Endpoint.TEST,           new Uri("http://webdev.codepharm.net/RC1Test/OData") },
            { Endpoint.RC1,            new Uri("http://websvc.codepharm.net/RC1/OData") },
            { Endpoint.RC2,            new Uri("http://websvc.codepharm.net/RC2/OData") },
            { Endpoint.RC3,            new Uri("http://websvc.codepharm.net/RC3/OData") },
            { Endpoint.TEMPORALMONKEY, new Uri("http://websvc.codepharm.net/NeitzTemporalMonkey/OData") },
            { Endpoint.INFERIORMONKEY, new Uri("http://websvc.codepharm.net/NeitzInferiorMonkey/OData") },
            { Endpoint.CPED,           new Uri("http://websvc.codepharm.net/NeitzCPED/OData") },
            { Endpoint.RPC1,           new Uri("http://websvc.codepharm.net/RPC1/OData") },
            { Endpoint.RPC2,           new Uri("http://websvc.codepharm.net/RPC2/OData") },
            { Endpoint.RPC3,           new Uri("http://websvc.codepharm.net/RPC3/OData") },
        };
    }
}
