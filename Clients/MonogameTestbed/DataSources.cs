using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonogameTestbed
{
    public enum ENDPOINT
    {
        TEST,
        RC1,
        RC2,
        TEMPORALMONKEY,
        INFERIORMONKEY,
        RPC1
    }

    static public class DataSource
    {
        

        public static Dictionary<ENDPOINT, Uri> EndpointMap = new Dictionary<ENDPOINT, Uri> { { ENDPOINT.TEST, new Uri("http://webdev.connectomes.utah.edu/RC1Test/OData") },
                                                                                               { ENDPOINT.RC1, new Uri("http://websvc1.connectomes.utah.edu/RC1/OData") },
                                                                                               { ENDPOINT.RC2, new Uri("http://websvc1.connectomes.utah.edu/RC2/OData") },
                                                                                               { ENDPOINT.TEMPORALMONKEY, new Uri("http://websvc1.connectomes.utah.edu/NeitzTemporalMonkey/OData") },
                                                                                               { ENDPOINT.INFERIORMONKEY, new Uri("http://websvc1.connectomes.utah.edu/NeitzInferiorMonkey/OData") },
                                                                                               { ENDPOINT.RPC1, new Uri("http://websvc1.connectomes.utah.edu/RPC1/OData") }};

    }
}
