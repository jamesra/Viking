using Geometry;
using Geometry.Meshing;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using TriangleNet;

namespace MonogameTestbed
{

    public class BoundaryFinder
    { 
        /// <summary>
        /// Approximate the boundary that is equidistant from all shapes
        /// </summary>
        /// <param name="shapes"></param>
        /// <returns></returns>
        static public List<GridLineSegment> DetermineBoundary(GridPolygon[] shapes)
        {
            TriangleNet.Meshing.IMesh triangulationMesh = null;
            try
            {
                triangulationMesh = shapes.Triangulate();
            }
            catch(ArgumentException)
            {
                return new List<GridLineSegment>();
            }

            

            //List<GridLineSegment> LinesBetweenShapes = SelectLinesBetweenShapes(triangulationMesh, shapes);

            List<GridTriangle> TrianglesBetweenShapes = SelectTrianglesBetweenShapes(triangulationMesh, shapes);

            TriangleNet.Voronoi.VoronoiBase voronoi = shapes.Voronoi();
            if (voronoi == null)
                return new List<GridLineSegment>(0);

            //List<GridLineSegment> listVoronoiBetweenShapes = StripNonBoundaryLines(voronoi, shapes);

            MedialAxisGraph graph = BuildGraphFromTriangles(TrianglesBetweenShapes.ToArray(), shapes);
            
            /*
            //Find all the intersections between the remaining Delaunay and Voronoi lines  
            //DynamicRenderMesh mesh = CreateMesh(KnownGoodLines, out PointToIndex);
            BorderGraph graph = CreateGraph(listVoronoiBetweenShapes, shapes);

            //Look at each edge in the graph.  Split each edge at every intersection with a delaunayLine
            foreach (BorderEdge edge in graph.Edges.Keys.ToArray())
            {
                AddVertexAtDelaunayIntercepts(graph, edge, LinesBetweenShapes, shapes);
            }

            

            */
            /*
            //OK, the graph is populated.  Walk the edges and find any intersections with shapes
            //When we intersect with a shape delete the edge but record the point outside of the shapes.
            //The search all paths from the node inside the shape for the first nodes outside of the shape.
            //Create a new edge between the two external points.

            //Make sure our starting point is outside any of the shapes.
            //int iSearchStart = FindStartForBoundarySearch(mesh, shapes);

            //TODO: Find the nodes with only one edge and use them as a starting point
            List<BorderVertex> StartCandidates = graph.Nodes.Where(n => !n.Value.InsidePolygon && n.Value.Edges.Count == 1).Select(n => n.Value).ToList();

            while (StartCandidates.Count > 0)
            {
                BorderVertex vert = StartCandidates[0];
                StartCandidates.RemoveAt(0);

                foreach (GridVector2 TargetNode in vert.Edges.Keys.ToArray())
                {
                    GridLineSegment line = new GridLineSegment(vert.Key, TargetNode);
                    if (!IsValidBorderLine(line, shapes))
                    {
                        MoveBorder(graph, vert.Edges[TargetNode].First(), vert, graph.Nodes[TargetNode], shapes);
                    }
                }
            }

            List<BorderEdge> KnownBadEdges = graph.Edges.Where(e => !IsValidBorderLine(e.Value.Line, shapes)).Select(e => e.Value).ToList();
            foreach(BorderEdge badEdge in KnownBadEdges)
            {
                graph.RemoveEdge(badEdge);
            }

            //Remove any edges that are entirely within a shape
            List<GridVector2> KnownBad = graph.Nodes.Where(n => n.Value.InsidePolygon).Select(v => v.Key).ToList();
            foreach (GridVector2 v in KnownBad)
            {
                graph.RemoveNode(v);
            }

            //Remove any nodes with no edges
            KnownBad = graph.Nodes.Where(n => n.Value.Edges.Count == 0).Select(v => v.Key).ToList();
            foreach (GridVector2 v in KnownBad)
            {
                graph.RemoveNode(v);
            }
            */

            return graph.Edges.Select(edge => edge.Value.Line).ToList();
        }

        private static bool LineConnectsShapes(GridLineSegment line, Dictionary<GridVector2, int> PointToShapeIndex)
        {
            return PointToShapeIndex[line.A] != PointToShapeIndex[line.B];
        }

        private static List<IEdge> LinesOfFaceBetweenShapes(IReadOnlyMesh2D<IVertex2D> mesh, IFace face, Dictionary<GridVector2, int> PointToShapeIndex)
        {
            List<IEdge> edges = new List<IEdge>(); 
            foreach(var edge in face.Edges)
            {
                GridLineSegment line = mesh.ToGridLineSegment(edge);
                if(LineConnectsShapes(line, PointToShapeIndex))
                {
                    edges.Add(mesh.Edges[edge]);
                }
            }

            return edges;
        }

        private static MedialAxisVertex GetOrAddVertex(MedialAxisGraph graph, GridVector2 p)
        {
            if (!graph.ContainsKey(p))
            {
                MedialAxisVertex node = new MedialAxisVertex(p);
                graph.AddNode(node);
            }

            return graph[p];
        }

        private static MedialAxisVertex GetOrAddLineBisectorVertex(MedialAxisGraph graph, GridLineSegment line)
        {
            GridVector2 midpoint = line.Bisect();
            if (!graph.ContainsKey(midpoint))
            { 
                MedialAxisVertex node = new MedialAxisVertex(midpoint);
                graph.AddNode(node);
            }

            return graph[midpoint];
        }

        private static MedialAxisGraph BuildGraphFromTriangles(GridTriangle[] triangles, GridPolygon[] shapes)
        {
            MedialAxisGraph graph = new MedialAxisGraph();

            //Create an index map of points 
            Dictionary<GridVector2, SortedSet<int>> PointToTrianglesIndex = CreatePointToConnectedTrianglesIndexLookup(triangles);
            Dictionary<GridVector2, int> PointToShapeIndex = CreatePointToShapeIndexLookup(shapes);

            Mesh2D mesh = triangles.ToDynamicRenderMesh();

            foreach(var edge in mesh.Edges.Values)
            {
                //Create a vertex at the edge midpoint
                GridLineSegment line = mesh.ToGridLineSegment(edge);

                //If the line is between two different shapes we add a node to the graph
                if (LineConnectsShapes(line, PointToShapeIndex))
                {
                    MedialAxisVertex node = GetOrAddLineBisectorVertex(graph, line);

                    //Check the faces of this edge for lines to connect to.
                    foreach (var AdjacentEdge in edge.Faces.SelectMany(f => LinesOfFaceBetweenShapes(mesh, f, PointToShapeIndex)).Where(foundEdge => foundEdge != edge))
                    {
                        GridLineSegment ConnectedLine = mesh.ToGridLineSegment(AdjacentEdge);
                        MedialAxisVertex otherNode = GetOrAddLineBisectorVertex(graph, ConnectedLine);

                        MedialAxisEdge borderEdge = new MedialAxisEdge(node.Key, otherNode.Key);
                        if(!graph.Edges.ContainsKey(borderEdge))
                            graph.AddEdge(borderEdge);
                    }
                }
            }

            return graph; 
        } 

        private static bool IsValidBorderLine(GridLineSegment line, GridPolygon[] shapes)
        {
            return !shapes.Any(shape => shape.Intersects(line));
        }
         
        /// <summary>
        /// This function creates the triangulation of a set of polygons returning the set of edges between polygons and the external polygon borders.
        /// This function is undefined if the input polygons overlap
        /// </summary>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        internal static TriangleNet.Meshing.IMesh TriangulatePolygons(GridPolygon[] Polygons)
        {
            if (Polygons.AnyIntersect())
                throw new ArgumentException("TriangulatePolygons expects non overlapping polygons as input");

            GridPolygon EntireSetConvexHull = Polygons.ConvexHull();
            if (EntireSetConvexHull == null)
                return null; 

            TriangleNet.Geometry.Polygon EntireSetConvexHullPoly = TriangleExtensions.CreatePolygon(EntireSetConvexHull);

            foreach (GridVector2[] points in Polygons.Select(poly => poly.ExteriorRing))
            {
                if (points == null || points.Length < 4)
                    continue;

                //Record the borders of each polygon in the aggregate polygon.  These restrict the delaunay triangulation to keep those edges
                EntireSetConvexHullPoly.AppendCountour(points);
            }

            //If there are not enough points to triangulate return null
            if (EntireSetConvexHullPoly.Count < 3)
                return null;

            TriangleNet.Meshing.IMesh mesh = TriangleNet.Geometry.ExtensionMethods.Triangulate(EntireSetConvexHullPoly);
            return mesh;
        }

        /// <summary>
        /// Given a triangulation between a set of polygons, remove all lines that are not between verticies from seperate shapes.  
        /// Lines that intersect shapes are also removed
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        private static List<GridLineSegment> SelectLinesBetweenShapes(TriangleNet.Meshing.IMesh mesh, GridPolygon[] Polygons)
        {
            if (mesh == null)
                return null;

            List<GridLineSegment> lines = mesh.ToLines();
            if (lines == null)
                return null;

            if (lines.Count == 0)
                return new List<GridLineSegment>();

            //Create an index map of points

            Dictionary<GridVector2, int> PointToShapeIndex = CreatePointToShapeIndexLookup(Polygons);

            for (int i = lines.Count - 1; i >= 0; i--)
            {
                GridLineSegment line = lines[i];
                if (!(PointToShapeIndex.ContainsKey(line.A) && PointToShapeIndex.ContainsKey(line.B)))
                    continue;

                int HullA = PointToShapeIndex[line.A];
                int HullB = PointToShapeIndex[line.B];
                if (HullA == HullB)
                    lines.RemoveAt(i);
            }

            return lines;
        }

        /// <summary>
        /// Given a triangulation between a set of polygons, remove all lines that are not between verticies from seperate shapes.  
        /// Lines that intersect shapes are also removed
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        private static List<GridTriangle> SelectTrianglesBetweenShapes(TriangleNet.Meshing.IMesh mesh, GridPolygon[] Polygons)
        {
            if (mesh == null)
                return null;

            List<GridTriangle> triangles = mesh.ToTriangles();
            if (triangles == null)
                return null;

            if (triangles.Count == 0)
                return new List<GridTriangle>();

            //Create an index map of points 
            Dictionary<GridVector2, int> PointToShapeIndex = CreatePointToShapeIndexLookup(Polygons);

            for (int i = triangles.Count - 1; i >= 0; i--)
            {
                GridTriangle tri = triangles[i];
                if (!tri.Points.All(p => PointToShapeIndex.ContainsKey(p)))
                    continue;

                int[] ShapeIndicies = tri.Points.Select(p => PointToShapeIndex[p]).Distinct().ToArray();

                //If the verticies of the triangle do not connect two or more shapes remove it from the list
                if (ShapeIndicies.Length < 2)
                    triangles.RemoveAt(i);
            }

            return triangles;
        }


        /// <summary>
        /// Return a dictionary mapped each vertex to the index of the shape in the array
        /// </summary>
        /// <param name="Shapes"></param>
        /// <returns></returns>
        private static Dictionary<GridVector2, int> CreatePointToShapeIndexLookup(GridPolygon[] Shapes)
        {
            Dictionary<GridVector2, int> PointToShapeIndex = new Dictionary<GridVector2, int>();
            //Create an index map of points
            List<GridVector2> listPoints = new List<GridVector2>();
            List<int> listIndicies = new List<int>();

            for (int iShape = 0; iShape < Shapes.Length; iShape++)
            {
                if (Shapes[iShape] == null)
                    continue;

                GridVector2[] points = Shapes[iShape].ExteriorRing;
                if (points == null || points.Length == 0)
                    continue;

                points = points.EnsureOpenRing();

                foreach (GridVector2 point in points)
                {
                    PointToShapeIndex[point] = iShape;
                }
            }

            return PointToShapeIndex;
        }


        /// <summary>
        /// Return a dictionary mapped each vertex to the index of the triangles using that vertex in the passed array
        /// </summary>
        /// <param name="Shapes"></param>
        /// <returns></returns>
        private static Dictionary<GridVector2, SortedSet<int>> CreatePointToConnectedTrianglesIndexLookup(GridTriangle[] Shapes)
        {
            Dictionary<GridVector2, SortedSet<int>> PointToShapeIndex = new Dictionary<GridVector2, SortedSet<int>>();
            //Create an index map of points
            List<GridVector2> listPoints = new List<GridVector2>();
            List<int> listIndicies = new List<int>();

            for (int iShape = 0; iShape < Shapes.Length; iShape++)
            {
                if (Shapes[iShape] == null)
                    continue;

                GridVector2[] points = Shapes[iShape].Points;
                if (points == null || points.Length == 0)
                    continue;
                  
                foreach (GridVector2 point in points)
                {
                    if(!PointToShapeIndex.ContainsKey(point))
                    {
                        PointToShapeIndex[point] = new SortedSet<int>();
                    }

                    PointToShapeIndex[point].Add(iShape);
                }
            }

            return PointToShapeIndex;
        }
        
        /*
        private static void MoveBorder(MedialAxisGraph graph, MedialAxisEdge edge, MedialAxisVertex StartingVertex, MedialAxisVertex InvalidVertex, GridPolygon[] shapes)
        {
            //Remove the edge that we know is invalid

            //Find all verticies the invalid node can reach that are valid
            SortedSet<GridVector2> validDestinations = MedialAxisGraph.FindReachableMatches(graph, StartingVertex.Key,
                 v => {
                     if (v == StartingVertex || v == InvalidVertex || v.InsidePolygon)
                         return false;

                     GridLineSegment line = new GridLineSegment(StartingVertex.Key, v.Key);
                     return !shapes.Any(shape => shape.Intersects(line));
                     });
            if (validDestinations == null)
                return;

            bool EdgeAdded = false; 

            //Create lines between our source and destination.  If they do not intersect any shapes create a new edge
            foreach(GridVector2 validTarget in validDestinations)
            {
                GridLineSegment newLine = new GridLineSegment(StartingVertex.Key, validTarget);
                IList<GridVector2> path = MedialAxisGraph.ShortestPath(graph, StartingVertex.Key, validTarget);

                //Make sure there is not a valid node further down the path.  This would create a duplicate or an extra branch in the border
                if (IsValidBorderLine(new GridLineSegment(StartingVertex.Key, path[1]), shapes))
                    continue;

                if(!shapes.Any(shape => shape.Intersects(newLine)))
                {
                    MedialAxisEdge newEdge = new MedialAxisEdge(StartingVertex.Key, validTarget);
                    if (!graph.Edges.ContainsKey(newEdge))
                    {
                        graph.AddEdge(newEdge);
                        EdgeAdded = true;
                    }
                }
            }

            if (EdgeAdded)
                graph.RemoveEdge(edge);

        }
        */

        /// <summary>
        /// We are checking the edge in the mesh to determine if it crosses a Delaunay lines.
        /// Wherever it crosses a line we add a vertex and create two new edges.
        /// We then continue tracing a line
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="edgeToTest"></param>
        /// <param name="linesBetweenShapes"></param>
        /// <param name="LineOrigin"></param>
        private static void AddVertexAtDelaunayIntercepts(MedialAxisGraph graph, MedialAxisEdge edgeToTest, List<GridLineSegment> linesBetweenShapes, GridPolygon[] shapes)
        {
            GridLineSegment boundaryLine = edgeToTest.Line;
             
            GridVector2[] delaunayIntersections = IntersectionPointsForLines(boundaryLine, linesBetweenShapes, out GridLineSegment[]  intersectedDelaunayLines);

            if (delaunayIntersections.Length == 0)
            {
                return;
            }
            else
            {
                double[] delaunayDistances = delaunayIntersections.Select(intersection => GridVector2.Distance(boundaryLine.A, intersection)).ToArray();
                double NearestDelaunay = delaunayDistances.Min();

                //OK, add a vertex at the intersection.  Split the edge into two parts.
                int iIntersection = Array.FindIndex(delaunayDistances, d => d == NearestDelaunay);

                GridVector2 IntersectionPoint = delaunayIntersections[iIntersection];

                graph.RemoveEdge(edgeToTest);
                
                graph.AddNode(new MedialAxisVertex(IntersectionPoint)); //No need to check if the point is inside a shape because by definition a line between shapes is outside the shapes

                //Create a new vertex
                MedialAxisEdge sourceToDelaunay = new MedialAxisEdge(boundaryLine.A, IntersectionPoint);
                graph.AddEdge(sourceToDelaunay);
                MedialAxisEdge DelaunayToTarget = new MedialAxisEdge(IntersectionPoint, boundaryLine.B);
                graph.AddEdge(DelaunayToTarget);

                //Continue searching down the line for more intercepts
                //Create a copy of the Delaunay Lines and remove the line we intersected with.  This prevents us from intersecting with that line again when we test the next segment we are making.
                List<GridLineSegment> updatedLinesBetweenShapes = linesBetweenShapes.ToList();
                updatedLinesBetweenShapes.Remove(intersectedDelaunayLines[iIntersection]);

                AddVertexAtDelaunayIntercepts(graph, DelaunayToTarget, updatedLinesBetweenShapes, shapes);
            }
        }
        
        /// <summary>
        /// Return an array of intersection points along the line to test with the provided array of lines
        /// </summary>
        /// <param name="testLine">Line to test</param>
        /// <param name="lineset">Set of lines we are looking for intersections with</param>
        /// <param name="intersectingLines">An array of the same length as the return value containing the lines that were intersected</param>
        /// <returns></returns>
        private static GridVector2[] IntersectionPointsForLines(GridLineSegment testLine, ICollection<GridLineSegment> lineset, out GridLineSegment[] intersectingLines)
        {
            intersectingLines = lineset.Where(Line => Line.Intersects(testLine)).ToArray();

            GridVector2[] intersections = intersectingLines.Select(Line =>
            {
                Line.Intersects(testLine, out GridVector2 intersection);
                return intersection;
            }).ToArray();

            return intersections;
        }

        /// <summary>
        /// Remove edges of the voronoi graph that do not divide verticies from different shapes.
        /// </summary>
        /// <param name="voronoi"></param>
        /// <returns></returns>
        internal static List<GridLineSegment> StripNonBoundaryLines(TriangleNet.Voronoi.VoronoiBase voronoi, GridPolygon[] shapes)
        {
            if (voronoi == null)
                return null;

            //Build a set of LineSegments
            List<GridLineSegment> lines = new List<GridLineSegment>();

            Dictionary<GridVector2, int> PointToShapeIndex = CreatePointToShapeIndexLookup(shapes.Select(s => s.ExteriorRing).ToList());

            foreach (TriangleNet.Topology.DCEL.HalfEdge halfEdge in voronoi.HalfEdges)
            {
                GridVector2 FaceA = new GridVector2(halfEdge.Face.generator.X,
                                                    halfEdge.Face.generator.Y);
                GridVector2 FaceB = new GridVector2(halfEdge.Twin.Face.generator.X,
                                                    halfEdge.Twin.Face.generator.Y);

                if (!(PointToShapeIndex.ContainsKey(FaceA) && PointToShapeIndex.ContainsKey(FaceB)))
                    continue;

                if (PointToShapeIndex[FaceA] != PointToShapeIndex[FaceB])
                {
                    GridLineSegment line = new GridLineSegment(halfEdge.Origin.ToGridVector2(),
                                                  halfEdge.Twin.Origin.ToGridVector2());
                    if (!lines.Contains(line))
                        lines.Add(line);
                }
            }

            return lines;
        }

        private static int FindStartForBoundarySearch(Mesh3D mesh, GridPolygon[] shapes)
        {
            IVertex3D vert = mesh.Verticies.First(v => shapes.All(shape => !shape.Contains(v.Position.XY())));
            return vert.Index;
            //return mesh.Verticies.TIndexOf(vert);
        }
        
        private static Dictionary<GridVector2, int> CreatePointToShapeIndexLookup(List<GridVector2[]> shapeVerticies)
        {
            Dictionary<GridVector2, int> PointToShapeIndex = new Dictionary<GridVector2, int>();
            //Create an index map of points
            List<GridVector2> listPoints = new List<GridVector2>();
            List<int> listIndicies = new List<int>();

            for (int iShape = 0; iShape < shapeVerticies.Count; iShape++)
            {
                GridVector2[] points = shapeVerticies[iShape];
                if (points == null || points.Length == 0)
                    continue;

                points = shapeVerticies[iShape].EnsureOpenRing();

                foreach (GridVector2 point in points)
                {
                    PointToShapeIndex[point] = iShape;
                }
            }

            return PointToShapeIndex;
        }
        
        /*
        private static MedialAxisGraph CreateGraph(List<GridLineSegment> KnownGoodLines, GridPolygon[] shapes)
        {
            MedialAxisGraph graph = new MorphologyMesh.MedialAxisGraph();

            foreach (var line in KnownGoodLines)
            {
                if(!graph.Nodes.ContainsKey(line.A))
                {
                    graph.AddNode(new MedialAxisVertex(line.A, shapes.Any(shape => shape.Contains(line.A))));
                }

                if(!graph.Nodes.ContainsKey(line.B))
                {
                    graph.AddNode(new MedialAxisVertex(line.B, shapes.Any(shape => shape.Contains(line.B))));
                }

                graph.AddEdge(new MedialAxisEdge(line.A, line.B));
            }

            return graph;
        }*/
    }
}
