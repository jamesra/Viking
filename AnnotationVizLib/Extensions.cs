using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnnotationVizLib.AnnotationService;
using Simple.OData.Client;

namespace AnnotationVizLib
{
    public static class AttributeExtensions
    {
        /// <summary>
        /// Converts attributes to a string and caches the results.  Not caching the results was causing performance issues.
        /// </summary>
        /// <returns></returns>
        public static string AttributesToString(this IDictionary<string, object> dict)
        {
            StringBuilder sb = new StringBuilder();
            List<string> keys = dict.Keys.ToList();
            keys.Sort();
             
            foreach (string key in keys)
            { 
                sb.AppendFormat(" {0} : {1}", key, dict[key].ToString());
            }

            return sb.ToString();
        }
    }

    public static class ODataExtensions
    {
        public static Geometry.Scale ToGeometryScale(this ODataClient.Geometry.Scale scale)
        {
            return new Geometry.Scale(new Geometry.AxisUnits(scale.X.Value, scale.X.Units),
                                      new Geometry.AxisUnits(scale.Y.Value, scale.Y.Units),
                                      new Geometry.AxisUnits(scale.Z.Value, scale.Z.Units));
        }
    }

    public static class WCFExtensions
    {
        public static Geometry.Scale ToGeometryScale(this AnnotationVizLib.AnnotationService.Scale scale)
        {
            return new Geometry.Scale(new Geometry.AxisUnits(scale.X.Value, scale.X.Units),
                                      new Geometry.AxisUnits(scale.Y.Value, scale.Y.Units),
                                      new Geometry.AxisUnits(scale.Z.Value, scale.Z.Units));
        }
    }
}
