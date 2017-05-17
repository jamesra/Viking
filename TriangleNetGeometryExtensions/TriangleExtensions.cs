using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using TriangleNet; 
using TriangleNet.Meshing;
using TriangleNet.Geometry;

namespace TriangleNet
{
    public static class TriangleExtensions
    {
        public static TriangleNet.Geometry.IPolygon CreatePolygon(this ICollection<GridVector2> Verticies, ICollection<GridVector2[]> InteriorPolygons = null)
        {
            IPoint2D[] v = Verticies.Select(p => p as IPoint2D).ToArray();
            IPoint2D[][] ip = null;
            if(InteriorPolygons != null)
                ip = InteriorPolygons.Select(interiorPolygon => interiorPolygon.Select(p => p as IPoint2D).ToArray()).ToArray();
            return CreatePolygon(v, ip );
        }

        public static TriangleNet.Geometry.IPolygon CreatePolygon(this ICollection<IPoint2D> Verticies, ICollection<IPoint2D[]> InteriorPolygons = null)
        {
            TriangleNet.Geometry.Polygon poly = new TriangleNet.Geometry.Polygon(Verticies.Count);
            TriangleNet.Geometry.Vertex[] points = Verticies.Select((v, i) => new TriangleNet.Geometry.Vertex(v.X, v.Y)).ToArray();

            TriangleNet.Geometry.Contour contour = new TriangleNet.Geometry.Contour(points);

            /*
            foreach (TriangleNet.Geometry.Vertex p in points)
            {
                poly.Add(p);
            }
            */

            poly.Add(contour);

            if (InteriorPolygons != null)
            {
                int InnerPolyID = 1; 
                foreach (ICollection<IPoint2D> inner_polygon in InteriorPolygons)
                {
                    TriangleNet.Geometry.Contour inner_poly = inner_polygon.CreateContour();
                    poly.Add(inner_poly, true);
                    InnerPolyID++;
                }
            }

            return poly;
        }

        public static TriangleNet.Geometry.IPolygon CreatePolygon(this GridPolygon input)
        {
            return CreatePolygon(input.ExteriorRing, input.InteriorRings);
        }

        public static TriangleNet.Geometry.IPolygon CreatePolygon(this IPolygon2D input)
        {
            return CreatePolygon(input.ExteriorRing, input.InteriorRings);
        }

        public static TriangleNet.Geometry.Contour CreateContour(this ICollection<GridVector2> Verticies)
        {
            TriangleNet.Geometry.Vertex[] points = Verticies.Select((v, i) => new TriangleNet.Geometry.Vertex(v.X, v.Y)).ToArray();
            TriangleNet.Geometry.Contour contour = new TriangleNet.Geometry.Contour(points);

            return contour;
        }

        public static TriangleNet.Geometry.Contour CreateContour(this ICollection<IPoint2D> Verticies)
        {
            TriangleNet.Geometry.Vertex[] points = Verticies.Select((v, i) => new TriangleNet.Geometry.Vertex(v.X, v.Y)).ToArray();
            TriangleNet.Geometry.Contour contour = new TriangleNet.Geometry.Contour(points);

            return contour;
        }

        public static IMesh Triangulate(this GridPolygon input)
        {
            TriangleNet.Geometry.IPolygon polygon = input.CreatePolygon();

            ConstraintOptions constraints = new ConstraintOptions();
            constraints.ConformingDelaunay = false;
            constraints.Convex = false;

            QualityOptions quality = new QualityOptions();
            quality.SteinerPoints = (polygon.Points.Count / 2) + 1;

            IMesh mesh = polygon.Triangulate(constraints, quality);
            return mesh;
        }

        /// <summary>
        /// Return the indicies for the array of points in the mesh.  If the point is not in the mesh return -1
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="points"></param>
        /// <returns></returns>
        public static int[] IndiciesForPointsXY(this IMesh mesh, GridVector2[] points)
        {
            GridVector2[] mesh_points = mesh.Vertices.Select(v => new GridVector2(v.X, v.Y)).ToArray();
              
            ///Create a map of position to index
            Dictionary<GridVector2, int> lookup = mesh_points.Select((p, i) => i).ToArray().ToDictionary(i => mesh_points[i]);

            int[] output_map = new int[mesh_points.Length];

            for(int i = 0; i < points.Length; i++)
            {
                if(lookup.ContainsKey(points[i]))
                {
                    output_map[i] = lookup[points[i]];
                }
                else
                {
                    output_map[i] = -1; 
                }
            }

            return output_map;
        }

        public static IMesh Triangulate(this IPolygon2D input)
        {
            TriangleNet.Geometry.IPolygon polygon = input.CreatePolygon();

            ConstraintOptions constraints = new ConstraintOptions();
            constraints.ConformingDelaunay = false;
            constraints.Convex = false;

            QualityOptions quality = new QualityOptions();
            quality.SteinerPoints = (polygon.Points.Count / 2) + 1;

            IMesh mesh = polygon.Triangulate(constraints, quality); 
            return mesh;
        }
    }
}
