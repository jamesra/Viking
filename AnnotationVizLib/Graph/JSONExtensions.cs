using Viking.AnnotationServiceTypes.Interfaces;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;


namespace AnnotationVizLib
{
    static class NewtonsoftJSONExtensions
    {
        public static void AddAttributes(this JObject obj, IDictionary<string, object> attribs)
        {
            foreach (string key in attribs.Keys)
            {
                object value = attribs[key];
                JToken token = value as JToken != null ? (JToken)value : JToken.FromObject(value);
                obj[key] = token;
            }
        }

        public static JArray ToJArray(this IEnumerable<IStructureReadOnly> structs)
        {
            JArray arr = [];
            foreach (IStructureReadOnly s in structs)
            {
                JObject obj = s.ToJObject();
                arr.Add(obj);
            }

            return arr;
        }

        public static JObject ToJObject(this IStructureReadOnly s)
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
