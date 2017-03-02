using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; 

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


}
