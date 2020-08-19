using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Annotation.Interfaces;


namespace AnnotationVizLib
{
    static class NewtonsoftJSONExtensions
    {
        public static void AddAttributes(this JObject obj, IDictionary<string, object> attribs)
        {
            foreach (string key in attribs.Keys)
            {
                object value = attribs[key];
                JToken token;
                if (value as JToken != null)
                {
                    token = (JToken)value;
                }
                else
                {
                    token = JToken.FromObject(value);
                }

                obj[key] = token;
            }
        }

        public static JArray ToJArray(this IEnumerable<IStructure> structs)
        {
            JArray arr = new JArray();
            foreach(IStructure s in structs)
            {
                JObject obj = s.ToJObject();
                arr.Add(obj);
            }

            return arr;
        }

        public static JObject ToJObject(this IStructure s)
        {
            dynamic obj = new JObject();
            obj.ID = s.ID;
            obj.Label = s.Label;
            obj.ParentID = s.ParentID;
            obj.Tags = s.TagsXML;
            obj.TypeID = s.TypeID;
            
            return obj;
        }
    } 
}
