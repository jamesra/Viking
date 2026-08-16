using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    public class MedialAxisEdge(Vector2 SourceNode, Vector2 TargetNode) : GraphLib.Edge<Vector2>(SourceNode, TargetNode, false)
    {
        public LineSegment Line => new(this.SourceNodeKey, this.TargetNodeKey);
    }

    public class MedialAxisVertex(Vector2 k) : GraphLib.Node<Vector2, MedialAxisEdge>(k)
    {
        public override string ToString() => Key.ToString();
    }

    public class MedialAxisGraph : GraphLib.Graph<Vector2, MedialAxisVertex, MedialAxisEdge>
    {
        public Vector2 FindStartForBoundarySearch(Polygon[] shapes) => Nodes.First(v => shapes.All(shape => !shape.Contains(v.Key))).Key;

        public LineSegment[] Segments => [.. this.Edges.Select(edge => edge.Value.Line)];

        public Vector2[] Points => [.. this.Nodes.Select(n => n.Key)];

        /// <summary>
        /// Returns a copy of the graph with all nodes translated by the specified vector.
        /// </summary>
        /// <param name="vector">The translation vector to apply to all nodes</param>
        /// <returns>A new MedialAxisGraph with translated nodes and edges</returns>
        public MedialAxisGraph Translate(Vector2 vector)
        {
            MedialAxisGraph translatedGraph = new();

            // Add all translated nodes
            foreach (var node in this.Nodes)
            {
                Vector2 translatedPosition = node.Key + vector;
                translatedGraph.AddNode(new MedialAxisVertex(translatedPosition));
            }

            // Add all edges with translated endpoints
            foreach (var edge in this.Edges.Values)
            {
                Vector2 translatedSource = edge.SourceNodeKey + vector;
                Vector2 translatedTarget = edge.TargetNodeKey + vector;
                translatedGraph.AddEdge(new MedialAxisEdge(translatedSource, translatedTarget));
            }

            return translatedGraph;
        }

    }

    public static class MedialAxisFinder
    {
        /// <summary>
        /// Approximate the medial axis of a polygon.
        /// </summary>
        /// <remarks>
        /// This is the entry point used by the mesh pipeline (untiled-region closing).  It uses the
        /// Chordal Axis Transform, which reuses the constrained Delaunay triangulation and is guaranteed
        /// to produce a connected graph whose vertices all fall strictly inside the polygon (a contract the
        /// circumcenter approach could not honor, because obtuse/sliver triangles place circumcenters
        /// outside the boundary where they were silently dropped).
        /// </remarks>
        /// <param name="shape">The polygon shape to calculate the medial axis for</param>
        /// <returns></returns>
        public static MedialAxisGraph ApproximateMedialAxis(Polygon shape) =>
            ApproximateMedialAxisChordal(shape, extendToApex: false, pruneRatio: 0.0);

        /// <summary>
        /// Approximate the medial axis using the Chordal Axis Transform (CAT).
        /// </summary>
        /// <remarks>
        /// The Chordal Axis (Prasad 1997) is extracted directly from the constrained Delaunay triangulation
        /// produced by <see cref="MeshExtensions.Triangulate(Polygon, int, TriangulationMesh{IVertex2D{PolygonIndex}}.ProgressUpdate)"/>.
        /// Each interior triangle is classified by how many of its edges lie on the polygon boundary and the
        /// midpoints of its interior edges (plus a centroid at junctions) are connected.  Because adjacent
        /// triangles share an interior edge, they share that edge's midpoint node, so the resulting graph is
        /// always connected.
        /// </remarks>
        /// <param name="shape">The polygon shape to calculate the medial axis for</param>
        /// <param name="extendToApex">
        /// When true, termination triangles extend a branch to the boundary vertex opposite their interior
        /// edge, producing a skeleton that reaches into convex corners (useful for visualization).  When
        /// false (the default, used by the mesh pipeline) branches stop at interior-edge midpoints so every
        /// vertex is strictly inside the polygon.
        /// </param>
        /// <param name="pruneRatio">
        /// When greater than zero, spurious short "hair" branches that sprout from junctions are removed.  A
        /// leaf is pruned when its clearance (distance to the boundary) is less than pruneRatio times the
        /// clearance of the junction it attaches to.  Zero (the default) disables pruning.
        /// </param>
        /// <returns>A connected medial axis graph</returns>
        public static MedialAxisGraph ApproximateMedialAxisChordal(Polygon shape, bool extendToApex = false, double pruneRatio = 0.0)
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

            Polygon boundary = centroid == Vector2.Zero ? shape : shape.Translate(-centroid);
            MedialAxisGraph graph = BuildChordalAxisFromMesh2D(mesh, boundary, extendToApex);

            if (pruneRatio > 0.0)
                PruneBranches(graph, boundary, pruneRatio);

            //Translate the medial axis graph back to the shape centroid if necessary
            if (centroid == Vector2.Zero)
                return graph;
            else
                return graph.Translate(centroid);
        }

        /// <summary>
        /// Approximate the medial axis using the Chordal Axis Transform.
        /// </summary>
        /// <remarks>
        /// Retained for source compatibility.  Previously this used a circumcenter (Voronoi-dual) approach;
        /// it now forwards to the Chordal Axis Transform, which is connected and stays inside the boundary.
        /// The skeleton is extended into convex corners for visualization parity with the prior behavior.
        /// Prefer <see cref="ApproximateMedialAxisChordal"/> in new code.
        /// </remarks>
        /// <param name="shape">The polygon shape to calculate the medial axis for</param>
        /// <returns>A medial axis graph</returns>
        [Obsolete("Use ApproximateMedialAxisChordal. This method now forwards to the Chordal Axis Transform.")]
        public static MedialAxisGraph ApproximateMedialAxisImproved(Polygon shape) =>
            ApproximateMedialAxisChordal(shape, extendToApex: true, pruneRatio: 0.0);

        /// <summary>
        /// Approximate the medial axis using triangle circumcenters (the Voronoi dual of the Delaunay
        /// triangulation).  Retained for A/B comparison in tests; prefer <see cref="ApproximateMedialAxisChordal"/>.
        /// </summary>
        /// <remarks>
        /// Note that the dual-of-Delaunay = Voronoi property only holds for an unconstrained triangulation.
        /// For a constrained polygon triangulation, circumcenters of obtuse/sliver triangles can fall outside
        /// the boundary and are dropped, which can disconnect the graph or yield no interior points.
        /// </remarks>
        /// <param name="shape">The polygon shape to calculate the medial axis for</param>
        /// <returns>A medial axis graph with vertices at triangle circumcenters</returns>
        public static MedialAxisGraph ApproximateMedialAxisCircumcenter(Polygon shape)
        {
            TriangulationMesh<IVertex2D<PolygonIndex>> mesh;
            var centroid = shape.Centroid;
            try
            {
                mesh = shape.Triangulate();
            }
            catch (ArgumentException)
            {
                return new MedialAxisGraph();
            }

            MedialAxisGraph graph = BuildImprovedGraphFromMesh2D(mesh, centroid == Vector2.Zero ? shape : shape.Translate(-centroid));

            if (centroid == Vector2.Zero)
                return graph;
            else
                return graph.Translate(centroid);
        }

        /// <summary>
        /// Builds a medial axis graph from a triangulated polygon using the Chordal Axis Transform.
        /// </summary>
        /// <remarks>
        /// Each triangle is classified by the number of its edges that lie on the polygon boundary:
        /// <list type="bullet">
        /// <item>0 boundary edges (junction): a node is placed at the triangle centroid and connected to the
        /// midpoints of all three interior edges.</item>
        /// <item>1 boundary edge (sleeve): the midpoints of the two interior edges are connected.</item>
        /// <item>2 boundary edges (termination): the midpoint of the single interior edge optionally connects
        /// to the opposite (apex) boundary vertex (see <paramref name="extendToApex"/>).</item>
        /// <item>3 boundary edges (isolated single-triangle region): a single centroid node is emitted so the
        /// region always yields at least one interior point.</item>
        /// </list>
        /// Interior-edge midpoints are shared between the two triangles that share the edge, so the graph is
        /// connected without any explicit stitching step.
        /// </remarks>
        /// <param name="mesh">The triangulated mesh</param>
        /// <param name="boundary">The polygon boundary used to classify edges and constrain the axis</param>
        /// <param name="extendToApex">Whether termination triangles extend a branch to their apex boundary vertex</param>
        /// <returns>A connected medial axis graph</returns>
        private static MedialAxisGraph BuildChordalAxisFromMesh2D(IReadOnlyMesh2D<IVertex2D> mesh, Polygon boundary, bool extendToApex)
        {
            MedialAxisGraph graph = new();

            foreach (var face in mesh.Faces)
            {
                var iVerts = face.iVerts;

                //The Chordal Axis Transform is defined on triangulations.  Skip any non-triangle face.
                if (iVerts.Length != 3)
                    continue;

                //Classify each edge of the triangle as a boundary (ring) edge or an interior (diagonal) edge.
                List<IEdgeKey> interiorEdges = [];
                foreach (IEdgeKey edgeKey in face.Edges)
                {
                    LineSegment line = mesh.ToLineSegment(edgeKey);
                    if (false == boundary.IsExteriorOrInteriorSegment(line))
                        interiorEdges.Add(edgeKey);
                }

                switch (interiorEdges.Count)
                {
                    case 3: //Junction triangle: centroid connects to all three interior edge midpoints.
                        {
                            Triangle tri = new([.. mesh[iVerts].Select(v => v.Position)]);
                            MedialAxisVertex center = GetOrAddVertex(graph, tri.Centroid);
                            System.Diagnostics.Debug.Assert(boundary.GetRelation(center.Key) == ShapeRelation.Contained, "Medial Axis junction vertex must be within polygonal boundary");

                            foreach (IEdgeKey interiorEdge in interiorEdges)
                            {
                                MedialAxisVertex mid = GetOrAddLineBisectorVertex(graph, mesh.ToLineSegment(interiorEdge));
                                AddEdgeIfMissing(graph, center.Key, mid.Key);
                            }
                            break;
                        }
                    case 2: //Sleeve triangle: connect the midpoints of the two interior edges.
                        {
                            MedialAxisVertex midA = GetOrAddLineBisectorVertex(graph, mesh.ToLineSegment(interiorEdges[0]));
                            MedialAxisVertex midB = GetOrAddLineBisectorVertex(graph, mesh.ToLineSegment(interiorEdges[1]));
                            AddEdgeIfMissing(graph, midA.Key, midB.Key);
                            break;
                        }
                    case 1: //Termination triangle: the branch ends at the interior edge midpoint.
                        {
                            IEdgeKey interiorEdge = interiorEdges[0];
                            MedialAxisVertex mid = GetOrAddLineBisectorVertex(graph, mesh.ToLineSegment(interiorEdge));

                            if (extendToApex)
                            {
                                //The apex is the triangle vertex not on the interior edge (it lies on the boundary).
                                Vector2[] positions = [.. mesh[iVerts].Select(v => v.Position)];
                                for (int k = 0; k < iVerts.Length; k++)
                                {
                                    if (false == interiorEdge.Contains(iVerts[k]))
                                    {
                                        MedialAxisVertex apex = GetOrAddVertex(graph, positions[k]);
                                        AddEdgeIfMissing(graph, mid.Key, apex.Key);
                                        break;
                                    }
                                }
                            }
                            break;
                        }
                    case 0: //Isolated single-triangle region: emit a centroid so the region is never empty.
                        {
                            Triangle tri = new([.. mesh[iVerts].Select(v => v.Position)]);
                            GetOrAddVertex(graph, tri.Centroid);
                            break;
                        }
                }
            }

            return graph;
        }

        /// <summary>
        /// Removes spurious short "hair" branches that sprout from junctions.  A leaf node (degree 1) whose
        /// only neighbor is a junction (degree >= 3) is removed when the leaf's clearance is less than
        /// <paramref name="pruneRatio"/> times the junction's clearance.  Pruning iterates until stable.
        /// </summary>
        /// <param name="graph">The medial axis graph to prune in place</param>
        /// <param name="boundary">The polygon boundary used to measure clearance (distance to the boundary)</param>
        /// <param name="pruneRatio">The clearance ratio threshold; values around 0.5-1.0 are typical</param>
        private static void PruneBranches(MedialAxisGraph graph, Polygon boundary, double pruneRatio)
        {
            bool removedAny = true;
            while (removedAny)
            {
                removedAny = false;

                //Snapshot the current leaves; the collection is mutated as we remove nodes.
                List<MedialAxisVertex> leaves = [.. graph.Nodes.Values.Where(n => n.Edges.Count == 1)];
                foreach (MedialAxisVertex leaf in leaves)
                {
                    //The leaf may have been removed already during this pass.
                    if (false == graph.TryGetValue(leaf.Key, out MedialAxisVertex leafNode))
                        continue;
                    if (leafNode.Edges.Count != 1)
                        continue;

                    Vector2 neighborKey = leafNode.Edges.Keys.First();
                    if (false == graph.TryGetValue(neighborKey, out MedialAxisVertex neighbor))
                        continue;

                    //Only prune true hairs: leaves attached to a junction.  This preserves the main spine and
                    //legitimate endpoints of long branches.
                    if (neighbor.Edges.Count < 3)
                        continue;

                    double leafClearance = boundary.Distance(leaf.Key);
                    double neighborClearance = boundary.Distance(neighborKey);

                    if (leafClearance < pruneRatio * neighborClearance)
                    {
                        graph.RemoveNode(leaf.Key); //RemoveNode also removes the incident edge.
                        removedAny = true;
                    }
                }
            }
        }

        private static void AddEdgeIfMissing(MedialAxisGraph graph, Vector2 a, Vector2 b)
        {
            //Skip zero-length self edges (can occur if two midpoints/centroids deduplicate to the same key).
            if (a.Equals(b))
                return;

            MedialAxisEdge edge = new(a, b);
            if (false == graph.Edges.ContainsKey(edge))
                graph.AddEdge(edge);
        }

        /// <summary>
        /// Converts a triangulation of a polygon into an improved medial axis graph using triangle circumcenters.
        /// This method implements the mathematically correct approach using the Voronoi dual of the Delaunay triangulation.
        /// Circumcenters of triangles are used as medial axis vertices, and edges connect circumcenters of adjacent triangles.
        /// </summary>
        /// <param name="mesh">The triangulated mesh</param>
        /// <param name="boundary">The polygon boundary to constrain the medial axis</param>
        /// <returns>A medial axis graph with vertices at triangle circumcenters</returns>
        private static MedialAxisGraph BuildImprovedGraphFromMesh2D(IReadOnlyMesh2D<IVertex2D> mesh, Polygon boundary)
        {
            MedialAxisGraph graph = new();

            // Map faces to their circumcenters (only if inside boundary)
            Dictionary<IFace, Vector2> faceToCircumcenter = [];

            // Step 1: Calculate circumcenters for all triangles
            foreach (var face in mesh.Faces)
            {
                try
                {
                    Vector2[] vertices = [.. mesh[face.iVerts].Select(v => v.Position)];

                    // Calculate the circumcircle of the triangle
                    Circle circle = Circle.CircleFromThreePoints(vertices);

                    // Only add circumcenters that fall inside the polygon boundary
                    if (boundary.GetRelation(circle.Center) == ShapeRelation.Contained)
                    {
                        // Store the canonical key from the deduplicated vertex, not the raw circumcenter.
                        // Near-duplicate circumcenters (within epsilon) must map to the same key so that
                        // Step 2 can detect and skip zero-length self-edges.
                        var vertex = GetOrAddVertex(graph, circle.Center);
                        faceToCircumcenter[face] = vertex.Key;
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
                LineSegment line = mesh.ToLineSegment(edge);

                // Only process interior (non-boundary) edges with exactly two adjacent faces
                if (!boundary.IsExteriorOrInteriorSegment(line) && edge.Faces.Count == 2)
                {
                    var faces = edge.Faces.ToArray();

                    // Connect the circumcenters if both triangles have valid circumcenters inside the boundary
                    if (faceToCircumcenter.TryGetValue(faces[0], out var center1) &&
                        faceToCircumcenter.TryGetValue(faces[1], out var center2))
                    {
                        // Skip self-edges: two adjacent triangles whose circumcenters deduplicated to the
                        // same vertex (i.e., were within epsilon of each other). Such an edge would have
                        // identical endpoints and cause a LineSegment exception when Line is accessed.
                        if (center1.Equals(center2))
                            continue;

                        MedialAxisEdge medialEdge = new(center1, center2);
                        if (!graph.Edges.ContainsKey(medialEdge))
                            graph.AddEdge(medialEdge);
                    }
                }
            }

            return graph;
        }

        private static MedialAxisVertex GetOrAddVertex(MedialAxisGraph graph, Vector2 p)
        {
            if (graph.TryGetValue(p, out var node)) return node;

            node = new MedialAxisVertex(p);
            graph.AddNode(node);
            return node;
        }

        private static MedialAxisVertex GetOrAddLineBisectorVertex(MedialAxisGraph graph, LineSegment line)
        {
            Vector2 midpoint = line.Bisect();
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
