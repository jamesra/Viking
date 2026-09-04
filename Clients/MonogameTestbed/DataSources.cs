using System;
using Viking.Common;

namespace MonogameTestbed
{
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

    public static class DataSource
    {
        public static System.Collections.Generic.Dictionary<Endpoint, Uri> EndpointMap =>
            ODataEndpointCatalog.EndpointMap;
    }
}
