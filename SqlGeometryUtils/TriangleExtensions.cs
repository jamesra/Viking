using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TriangleNet;
using TriangleNet.Geometry;
using Microsoft.SqlServer.Types;

namespace SqlGeometryUtils
{
    static public class TriangleExtensions
    {
        public static TriangleNet.Geometry.Vertex[] ToTriangleNetVerticies(this SqlGeometry geometry)
        {
            TriangleNet.Geometry.Vertex[] verts = new Vertex[geometry.STNumPoints().Value];
            for(int i = 0; i < verts.Length; i++)
            {
                SqlGeometry point = geometry.GetPoint(i);
                verts[i] = new Vertex(point.STX.Value, point.STY.Value);
            }

            return verts;
        }

        public static TriangleNet.Geometry.Contour ToTriangleNetContour(this SqlGeometry geometry)
        {
            return new Contour(geometry.ToTriangleNetVerticies());
        }

        public static TriangleNet.Geometry.IPolygon ToTriangleNetPolygon(this SqlGeometry geometry)
        {
            if (!geometry.InstanceOf("POLYGON"))
                throw new ArgumentException("Can only convert POLYGON SqlGeometry to Triangle.Net Polygons");

            Vertex[] outer_verts = geometry.ToTriangleNetVerticies();
            Contour outer_contour = new Contour(outer_verts);
            Polygon polygon = new Polygon(outer_verts.Length);

            foreach (Vertex v in outer_verts)
            {
                polygon.Add(v);
            }

            polygon.Add(outer_contour);

            for(int iRing = 0; iRing < geometry.STNumInteriorRing().Value; iRing++)
            {
                SqlGeometry innerRing = geometry.GetInteriorRing(iRing);
                Contour innerHole = innerRing.ToTriangleNetContour();
                polygon.Add(innerHole, true);
            }
                        
            return polygon;
        }
    }
}
