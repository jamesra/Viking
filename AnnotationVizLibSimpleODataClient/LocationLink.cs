using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationVizLib.SimpleOData
{
    public class LocationLink
    {
        public static LocationLink FromDictionary(IDictionary<string, object> dict)
        {
            LocationLink ll = new LocationLink { A = System.Convert.ToUInt64(dict["A"]), B = System.Convert.ToUInt64(dict["B"]) };
            return ll;
        }

        public ulong A { get; private set; }
        public ulong B { get; private set; }
    }
}
