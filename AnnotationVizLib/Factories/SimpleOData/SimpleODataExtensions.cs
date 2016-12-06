using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Simple.OData.Client;
using System.Diagnostics;

namespace AnnotationVizLib
{
    static class SimpleODataExtensions
    {
        public static Geometry.Scale GetScale(this Simple.OData.Client.ODataClient client)
        {
            Task<IDictionary<string, object>> t = client.ExecuteFunctionAsSingleAsync("Scale", null);
            t.Wait();
            var scale = t.Result;
            Debug.Assert(scale != null);

            return new Geometry.Scale(ConvertToAxisScale((IDictionary<string, object>)scale["X"]),
                                      ConvertToAxisScale((IDictionary<string, object>)scale["Y"]),
                                      ConvertToAxisScale((IDictionary<string, object>)scale["Z"]));
        }

        private static Geometry.AxisUnits ConvertToAxisScale(IDictionary<string, object> axis)
        {
            return new Geometry.AxisUnits((double)axis["Value"], (string)axis["Units"]);
        }

        internal static string ToODataArrayParameterString(this ICollection<long> IDs)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('[');
            bool FirstEntry = true;
            foreach(long ID in IDs)
            {
                if(FirstEntry)
                { 
                    sb.AppendFormat("{0}", ID);
                    FirstEntry = false;
                }
                else
                {
                    sb.AppendFormat(",{0}", ID);
                }
            }
            
            sb.Append(']');

            return sb.ToString();
        }
    }
}
