using System;
using System.Collections.Generic;

namespace MonogameTestbed
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
     

    public static class EnumExtensions
    { 
        public static T ToEnum<T>(this string value) 
            where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum) throw new ArgumentException("T must be an enumerated type");
            Array values = Enum.GetValues(typeof(T));
            foreach (var program in values)
            {
                string str = program.ToString();
                if (str == value)
                {
                    return (T)program;
                }
            }

            throw new NotImplementedException($"Unknown enum type: {value} of enum {typeof(T)}");
        }
    }

    static public class DataSource
    {
        

        public static Dictionary<Endpoint, Uri> EndpointMap = new Dictionary<Endpoint, Uri> { { Endpoint.TEST, new Uri("http://webdev.connectomes.utah.edu/RC1Test/OData") },
                                                                                               { Endpoint.RC1, new Uri("http://websvc1.connectomes.utah.edu/RC1/OData") },
                                                                                               { Endpoint.RC2, new Uri("http://websvc1.connectomes.utah.edu/RC2/OData") },
                                                                                               { Endpoint.RC3, new Uri("http://websvc1.connectomes.utah.edu/RC3/OData") },
                                                                                               { Endpoint.TEMPORALMONKEY, new Uri("http://websvc1.connectomes.utah.edu/NeitzTemporalMonkey/OData") },
                                                                                               { Endpoint.INFERIORMONKEY, new Uri("http://websvc1.connectomes.utah.edu/NeitzInferiorMonkey/OData") },
                                                                                               { Endpoint.CPED, new Uri("http://websvc1.connectomes.utah.edu/NeitzCPED/OData") },
                                                                                               { Endpoint.RPC1, new Uri("http://websvc1.connectomes.utah.edu/RPC1/OData") },
                                                                                               { Endpoint.RPC2, new Uri("http://websvc1.connectomes.utah.edu/RPC2/OData") },
                                                                                               { Endpoint.RPC3, new Uri("http://websvc1.connectomes.utah.edu/RPC3/OData") }
        };

    }
}
