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
        public static GridVector2 ToGridVector2(this Vertex v)
        {
            return new GridVector2(v.X, v.Y);
        }

        public static GridVector2 ToGridVector2(this TriangleNet.Topology.DCEL.Vertex v)
        {
            return new GridVector2(v.X, v.Y);
        }

         
        public static List<GridLineSegment> ToLines(this TriangleNet.Topology.DCEL.DcelMesh mesh)
        {
            if (mesh == null)
                return null;

            List<GridLineSegment> listLines = new List<GridLineSegment>();
            //Create a map of Vertex ID's to DRMesh ID's
            int[] IndexMap = mesh.Vertices.Select(v => v.ID).ToArray();

            foreach (var e in mesh.Edges)
            {
                listLines.Add(new GridLineSegment(mesh.Vertices[e.P0].ToGridVector2(),
                                           mesh.Vertices[e.P1].ToGridVector2()));
            }

            return listLines;
        }

        public static List<GridLineSegment> ToLines(this TriangleNet.Meshing.IMesh mesh)
        {
            if (mesh == null)
                return null;

            List<GridLineSegment> listLines = new List<GridLineSegment>();
            //Create a map of Vertex ID's to DRMesh ID's
            int[] IndexMap = mesh.Vertices.Select(v => v.ID).ToArray();
            Vertex[] verticies = mesh.Vertices.ToArray();

            foreach (var e in mesh.Edges)
            {
                listLines.Add(new GridLineSegment(verticies[e.P0].ToGridVector2(),
                                           verticies[e.P1].ToGridVector2()));
            }

            return listLines;
        }

        public static TriangleNet.Geometry.Polygon CreatePolygon(this ICollection<GridVector2> Verticies, ICollection<GridVector2[]> InteriorPolygons = null)
        {
            IPoint2D[] v = Verticies.Select(p => p as IPoint2D).ToArray();
            IPoint2D[][] ip = null;
            if(InteriorPolygons != null)
                ip = InteriorPolygons.Select(interiorPolygon => interiorPolygon.Select(p => p as IPoint2D).ToArray()).ToArray();
            return CreatePolygon(v, ip );
        }

        public static TriangleNet.Geometry.Polygon CreatePolygon(this ICollection<IPoint2D> Verticies, ICollection<IPoint2D[]> InteriorPolygons = null)
        {
            TriangleNet.Geometry.Polygon poly = new TriangleNet.Geometry.Polygon(Verticies.Count);
            TriangleNet.Geometry.Vertex[] points = Verticies.Select((v, i) => new TriangleNet.Geometry.Vertex(v.X, v.Y)).ToArray();

            TriangleNet.Geometry.Contour contour = new TriangleNet.Geometry.Contour(points);
             
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

        /// <summary>
        /// Append the exterior ring to the polygon as new points with a contraint around the exterior ring
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="other"></param>
        public static void Append(this Polygon polygon, GridPolygon other)
        {
            TriangleNet.Geometry.Contour contour = new TriangleNet.Geometry.Contour(other.ExteriorRing.Select(p => new Vertex(p.X, p.Y)));
            polygon.Add(contour); 
        }

        /// <summary>
        /// Append the exterior ring to the polygon as new points with a contraint around the exterior ring
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="other"></param>
        public static void Append(this Polygon polygon, ICollection<GridVector2> points)
        {
            points = points.EnsureOpenRing();

            foreach (Vertex v in points.Select(p => new Vertex(p.X, p.Y)))
            {
                polygon.Add(v);
            }
        }

        /// <summary>
        /// Append the exterior ring to the polygon as new points with a contraint around the exterior ring
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="other"></param>
        public static void AppendCountour(this Polygon polygon, ICollection<GridVector2> points)
        {
            TriangleNet.Geometry.Contour contour = new TriangleNet.Geometry.Contour(points.Select(p => new Vertex(p.X, p.Y)));
            polygon.Add(contour, true);
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

        /// <summary>
        /// Triangulate the polygon.
        /// </summary>
        /// <param name="input">Polygon to generate faces for</param>
        /// <param name="internalPoints">Additional points inside the polygon which should be included in the triangulation</param>
        /// <returns></returns>
        public static IMesh Triangulate(this ICollection<IPoint2D> points)
        {
            TriangleNet.Geometry.IPolygon polygon = points.CreatePolygon();

            ConstraintOptions constraints = new ConstraintOptions();
            constraints.ConformingDelaunay = false;
            constraints.Convex = false;

            QualityOptions quality = new QualityOptions();
            quality.SteinerPoints = 0;

            IMesh mesh = polygon.Triangulate(constraints, quality);
            return mesh;
        }


        /// <summary>
        /// Triangulate the polygon.
        /// </summary>
        /// <param name="input">Polygon to generate faces for</param>
        /// <param name="internalPoints">Additional points inside the polygon which should be included in the triangulation</param>
        /// <returns></returns>
        public static IMesh Triangulate(this GridPolygon input, ICollection<IPoint2D> internalPoints = null)
        {
            TriangleNet.Geometry.IPolygon polygon = input.CreatePolygon();

            if (internalPoints != null)
            {
                foreach (IPoint2D p in internalPoints)
                {
                    polygon.Add(new Vertex(p.X, p.Y));
                }
            }

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
            points = points.EnsureOpenRing();

            GridVector2[] mesh_points = mesh.Vertices.Select(v => new GridVector2(v.X, v.Y)).ToArray();

            ///Create a map of position to index
            Dictionary<GridVector2, int> lookup = mesh_points.Select((p, i) => i).ToArray().ToDictionary(i => mesh_points[i]);

            int[] output_map = new int[points.Length];

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

        public static TriangleNet.Voronoi.VoronoiBase Voronoi(this ICollection<IPoint2D> points)
        {
            return points.Select(p => new GridVector2(p.X, p.Y)).ToList().Voronoi();
        }

        public static TriangleNet.Voronoi.VoronoiBase Voronoi(this ICollection<GridVector2> input)
        {
            TriangleNet.Geometry.Vertex[] verticies = input.Select(p => new Vertex(p.X, p.Y)).ToArray();

            return verticies.Voronoi();
        }

        public static TriangleNet.Voronoi.VoronoiBase Voronoi(this ICollection<Vertex> verticies)
        { 
            Polygon polygon = new Polygon();
            foreach (Vertex v in verticies)
            {
                polygon.Add(v);
            }

            ConstraintOptions constraints = new ConstraintOptions();
            constraints.ConformingDelaunay = false;
            constraints.Convex = false;

            QualityOptions quality = new QualityOptions();
            quality.SteinerPoints = (polygon.Points.Count / 2) + 1;
            Mesh mesh = (Mesh)polygon.Triangulate(constraints, quality);

            TriangleNet.Voronoi.VoronoiBase voronoi = null;

            if (mesh.IsPolygon)
            {
                try
                {
                    voronoi = new TriangleNet.Voronoi.BoundedVoronoi(mesh);
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
            else
            {
                voronoi = new TriangleNet.Voronoi.StandardVoronoi(mesh);
            }

            return voronoi;
        }
    }
}
