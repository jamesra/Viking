using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Geometry;
using System.Dynamic;
using Geometry.Meshing;

namespace Geometry.JSON
{
    /// <summary>
    /// Helper methods for serializing and deserializing geometry to JSON
    /// </summary>
    public static class GeometryJSONExtensions
    {
        public static JObject ToJObject(this GridPolygon poly)
        {
            dynamic obj = new JObject();

            obj.ExteriorRing = poly.ExteriorRing.ToJArray();

            obj.InteriorRings = poly.HasInteriorRings ? new JArray(poly.InteriorRings.Select(ir => ir.ToJArray())) : new JArray();
            return obj;
        }

        public static string ToJSON(this GridPolygon poly)
        {
            dynamic obj = poly.ToJObject();
            return obj.ToString();
        }

        public static JArray ToJArray(this IEnumerable<GridPolygon> input)
        {
            JArray obj = new JArray(input.Select(p => p.ToJObject()));
            return obj;
        }

        public static string ToJSON(this IEnumerable<GridVector2> input)
        {
            JArray obj = input.ToJArray(); 
            return obj.ToString();
        }

        public static JObject ToJObject(this GridVector2 p)
        { 
            dynamic jObj = new JObject();
            jObj.X = p.X;
            jObj.Y = p.Y;
            return jObj; 
        }

        public static JArray ToJArray(this IEnumerable<GridVector2> points)
        {
            return new JArray(points.Select(p => p.ToJObject()));
        }

        public static JObject ToJObject(this GridLineSegment p)
        {
            dynamic jObj = new JObject();
            jObj.A = p.A;
            jObj.B = p.B;
            return jObj;
        }

        public static JArray ToJArray(this IEnumerable<GridLineSegment> lines)
        {
            return new JArray(lines.Select(p => p.ToJObject()));
        }

        public static GridVector2[] PointsFromJSON(string json)
        {
            if (json == null)
                return null;

            JArray obj = JArray.Parse(json);
            return PointsFromJSON(obj); 
        }


        public static GridVector2[] PointsFromJSON(this JToken points)
        {
            GridVector2[] output = points.Select(p => new GridVector2(System.Convert.ToDouble(p["X"]), System.Convert.ToDouble((p["Y"])))).ToArray();
            return output;
        }

        public static GridPolygon PolygonFromJSON(string json)
        {
            if (json == null)
                return null;

            JObject obj = JObject.Parse(json);

            var ExteriorRing = obj["ExteriorRing"];

            GridVector2[] ERing = ExteriorRing.PointsFromJSON();

            var InteriorRings = obj["InteriorRings"];
            List<GridVector2[]> IRings = InteriorRings.Select(ir => ir.PointsFromJSON()).ToList();

            GridPolygon output = new GridPolygon(ERing, IRings);

            return output;
        }

        public static GridPolygon PolygonFromJSON(JObject obj)
        {
            if (obj == null)
                return null;
             
            var ExteriorRing = obj["ExteriorRing"];

            GridVector2[] ERing = ExteriorRing.PointsFromJSON();

            var InteriorRings = obj["InteriorRings"];
            List<GridVector2[]> IRings = InteriorRings.Select(ir => ir.PointsFromJSON()).ToList();

            GridPolygon output = new GridPolygon(ERing, IRings);

            return output;
        }

        public static GridPolygon[] PolygonsFromJSON(string json)
        {
            if (json == null)
                return null;

            JArray array = JArray.Parse(json);

            List<GridPolygon> polygonList = new List<GridPolygon>();

            foreach(var token in array)
            {
                GridPolygon p = PolygonFromJSON(token as JObject);
                polygonList.Add(p);
            }

            return polygonList.ToArray();
        }

    }
}
