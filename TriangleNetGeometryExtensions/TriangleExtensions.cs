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

        public static GridVector3 ToGridVector3(this Vertex v, double Z)
        {
            return new GridVector3(v.X, v.Y, Z);
        }

        public static GridVector3 ToGridVector3(this TriangleNet.Topology.DCEL.Vertex v, double Z)
        {
            return new GridVector3(v.X, v.Y, Z);
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

        public static GridPolygon ToPolygon(this TriangleNet.Topology.DCEL.Face face)
        {
            if (face == null)
                return null;

            GridVector2[] verts = face.EnumerateEdges().Select(edge => edge.Origin.ToGridVector2()).ToArray();

            GridPolygon polygon = new GridPolygon(verts.EnsureClosedRing());
            return polygon;
        }

        public static List<GridPolygon> ToPolygons(this TriangleNet.Topology.DCEL.DcelMesh mesh)
        {
            if (mesh == null)
                return null;

            List<GridPolygon> listTriangles = new List<GridPolygon>();
            return mesh.Faces.Select(face => face.ToPolygon()).ToList();
        }

        public static List<GridLineSegment> ToLines(this TriangleNet.Meshing.IMesh mesh)
        {
            if (mesh == null)
                return null;

            SortedSet<GridLineSegment> listLines = new SortedSet<GridLineSegment>();
            //Create a map of Vertex ID's to DRMesh ID's
            int[] IndexMap = mesh.Vertices.Select(v => v.ID).ToArray();
            Vertex[] verticies = mesh.Vertices.ToArray();

            foreach (var t in mesh.Triangles)
            {
                listLines.Add(new GridLineSegment(t.GetVertex(0).ToGridVector2(), t.GetVertex(1).ToGridVector2()));
                listLines.Add(new GridLineSegment(t.GetVertex(1).ToGridVector2(), t.GetVertex(2).ToGridVector2()));
                listLines.Add(new GridLineSegment(t.GetVertex(2).ToGridVector2(), t.GetVertex(0).ToGridVector2()));
            }

            return listLines.ToList();
        }

        public static List<GridTriangle> ToTriangles(this TriangleNet.Meshing.IMesh mesh)
        {
            if (mesh == null)
                return null;

            List<GridTriangle> listTriangles = new List<GridTriangle>();
            //Create a map of Vertex ID's to DRMesh ID's
            int[] IndexMap = mesh.Vertices.Select(v => v.ID).ToArray();
            Vertex[] verticies = mesh.Vertices.ToArray();

            foreach (var tri in mesh.Triangles)
            {
                listTriangles.Add(new GridTriangle(tri.GetVertex(0).ToGridVector2(),
                                                   tri.GetVertex(1).ToGridVector2(),
                                                   tri.GetVertex(2).ToGridVector2()));
            }

            return listTriangles;
        }

        public static TriangleNet.Geometry.Polygon CreatePolygon(this ICollection<GridVector2> Verticies, ICollection<GridVector2[]> InteriorPolygons = null)
        {
            IPoint2D[] v = Verticies.Select(p => p as IPoint2D).ToArray();
            IPoint2D[][] ip = null;
            if (InteriorPolygons != null)
                ip = InteriorPolygons.Select(interiorPolygon => interiorPolygon.Select(p => p as IPoint2D).ToArray()).ToArray();
            return CreatePolygon(v, ip);
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


        public static TriangleNet.Geometry.Polygon CreatePolygon(this GridPolygon input)
        {
            return CreatePolygon(input.ExteriorRing, input.InteriorRings);
        }

        public static TriangleNet.Geometry.Polygon CreatePolygon(this IPolygon2D input)
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

        public static IMesh Triangulate(this ICollection<GridVector2> points, int SteinerPoints = 0)
        {

            return Triangulate(points.Select(p => (IPoint2D)p).ToList(), SteinerPoints);
        }

        /// <summary>
        /// Triangulate the polygon.
        /// </summary>
        /// <param name="input">Polygon to generate faces for</param>
        /// <param name="internalPoints">Additional points inside the polygon which should be included in the triangulation</param>
        /// <returns></returns>
        public static IMesh Triangulate(this ICollection<IPoint2D> points, int SteinerPoints = 0)
        {
            //TriangleNet.Geometry.IPolygon polygon = points.CreatePolygon();

            TriangleNet.Geometry.Polygon polygon = new TriangleNet.Geometry.Polygon(points.Count);
            TriangleNet.Geometry.Vertex[] verticies = points.Select((v, i) => new TriangleNet.Geometry.Vertex(v.X, v.Y)).ToArray();

            foreach (Vertex v in verticies)
            {
                polygon.Add(v);
            }

            ConstraintOptions constraints = new ConstraintOptions();
            constraints.ConformingDelaunay = SteinerPoints > 0;
            constraints.Convex = false;

            QualityOptions quality = new QualityOptions();
            quality.SteinerPoints = SteinerPoints;
            quality.MinimumAngle = SteinerPoints > 0 ? Math.PI / 6 : -1;

            IMesh mesh = polygon.Triangulate(constraints, quality);
            return mesh;
        }

        public static IMesh Triangulate(this GridPolygon input, ICollection<GridVector2> internalPoints)
        {
            return input.Triangulate(internalPoints: internalPoints.Select(p => p as IPoint2D).ToArray());
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
        /// Triangulate only the exterior rings of the polygons
        /// </summary>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public static TriangleNet.Meshing.IMesh TriangulateExterior(this GridPolygon[] Polygons)
        {
            GridPolygon[] ExteriorPolygons = Polygons.Select(p => new GridPolygon(p.ExteriorRing)).ToArray();

            return Triangulate(ExteriorPolygons); 
        }
        /// <summary>
        /// This function creates the triangulation of a set of polygons.  Internal and external borders are preserved. Where borders overlapped new
        /// points are added at the point of overlap.
        /// </summary>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public static TriangleNet.Meshing.IMesh Triangulate(this GridPolygon[] Polygons)
        {
            SortedSet<GridVector2> AddedPoints;
            //SortedSet<GridLineSegment> NonIntersectingSegments = Polygons.NonIntersectingSegments(true, out AddedPoints);

            Dictionary<GridVector2, List<PointIndex>> pointToPolyMap = GridPolygon.CreatePointToPolyMap(Polygons);
            List<GridVector2> points = pointToPolyMap.Keys.Distinct().ToList();

            TriangleNet.Geometry.Polygon polygon = new TriangleNet.Geometry.Polygon(points.Count);

            foreach (GridVector2 p in points)
            {
                polygon.Add(new Vertex(p.X, p.Y));
            }
            /*
            foreach (GridVector2 p in AddedPoints)
            {
                polygon.Add(new Vertex(p.X, p.Y));
            }
            
            //Add constraints for the non-intersecting line segments
            foreach (GridLineSegment line in NonIntersectingSegments)
            {
                Segment seg = new Segment(new Vertex(line.A.X, line.A.Y), new Vertex(line.B.X, line.B.Y));
                polygon.Add(seg, false);
            }*/
            
            //If there are not enough points to triangulate return null
            if (polygon.Points.Count < 3)
                return null;

            ConstraintOptions constraints = new ConstraintOptions();
            constraints.ConformingDelaunay = false;
            constraints.Convex = true;

            TriangleNet.Meshing.IMesh mesh = TriangleNet.Geometry.ExtensionMethods.Triangulate(polygon, constraints);
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

            for (int i = 0; i < points.Length; i++)
            {
                if (lookup.ContainsKey(points[i]))
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

        /// <summary>
        /// Construct the Voronoi domain for a set of shapes.
        /// </summary>
        /// <param name="Shapes"></param>
        /// <returns></returns>
        public static TriangleNet.Voronoi.VoronoiBase Voronoi(this IReadOnlyList<GridPolygon> Shapes)
        {
            List<TriangleNet.Geometry.Vertex> verts = new List<TriangleNet.Geometry.Vertex>();

            for (int i = 0; i < Shapes.Count; i++)
            {
                GridPolygon shape = Shapes[i];
                if (shape == null)
                    continue;

                GridVector2[] points = shape.ExteriorRing.EnsureOpenRing();
                verts.AddRange(points.Select(p =>
                {
                    var v = new TriangleNet.Geometry.Vertex(p.X, p.Y, i, 1);
                    v.Attributes[0] = i;
                    return v;
                }));
            }

            if (verts.Count >= 3)
            {
                var Voronoi = verts.Voronoi();
                return Voronoi;
            }

            return null;
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
