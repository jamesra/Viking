using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    public class MedialAxisEdge(GridVector2 SourceNode, GridVector2 TargetNode) : GraphLib.Edge<GridVector2>(SourceNode, TargetNode, false)
    {
        public GridLineSegment Line => new(this.SourceNodeKey, this.TargetNodeKey);
    }

    public class MedialAxisVertex(GridVector2 k) : GraphLib.Node<GridVector2, MedialAxisEdge>(k)
    {
        public override string ToString() => Key.ToString();
    }

    public class MedialAxisGraph : GraphLib.Graph<GridVector2, MedialAxisVertex, MedialAxisEdge>
    {
        public GridVector2 FindStartForBoundarySearch(GridPolygon[] shapes) => Nodes.First(v => shapes.All(shape => !shape.Contains(v.Key))).Key;

        public GridLineSegment[] Segments => [.. this.Edges.Select(edge => edge.Value.Line)];

        public GridVector2[] Points => [.. this.Nodes.Select(n => n.Key)];

        /// <summary>
        /// Returns a copy of the graph with all nodes translated by the specified vector.
        /// </summary>
        /// <param name="vector">The translation vector to apply to all nodes</param>
        /// <returns>A new MedialAxisGraph with translated nodes and edges</returns>
        public MedialAxisGraph Translate(GridVector2 vector)
        {
            MedialAxisGraph translatedGraph = new();

            // Add all translated nodes
            foreach (var node in this.Nodes)
            {
                GridVector2 translatedPosition = node.Key + vector;
                translatedGraph.AddNode(new MedialAxisVertex(translatedPosition));
            }

            // Add all edges with translated endpoints
            foreach (var edge in this.Edges.Values)
            {
                GridVector2 translatedSource = edge.SourceNodeKey + vector;
                GridVector2 translatedTarget = edge.TargetNodeKey + vector;
                translatedGraph.AddEdge(new MedialAxisEdge(translatedSource, translatedTarget));
            }

            return translatedGraph;
        }

    }

    public static class MedialAxisFinder
    {
        /// <summary>
        /// Approximate the boundary that is equidistant from all shapes
        /// </summary>
        /// <param name="shape">The polygon shape to calculate the medial axis for</param>
        /// <returns></returns>
        public static MedialAxisGraph ApproximateMedialAxis(GridPolygon shape)
        {
            TriangulationMesh<IVertex2D<PolygonIndex>> mesh;
            var centroid = shape.Centroid;
            try
            {

                mesh = shape.Triangulate();
                //Triangulate will translate the verticies to the centroid to avoid floating point rounding errors. 
                //We will correct the medial axis verticies to match the input shape later. 
            }
            catch (ArgumentException)
            {
                return new MedialAxisGraph();
            }

            //List<GridLineSegment> LinesBetweenShapes = SelectLinesBetweenShapes(triangulationMesh, shapes);

            //List<GridTriangle> triangles = triangulationMesh.ToTriangles();


            MedialAxisGraph graph;
            graph = BuildImprovedGraphFromMesh2D(mesh, centroid == GridVector2.Zero ? shape : shape.Translate(-centroid));

            //Translate the medial axis graph back to the shape centroid if necessary
            if (centroid == GridVector2.Zero)
                return graph;
            else
                return graph.Translate(centroid);
        }

        /// <summary>
        /// Approximate the medial axis using an improved circumcenter-based algorithm.
        /// This method uses the mathematically correct approach based on the Voronoi dual of the Delaunay triangulation.
        /// Triangle circumcenters are used as medial axis vertices, which are equidistant from all three triangle vertices.
        /// </summary>
        /// <param name="shape">The polygon shape to calculate the medial axis for</param>
        /// <returns>A medial axis graph with vertices at triangle circumcenters</returns>
        public static MedialAxisGraph ApproximateMedialAxisImproved(GridPolygon shape)
        {
            TriangulationMesh<IVertex2D<PolygonIndex>> mesh;
            var centroid = shape.Centroid;
            try
            {
                mesh = shape.Triangulate();
                //Triangulate will translate the vertices to the centroid to avoid floating point rounding errors. 
                //We will correct the medial axis vertices to match the input shape later. 
            }
            catch (ArgumentException)
            {
                return new MedialAxisGraph();
            }

            MedialAxisGraph graph;
            graph = BuildImprovedGraphFromMesh2D(mesh, centroid == GridVector2.Zero ? shape : shape.Translate(-centroid));

            //Translate the medial axis graph back to the shape centroid if necessary
            if (centroid == GridVector2.Zero)
                return graph;
            else
                return graph.Translate(centroid);
        }

        private static MedialAxisGraph BuildGraphFromTriangles(GridTriangle[] triangles, GridPolygon boundary)
        {

            //Create an index map of points 
            //Dictionary<GridVector2, SortedSet<int>> PointToTrianglesIndex = CreatePointToConnectedTrianglesIndexLookup(triangles);

            Mesh2D mesh = triangles.ToDynamicRenderMesh();
            return BuildGraphFromMesh2D(mesh, boundary);
        }

        /// <summary>
        /// Converts a triangulation of a polygon into an approximated medial axis graph. 
        /// This is 
        /// 
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="boundary"></param>
        /// <returns></returns>
        private static MedialAxisGraph BuildGraphFromMesh2D(IReadOnlyMesh2D<IVertex2D> mesh, GridPolygon boundary)
        {
            MedialAxisGraph graph = new();

            foreach (var edge in mesh.Edges.Values)
            {
                //Create a vertex at the edge midpoint
                GridLineSegment line = mesh.ToGridLineSegment(edge);

                //If the line is not part of the polygons outer or inner ring and falls within the polygon (In rare edge cases it can be outside the polygon even though it should have been trimmed by the triangulation) we should add it to the graph.
                if (false == boundary.IsExteriorOrInteriorSegment(line) && boundary.GetRelation(line.Bisect()) == ShapeRelation.CONTAINED)
                {
                    MedialAxisVertex node = GetOrAddLineBisectorVertex(graph, line);
                    System.Diagnostics.Debug.Assert(boundary.GetRelation(node.Key) == ShapeRelation.CONTAINED, "Medial Axis approximate vertex must be within polygonal boundary");

                    foreach (IFace AdjacentFace in edge.Faces)
                    {
                        MedialAxisVertex otherNode = null;

                        List<IEdgeKey> edgeCandidates = [.. AdjacentFace.Edges.Where(e => e.Equals(edge) == false && boundary.IsExteriorOrInteriorSegment(mesh.ToGridLineSegment(e)) == false)];
                        if (edgeCandidates.Count == 1)
                        {
                            GridLineSegment ConnectedLine = mesh.ToGridLineSegment(edgeCandidates.First());
                            GridVector2 midpoint = ConnectedLine.Bisect();
                            GridLineSegment ProposedMedialLine = new(node.Key, midpoint);
                            if (boundary.Intersects(ProposedMedialLine) == false && boundary.GetRelation(midpoint) == ShapeRelation.CONTAINED) //Checking for containment handles a rare edge case
                            {
                                otherNode = GetOrAddLineBisectorVertex(graph, ConnectedLine);
                                System.Diagnostics.Debug.Assert(boundary.GetRelation(otherNode.Key) == ShapeRelation.CONTAINED, "Medial Axis approximate vertex must be within polygonal boundary");
                            }
                            else
                            {
                                GridTriangle tri = new([.. mesh[AdjacentFace.iVerts].Select(v => v.Position)]);
                                //GridVector2 face_centroid = mesh.GetCentroid(AdjacentFace);
                                GridVector2 face_centroid = tri.Centroid;
                                otherNode = GetOrAddVertex(graph, face_centroid);
                                System.Diagnostics.Debug.Assert(boundary.GetRelation(face_centroid) == ShapeRelation.CONTAINED, "Medial Axis approximate vertex must be within polygonal boundary");
                            }
                        }
                        else if (edgeCandidates.Count == 2 || edgeCandidates.Count == 0) ////All edges of the face are part of the medial axis.  Add a vertex at the centroid and connect them all to the centroid
                        {
                            GridTriangle tri = new([.. mesh[AdjacentFace.iVerts].Select(v => v.Position)]);
                            //GridVector2 face_centroid = mesh.GetCentroid(AdjacentFace);
                            GridVector2 face_centroid = tri.Centroid;
                            //GridVector2 face_centroid = mesh.GetCentroid(AdjacentFace);
                            otherNode = GetOrAddVertex(graph, face_centroid);
                            System.Diagnostics.Debug.Assert(boundary.GetRelation(face_centroid) == ShapeRelation.CONTAINED, "Medial Axis approximate vertex must be within polygonal boundary");
                        }

                        if (otherNode != null)
                        {
                            MedialAxisEdge e = new(node.Key, otherNode.Key);
                            if (!graph.Edges.ContainsKey(e))
                                graph.AddEdge(e);
                        }
                    }

                    /*
                    //Check the faces of this edge for lines to connect to.
                    foreach (var AdjacentEdge in edge.Faces.SelectMany(f => f.Edges.Where(e => e != edge && boundary.IsExteriorOrInteriorSegment(mesh.ToSegment(e)) == false)))
                    {
                        GridLineSegment ConnectedLine = mesh.ToSegment(AdjacentEdge);
                        BorderVertex otherNode = GetOrAddLineBisectorVertex(graph, ConnectedLine);

                        BorderEdge borderEdge = new BorderEdge(node.Key, otherNode.Key);
                        if (!graph.Edges.ContainsKey(borderEdge))
                            graph.AddEdge(borderEdge);
                    }*/
                }
            }

            return graph;
        }

        /// <summary>
        /// Converts a triangulation of a polygon into an improved medial axis graph using triangle circumcenters.
        /// This method implements the mathematically correct approach using the Voronoi dual of the Delaunay triangulation.
        /// Circumcenters of triangles are used as medial axis vertices, and edges connect circumcenters of adjacent triangles.
        /// </summary>
        /// <param name="mesh">The triangulated mesh</param>
        /// <param name="boundary">The polygon boundary to constrain the medial axis</param>
        /// <returns>A medial axis graph with vertices at triangle circumcenters</returns>
        private static MedialAxisGraph BuildImprovedGraphFromMesh2D(IReadOnlyMesh2D<IVertex2D> mesh, GridPolygon boundary)
        {
            MedialAxisGraph graph = new();

            // Map faces to their circumcenters (only if inside boundary)
            Dictionary<IFace, GridVector2> faceToCircumcenter = [];

            // Step 1: Calculate circumcenters for all triangles
            foreach (var face in mesh.Faces)
            {
                try
                {
                    GridVector2[] vertices = [.. mesh[face.iVerts].Select(v => v.Position)];

                    // Calculate the circumcircle of the triangle
                    GridCircle circle = GridCircle.CircleFromThreePoints(vertices);

                    // Only add circumcenters that fall inside the polygon boundary
                    if (boundary.GetRelation(circle.Center) == ShapeRelation.CONTAINED)
                    {
                        faceToCircumcenter[face] = circle.Center;
                        GetOrAddVertex(graph, circle.Center);
                    }
                }
                catch (ArgumentException)
                {
                    // Degenerate triangle (collinear points) - skip this face
                    // CircleFromThreePoints throws ArgumentException for collinear points
                }
            }

            // Step 2: Connect circumcenters of adjacent triangles that share an interior edge
            foreach (var edge in mesh.Edges.Values)
            {
                GridLineSegment line = mesh.ToGridLineSegment(edge);

                // Only process interior (non-boundary) edges with exactly two adjacent faces
                if (!boundary.IsExteriorOrInteriorSegment(line) && edge.Faces.Count == 2)
                {
                    var faces = edge.Faces.ToArray();

                    // Connect the circumcenters if both triangles have valid circumcenters inside the boundary
                    if (faceToCircumcenter.TryGetValue(faces[0], out var center1) &&
                        faceToCircumcenter.TryGetValue(faces[1], out var center2))
                    {
                        MedialAxisEdge medialEdge = new(center1, center2);
                        if (!graph.Edges.ContainsKey(medialEdge))
                            graph.AddEdge(medialEdge);
                    }
                }
            }

            return graph;
        }

        private static MedialAxisVertex GetOrAddVertex(MedialAxisGraph graph, GridVector2 p)
        {
            if (graph.TryGetValue(p, out var node)) return node;

            node = new MedialAxisVertex(p);
            graph.AddNode(node);
            return node;
        }

        private static MedialAxisVertex GetOrAddLineBisectorVertex(MedialAxisGraph graph, GridLineSegment line)
        {
            GridVector2 midpoint = line.Bisect();
            if (graph.TryGetValue(midpoint, out var node))
            {
                return node;
            }

            node = new MedialAxisVertex(midpoint);
            graph.AddNode(node);
            return node;
        }
    }

}
