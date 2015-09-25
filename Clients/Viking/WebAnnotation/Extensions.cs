using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity.Spatial;

namespace WebAnnotation
{
    using RTree;
    using Geometry;

    public static class Extensions
    {
        public static RTree.Rectangle ToRTreeRect(this GridRectangle rect, float Z)
        {
            return new RTree.Rectangle((float)rect.Left, (float)rect.Bottom, (float)rect.Right, (float)rect.Top, Z, Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridRectangle rect, int Z)
        {
            return new RTree.Rectangle((float)rect.Left, (float)rect.Bottom, (float)rect.Right, (float)rect.Top, (float)Z, (float)Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridVector2 p, float Z)
        {
            return new RTree.Rectangle((float)p.X, (float)p.Y, (float)p.X, (float)p.Y, Z, Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridVector2 p, int Z)
        {
            return new RTree.Rectangle((float)p.X, (float)p.Y, (float)p.X, (float)p.Y, (float)Z, (float)Z);
        }

        public static System.Data.Entity.Spatial.DbGeometry ToGeometryPoint(this GridVector2 p)
        {
            return System.Data.Entity.Spatial.DbGeometry.PointFromText(string.Format("POINT ({0} {1})", p.X, p.Y), 0);
        }

        public static System.Data.Entity.Spatial.DbGeometry ToPolyLine(this GridVector2[] points)
        {
            StringBuilder PolyStringBuilder = new StringBuilder();
            PolyStringBuilder.Append("LINESTRING(");
            for(int i = 0; i  < points.Length; i++)
            {
                if (i != 0)
                    PolyStringBuilder.AppendFormat(",");

                PolyStringBuilder.AppendFormat("{0} {1}", points[i].X, points[i].Y);
            }
            PolyStringBuilder.Append(")");
            return System.Data.Entity.Spatial.DbGeometry.LineFromText(PolyStringBuilder.ToString(), 0);
        }

        public static System.Data.Entity.Spatial.DbGeometry ToCurvePolygon(double X, double Y, double Z, double Radius)
        {
            if (Radius == 0)
                throw new ArgumentException("Cannot create circle with a radius of zero");

            string circle_template = "CURVEPOLYGON(CIRCULARSTRING ({1} {3} {6}, " +
                                                                  "{0} {5} {6}, " +
                                                                  "{2} {3} {6}, " +
                                                                  "{0} {4} {6}, " +
                                                                  "{1} {3} {6}))";
            string circle_shape_string = string.Format(circle_template, new object[] { X, X - Radius, X + Radius, Y, Y - Radius, Y + Radius, Z });
            return System.Data.Entity.Spatial.DbGeometry.FromText(circle_shape_string,0);
        }


        public static System.Data.Entity.Spatial.DbGeometry ToCurvePolygon(this GridVector2[] points)
        {
            StringBuilder PolyStringBuilder = new StringBuilder();
            System.Diagnostics.Debug.Assert(points.Length == 4);
            PolyStringBuilder.Append("CURVEPOLYGON(CIRCULARSTRING (");
            for (int i = 0; i < points.Length; i++)
            {
                if (i != 0)
                    PolyStringBuilder.AppendFormat(",");

                PolyStringBuilder.AppendFormat("{0} {1}", points[i].X, points[i].Y);
            }
            PolyStringBuilder.Append(")");
             
            return System.Data.Entity.Spatial.DbGeometry.FromText(PolyStringBuilder.ToString());
        } 
    }
}
