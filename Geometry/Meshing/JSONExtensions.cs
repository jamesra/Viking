using Geometry.JSON;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace Geometry.Meshing
{
    public static class JSONExtensions
    {
        public static JObject ToJObject(this IVertex2D v)
        {
            dynamic jObj = new JObject();
            jObj.Position = v.Position.ToJObject();
            jObj.Index = v.Index;
            return jObj;
        }

        public static JObject ToJObject(this IEdge e)
        {
            dynamic jObj = new JObject();
            jObj.A = e.A;
            jObj.B = e.B;
            return jObj;
        }

        public static JObject ToJObject(this IFace f)
        {
            dynamic jObj = new JObject();
            jObj.iVerts = new JArray(f.iVerts.ToArray());
            return jObj;
        }
    }
}
