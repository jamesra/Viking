using Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TriangleNet;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriVertex = TriangleNet.Geometry.Vertex;
using VikingMeshing = Geometry.Meshing;
using VikingPolygon = Geometry.Polygon;

namespace TriangleNet
{
    public static class TriangleExtensions
    {
        public static Vector2 ToVector2(this TriVertex v) => new Vector2(v.X, v.Y);

        public static Vector2 ToVector2(this TriangleNet.Topology.DCEL.Vertex v) => new Vector2(v.X, v.Y);

        public static Vector3 ToVector3(this TriVertex v, double Z) => new Vector3(v.X, v.Y, Z);

        public static Vector3 ToVector3(this TriangleNet.Topology.DCEL.Vertex v, double Z) => new Vector3(v.X, v.Y, Z);

        public static List<LineSegment> ToLines(this TriangleNet.Topology.DCEL.DcelMesh mesh)
        {
            if (mesh is null)
                return null;

            List<LineSegment> listLines = [];
            //Create a map of TriVertex ID's to DRMesh ID's
            int[] IndexMap = [.. mesh.Vertices.Select(v => v.ID)];

            foreach (var e in mesh.Edges)
            {
                listLines.Add(new LineSegment(mesh.Vertices[e.P0].ToVector2(),
                                           mesh.Vertices[e.P1].ToVector2()));
            }

            return listLines;
        }

        public static VikingPolygon ToPolygon(this TriangleNet.Topology.DCEL.Face face)
        {
            if (face is null)
                return null;

            Vector2[] verts = [.. face.EnumerateEdges().Select(edge => edge.Origin.ToVector2())];

            VikingPolygon polygon = new(verts.EnsureClosedRing());
            return polygon;
        }

        public static List<VikingPolygon> ToPolygons(this TriangleNet.Topology.DCEL.DcelMesh mesh)
        {
            if (mesh is null)
                return null;

            List<VikingPolygon> listTriangles = [];
            return [.. mesh.Faces.Select(face => face.ToPolygon())];
        }

        public static List<LineSegment> ToLines(this TriangleNet.Meshing.IMesh mesh)
        {
            if (mesh is null)
                return null;

            SortedSet<LineSegment> listLines = [];
            //Create a map of TriVertex ID's to DRMesh ID's
            int[] IndexMap = [.. mesh.Vertices.Select(v => v.ID)];
            TriVertex[] verticies = [.. mesh.Vertices];

            foreach (var t in mesh.Triangles)
            {
                listLines.Add(new LineSegment(t.GetVertex(0).ToVector2(), t.GetVertex(1).ToVector2()));
                listLines.Add(new LineSegment(t.GetVertex(1).ToVector2(), t.GetVertex(2).ToVector2()));
                listLines.Add(new LineSegment(t.GetVertex(2).ToVector2(), t.GetVertex(0).ToVector2()));
            }

            return [.. listLines];
        }

        public static List<Triangle> ToTriangles(this TriangleNet.Meshing.IMesh mesh)
        {
            if (mesh is null)
                return null;

            List<Triangle> listTriangles = [];
            //Create a map of TriVertex ID's to DRMesh ID's
            int[] IndexMap = [.. mesh.Vertices.Select(v => v.ID)];
            TriVertex[] verticies = [.. mesh.Vertices];

            foreach (var tri in mesh.Triangles)
            {
                listTriangles.Add(new Triangle(tri.GetVertex(0).ToVector2(),
                                                   tri.GetVertex(1).ToVector2(),
                                                   tri.GetVertex(2).ToVector2()));
            }

            return listTriangles;
        }

        public static TriangleNet.Geometry.Polygon CreatePolygon(this IEnumerable<Vector2> Vertices, IEnumerable<Vector2[]> InteriorPolygons = null)
        {
            IPoint2D[] v = [.. Vertices.Select(p => p as IPoint2D)];
            IPoint2D[][] ip = null;
            if (InteriorPolygons != null)
                ip = [.. InteriorPolygons.Select(interiorPolygon => interiorPolygon.Select(p => p as IPoint2D).ToArray())];
            return CreatePolygon(v, ip);
        }

        public static TriangleNet.Geometry.Polygon CreatePolygon(this IEnumerable<IPoint2D> Vertices, IEnumerable<IPoint2D[]> InteriorPolygons = null)
        {
            TriangleNet.Geometry.Vertex[] points = [.. Vertices.Select((v, i) => new TriangleNet.Geometry.Vertex(v.X, v.Y))];
            TriangleNet.Geometry.Polygon poly = new(points.Length);

            TriangleNet.Geometry.Contour contour = new(points);

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
        public static void Append(this TriangleNet.Geometry.Polygon polygon, VikingPolygon other)
        {
            TriangleNet.Geometry.Contour contour = new(other.ExteriorRing.Select(p => new TriVertex(p.X, p.Y)));
            polygon.Add(contour);
        }

        /// <summary>
        /// Append the exterior ring to the polygon as new points with a contraint around the exterior ring
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="other"></param>
        public static void Append(this TriangleNet.Geometry.Polygon polygon, ICollection<Vector2> points)
        {
            points = points.EnsureOpenRing();

            foreach (TriVertex v in points.Select(p => new TriVertex(p.X, p.Y)))
            {
                polygon.Add(v);
            }
        }

        /// <summary>
        /// Append the exterior ring to the polygon as new points with a contraint around the exterior ring
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="other"></param>
        public static void AppendCountour(this TriangleNet.Geometry.Polygon polygon, ICollection<Vector2> points)
        {
            TriangleNet.Geometry.Contour contour = new(points.Select(p => new TriVertex(p.X, p.Y)));
            polygon.Add(contour, true);
        }


        public static TriangleNet.Geometry.Polygon CreatePolygon(this VikingPolygon input) => CreatePolygon(input.ExteriorRing, input.InteriorRings);

        public static TriangleNet.Geometry.Polygon CreatePolygon(this IPolygon2D input) => CreatePolygon(input.ExteriorRing, input.InteriorRings);

        public static TriangleNet.Geometry.Contour CreateContour(this ICollection<Vector2> Vertices)
        {
            TriangleNet.Geometry.Vertex[] points = [.. Vertices.Select((v, i) => new TriangleNet.Geometry.Vertex(v.X, v.Y))];
            TriangleNet.Geometry.Contour contour = new(points);

            return contour;
        }

        public static TriangleNet.Geometry.Contour CreateContour(this ICollection<IPoint2D> Vertices)
        {
            TriangleNet.Geometry.Vertex[] points = [.. Vertices.Select((v, i) => new TriangleNet.Geometry.Vertex(v.X, v.Y))];
            TriangleNet.Geometry.Contour contour = new(points);

            return contour;
        }

        public static IMesh Triangulate(this ICollection<Vector2> points, int SteinerPoints = 0) => Triangulate(points.Select(p => (IPoint2D)p).ToList(), SteinerPoints);

        /// <summary>
        /// Triangulate the polygon.
        /// </summary>
        /// <param name="input">Polygon to generate faces for</param>
        /// <param name="internalPoints">Additional points inside the polygon which should be included in the triangulation</param>
        /// <returns></returns>
        public static IMesh Triangulate(this ICollection<IPoint2D> points, int SteinerPoints = 0)
        {
            //TriangleNet.Geometry.IPolygon polygon = points.CreatePolygon();

            TriangleNet.Geometry.Polygon polygon = new(points.Count);
            TriangleNet.Geometry.Vertex[] verticies = [.. points.Select((v, i) => new TriangleNet.Geometry.Vertex(v.X, v.Y))];

            foreach (TriVertex v in verticies)
            {
                polygon.Add(v);
            }

            ConstraintOptions constraints = new()
            {
                ConformingDelaunay = SteinerPoints > 0,
                Convex = false
            };

            QualityOptions quality = new()
            {
                SteinerPoints = SteinerPoints,
                MinimumAngle = SteinerPoints > 0 ? Math.PI / 6 : -1
            };

            IMesh mesh = polygon.Triangulate(constraints, quality);
            return mesh;
        }

        public static IMesh Triangulate(this VikingPolygon input, ICollection<Vector2> internalPoints) => input.Triangulate(internalPoints: internalPoints.Select(p => p as IPoint2D).ToArray());

        /// <summary>
        /// Triangulate the polygon.
        /// </summary>
        /// <param name="input">Polygon to generate faces for</param>
        /// <param name="internalPoints">Additional points inside the polygon which should be included in the triangulation</param>
        /// <returns></returns>
        public static IMesh Triangulate(this VikingPolygon input, ICollection<IPoint2D> internalPoints = null, bool UseSteiner = true)
        {
            TriangleNet.Geometry.IPolygon polygon = input.CreatePolygon();

            if (internalPoints != null)
            {
                foreach (IPoint2D p in internalPoints)
                {
                    polygon.Add(new TriVertex(p.X, p.Y));
                }
            }

            ConstraintOptions constraints = new()
            {
                ConformingDelaunay = false,
                Convex = false
            };

            QualityOptions quality = new();
            if (UseSteiner)
                quality.SteinerPoints = (polygon.Points.Count / 2) + 1;

            IMesh mesh = polygon.Triangulate(constraints, quality);
            return mesh;
        }

        /// <summary>
        /// Experimental function (doesn't always work) to triangulate an existing mesh
        /// </summary>
        /// <param name="input_mesh"></param>
        /// <returns></returns>
        public static TriangleNet.Meshing.IMesh Triangulate(this VikingMeshing.Mesh3D input_mesh)
        {
            TriangleNet.Geometry.IPolygon fake_poly = new TriangleNet.Geometry.Polygon(input_mesh.Vertices.Count);

            GenericMesher mesher = new();

            List<TriVertex> tri_verts = [.. input_mesh.Vertices.Select(v => v.ToTriangleNetVertex())];

            foreach (TriVertex v in tri_verts)
            {
                fake_poly.Add(v);
            }

            List<Segment> tri_segments = [.. input_mesh.Edges.Values.Where(seg => input_mesh.Vertices[seg.A].Position.XY() != input_mesh.Vertices[seg.B].Position.XY()).
                                                                            Select(seg =>
                                                                            {
                                                                                TriVertex seg_v1 = input_mesh.Vertices[seg.A].ToTriangleNetVertex();
                                                                                TriVertex seg_v2 = input_mesh.Vertices[seg.B].ToTriangleNetVertex();

                                                                                Segment tri_seg = new(seg_v1, seg_v2);
                                                                                return tri_seg;
                                                                            })];


            foreach (ISegment seg in tri_segments)
            {
                fake_poly.Add(seg);
            }

            IMesh output_mesh = mesher.Triangulate(fake_poly);

            return output_mesh;
        }

        public static TriVertex ToTriangleNetVertex(this VikingMeshing.IVertex2D vert)
        {
            TriVertex out_v = new(vert.Position.X, vert.Position.Y)
            {
                ID = vert.Index
            };
            return out_v;
        }

        public static TriVertex ToTriangleNetVertex(this VikingMeshing.IVertex3D vert)
        {
            TriVertex out_v = new(vert.Position.X, vert.Position.Y)
            {
                ID = vert.Index
            };
            return out_v;
        }

        public static TriVertex ToTriangleNetVertex(this Vector2 vert, int ID)
        {
            TriVertex out_v = new(vert.X, vert.Y)
            {
                ID = ID
            };
            return out_v;
        }

        /// <summary>
        /// Triangulate only the exterior rings of the polygons
        /// </summary>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public static TriangleNet.Meshing.IMesh TriangulateExterior(this VikingPolygon[] Polygons)
        {
            VikingPolygon[] ExteriorPolygons = [.. Polygons.Select(p => new VikingPolygon(p.ExteriorRing))];

            return Triangulate(ExteriorPolygons);
        }

        /*
        /// <summary>
        /// This function creates the triangulation of a set of polygons.  Internal and external borders are preserved. Where borders overlapped new
        /// points are added at the point of overlap.
        /// </summary>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public static TriangleNet.Meshing.IMesh Triangulate(this VikingPolygon[] Polygons)
        {
            //When using this function with the bajaj mesh generator we must constrain the triangulation to 
            //include all segments because countour edges have already been added to the mesh.  If we do not
            //for inclusion of the contour edges in the triangulation it can produce edges that cross contours
            //which will cause obscure bugs downstream in the mesh generator.
            List<LineSegment> NonIntersectingSegments = Polygons.Segments();

            List<ISegment> input = new List<ISegment>();

            //Add constraints for the non-intersecting line segments
            foreach (LineSegment line in NonIntersectingSegments)
            {
                Segment seg = new Segment(new TriVertex(line.A.X, line.A.Y), new TriVertex(line.B.X, line.B.Y));
                //System.Diagnostics.Trace.WriteLine(string.Format("ADD SEGMENT {0}x {1}y - {2}x {3}y - ", line.A.X, line.A.Y, line.B.X, line.B.Y));

                //polygon.Add(seg, true);
                input.Add(seg);
            }

            //If there are not enough points to triangulate return null
            //if (polygon.Points.Count < 3)
            //    return null; 

            System.Diagnostics.Debug.Assert(false == NonIntersectingSegments.Any(s => NonIntersectingSegments.Any(ns => ns != s && ns.Intersects(s, EndpointsOnRingDoNotIntersect: true))));

            ConstraintOptions constraints = new ConstraintOptions();
            constraints.ConformingDelaunay = false;
            constraints.Convex = true;

            TriangleNet.Meshing.IMesh mesh = TriangleNet.Geometry.ExtensionMethods.Triangulate(input, constraints);

            return mesh;
        }
        */

        /// <summary>
        /// Note: This is where you left off to work on a constrained Delaunay 2D triangulation.  If that is working, use the function
        /// above and contrain the delauncy to solve the intermittant rendering bugs around corresponding verticies when dumping the
        /// large glail cell from RPC1.
        /// 
        /// This function creates the triangulation of a set of polygons.  Internal and external borders are preserved. Where borders overlapped new
        /// points are added at the point of overlap.
        /// </summary>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public static TriangleNet.Meshing.IMesh Triangulate(this VikingPolygon[] Polygons)
        {
            //SortedSet<Vector2> AddedPoints;
            //SortedSet<LineSegment> NonIntersectingSegments = Polygons.NonIntersectingSegments(true, out AddedPoints);

            var pointToPolyMap = Polygons.CreatePointToPolyMap();
            List<Vector2> points = [.. pointToPolyMap.Keys.Distinct()];

            TriangleNet.Geometry.Polygon polygon = new(points.Count);

            foreach (Vector2 p in points)
            {
                polygon.Add(new TriVertex(p.X, p.Y));
                //System.Diagnostics.Trace.WriteLine(string.Format("ADD POINT {0}x {1}y", p.X, p.Y));
            }

            //If there are not enough points to triangulate return null
            if (polygon.Points.Count < 3)
                return null;

            ConstraintOptions constraints = new()
            {
                ConformingDelaunay = false,
                Convex = true
            };

            TriangleNet.Meshing.IMesh mesh = TriangleNet.Geometry.ExtensionMethods.Triangulate(polygon, constraints);

            return mesh;
        }

        /// <summary>
        /// Return the indicies for the array of points in the mesh.  If the point is not in the mesh return -1
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="points"></param>
        /// <returns></returns>
        public static int[] IndiciesForPointsXY(this IMesh mesh, Vector2[] points)
        {
            points = points.EnsureOpenRing();

            Vector2[] mesh_points = [.. mesh.Vertices.Select(v => new Vector2(v.X, v.Y))];

            ///Create a map of position to index
            Dictionary<Vector2, int> lookup = mesh_points.Select((p, i) => i).ToDictionary(i => mesh_points[i]);

            int[] output_map = new int[points.Length];

            for (int i = 0; i < points.Length; i++)
            {
                output_map[i] = lookup.ContainsKey(points[i]) ? lookup[points[i]] : -1;
            }

            return output_map;
        }

        public static IMesh Triangulate(this IPolygon2D input)
        {
            TriangleNet.Geometry.IPolygon polygon = input.CreatePolygon();

            ConstraintOptions constraints = new()
            {
                ConformingDelaunay = false,
                Convex = false
            };

            QualityOptions quality = new()
            {
                SteinerPoints = (polygon.Points.Count / 2) + 1
            };

            IMesh mesh = polygon.Triangulate(constraints, quality);
            return mesh;
        }

        public static TriangleNet.Voronoi.VoronoiBase Voronoi(this ICollection<IPoint2D> points) => points.Select(p => new Vector2(p.X, p.Y)).ToList().Voronoi();

        public static TriangleNet.Voronoi.VoronoiBase Voronoi(this ICollection<Vector2> input)
        {
            TriangleNet.Geometry.Vertex[] verticies = [.. input.Select(p => new TriVertex(p.X, p.Y))];

            return verticies.Voronoi();
        }

        /// <summary>
        /// Construct the Voronoi domain for a set of shapes.
        /// </summary>
        /// <param name="Shapes"></param>
        /// <returns></returns>
        public static TriangleNet.Voronoi.VoronoiBase Voronoi(this IReadOnlyList<VikingPolygon> Shapes)
        {
            List<TriangleNet.Geometry.Vertex> verts = [];

            for (int i = 0; i < Shapes.Count; i++)
            {
                VikingPolygon shape = Shapes[i];
                if (shape is null)
                    continue;

                Vector2[] points = shape.ExteriorRing.EnsureOpenRing();
                verts.AddRange(points.Select(p =>
                {
                    TriVertex v = new(p.X, p.Y, i, 1);
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

        public static TriangleNet.Voronoi.VoronoiBase Voronoi(this ICollection<TriVertex> verticies)
        {
            TriangleNet.Geometry.Polygon polygon = new();
            foreach (TriVertex v in verticies)
            {
                polygon.Add(v);
            }

            ConstraintOptions constraints = new()
            {
                ConformingDelaunay = false,
                Convex = false
            };

            QualityOptions quality = new()
            {
                SteinerPoints = (polygon.Points.Count / 2) + 1
            };
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
                    Trace.WriteLine(string.Format("Exception performing voronoi {0}", ex));
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
