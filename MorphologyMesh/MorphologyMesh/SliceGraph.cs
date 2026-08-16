using AnnotationVizLib;
using Geometry;
using GraphLib;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace MorphologyMesh
{
    /// <summary>
    /// A view of a morphology graph optimized to generate meshes
    /// 
    /// /// We need to group sets of connected nodes in slices so we do not miss any branches in the final mesh.  
    /// The example belows shows lettered nodes that appear on each of 5 Z-Levels.  
    ///
    ///  Z = 1:               I
    ///                      /|
    ///  Z = 2:             / J
    ///                    /    \
    ///  Z = 3:   A   B   /       C
    ///            \ / \ /       / \
    ///  Z = 4:     D   E       /   F
    ///                  \     /
    ///  Z = 5:           G   H
    ///
    /// In this case we'd want to generate four meshing groups (nodes):
    /// 1: A,B,D,E,I,J
    /// 2: C,F,H
    /// 3: E,G
    /// 4: J,C
    /// 
    /// These nodes are connected with edges to indicate which sections can connect when merging the
    /// mesh from each meshing group
    /// 
    /// Edges: 1-3, 1-4, 2-4
    /// 
    /// To do this we pick a node, E, and a direction.  We build a list of all nodes above E -> B,I.  
    ///Then we ask B,E for nodes below B,I -> D,J.  Then we ask for nodes above: D,J -> A.  Continuing 
    ///until no new nodes are added.  These nodes are then combined and sent to the Bajaj generator
    ///
    /// A SliceGraph translates all polygons to the center of the bounding box of the MorphologyGraph it is passed.  To 
    /// position the SliceGraph in volume space you should translate it to the center of the bounding box of the Morhphology
    /// graph.
    /// </summary>
    public class SliceGraph : Graph<ulong, Slice, Edge<ulong>>
    {
        readonly MorphologyGraph Graph;

        public double SectionThickness => this.Graph.SectionThickness;

        /// <summary>
        /// Polygons with an area below this we do not bother to render in the slice graph
        /// </summary>
        static readonly double MinAnnotationArea = 0.25;

        /// <summary>
        /// Caches the shape of each morphology node in the slice graph.  After corresponding verticies are added this cache is used to ensures each section will get the same input shapes
        /// The map can also be used to support simplifying shapes.
        /// </summary>
        internal Dictionary<ulong, IShape2D> MorphNodeToShape = null;

        private Vector2? _translationToCenter;

        /// <summary>
        /// The translation InitializeShapes applied to every cached shape, so any shape rebuilt outside that cache
        /// can be placed in the same centered space.
        /// </summary>
        private Vector2 TranslationToCenter => _translationToCenter ??= -Graph.BoundingBox.CenterPoint.XY();

        private Dictionary<ulong, SliceTopology> SliceToTopology = null;

        /// <summary>
        /// The center of the bounding box of all slices in the graph
        /// </summary>
        public Box BoundingBox => Graph.BoundingBox;

        private SliceGraph(MorphologyGraph graph)
        {
            this.Graph = graph;
        }

        public static async Task<SliceGraph> Create(MorphologyGraph graph, double tolerance = 0)
        {
            //An empty morphology graph has no bounding box; downstream code (InitializeShapes ->
            //graph.BoundingBox.CenterPoint) would otherwise dereference a default Box and throw an opaque
            //NullReferenceException.  Fail fast with an actionable message instead.
            if (graph.Nodes.Count == 0 && graph.Subgraphs.IsEmpty)
                throw new ArgumentException(
                    "Cannot create a SliceGraph from an empty MorphologyGraph (no nodes). " +
                    "This usually means the OData query returned no annotations for the requested IDs " +
                    "(e.g. location IDs were queried as structure IDs, or the IDs/endpoint are wrong).",
                    nameof(graph));

            SliceGraph output = new(graph);

            SortedSet<MorphologyEdge> Edges = [.. graph.Edges.Values];

            Dictionary<ulong, SortedSet<ulong>> MorphNodeToSliceNodes = []; //Map a morphology node to all slice nodes it appears in.  Used to create edges

            ulong iNextKey = 0;
            while (Edges.Count > 0)
            {

                MorphologyEdge e = Edges.First();

                //We remove cycles from the graph as we work, so there is a remote chance the edge has been removed, so just move on in that case
                if (graph.Edges.ContainsKey(e) == false)
                {
                    Edges.Remove(e);
                    continue;
                }

                MorphologyNode Source = graph[e.SourceNodeKey];
                MorphologyNode Target = graph[e.TargetNodeKey];

                ZDirection SearchDirection = Source.Z < Target.Z ? ZDirection.Increasing : ZDirection.Decreasing;

                BuildMeshingCrossSection(graph, Source, SearchDirection, out var MeshGroupNodesAbove, out SortedSet<ulong> MeshGroupNodesBelow, out SortedSet<MorphologyEdge> MeshGroupEdges);

                if (graph.Edges.ContainsKey(e)) //If the edge wasn't removed to stop a cycle it should be in the result set
                {
                    Debug.Assert(MeshGroupNodesAbove.Count > 0, "Search should have found at least one node above and below.");
                    Debug.Assert(MeshGroupNodesBelow.Count > 0, "Search should have found at least one node above and below.");
                    Debug.Assert(MeshGroupEdges.Contains(e), "The edge we used to start the search is not in the search results.");
                    if (MeshGroupEdges.Contains(e) == false) //This is an edge cases that shouldn't happen if deleting edges from graphs works, but removing the edge from our list so we don't loop infinitely should fix it
                    {
                        Edges.Remove(e);
                    }
                }
                else
                {
                    //We removed the edge from the graph, probably a cycle, move on
                    Edges.Remove(e);
                    continue;
                }
                Slice group = new(iNextKey, MeshGroupNodesAbove, MeshGroupNodesBelow, MeshGroupEdges);

                foreach (ulong id in group.AllNodes)
                {
                    if (MorphNodeToSliceNodes.TryGetValue(id, out var sliceNodes) == false)
                    {
                        sliceNodes = [];
                        MorphNodeToSliceNodes[id] = sliceNodes;
                    }

                    sliceNodes.Add(iNextKey);
                }

                output.AddNode(group);

                Edges.ExceptWith(MeshGroupEdges);

                iNextKey++;
            }

            //Check nodes with no edges, and add them if they are not in the slicegraph
            //Sanity check that the edge wasn't removed to eliminate a cycle and the node is not somehow in the slicegraph 
            foreach (var Node in graph.Nodes.Values.Where(n => !n.Edges.Any() && !MorphNodeToSliceNodes.ContainsKey(n.ID)))
            {
                Slice abovegroup = new(iNextKey, [Node.ID], [], []);
                Slice belowgroup = new(iNextKey + 1, [], [Node.ID], []);

                MorphNodeToSliceNodes[Node.ID] = [iNextKey, iNextKey + 1];
                iNextKey += 2;
                output.AddNode(abovegroup);
                output.AddNode(belowgroup);
            }

            //Create edges between sections in the new graph to indicate how sections need to anneal in the final merged mesh
            foreach (var morph_id in graph.Nodes.Keys)
            {
                bool hasKey = MorphNodeToSliceNodes.TryGetValue(morph_id, out SortedSet<ulong> SlicesForMorphNode);
                if (false == hasKey)
                    continue;

                if (SlicesForMorphNode.Count < 2)
                    continue;

                foreach (var pair in SlicesForMorphNode.ToArray().CombinationPairs<ulong>())
                {
                    Edge<ulong> edge = new(pair.A, pair.B, false);

                    if (output.Edges.ContainsKey(edge))
                        continue;

                    output.AddEdge(edge);

                    ////////////////////////////////////////////////////////////////////////////
                    //Record that the slices have a connection above/below and do not need a cap
                    {
                        Slice A = output[pair.A];
                        Slice B = output[pair.B];

                        if (A.NodesAbove.Contains(morph_id) && B.NodesBelow.Contains(morph_id))
                        {
                            A.HasSliceAbove = true;
                            B.HasSliceBelow = true;
                        }
                        else if (B.NodesAbove.Contains(morph_id) && A.NodesBelow.Contains(morph_id))
                        {
                            //Debug.Assert(B.NodesAbove.Contains(morph_id));
                            A.HasSliceBelow = true;
                            B.HasSliceAbove = true;
                        }
                    }
                    /////////////////////////////////////////////////////////////////////////// 
                }
            }

            output.MorphNodeToShape = await InitializeShapes(graph, tolerance);
            output.InitializeSliceTopology(tolerance);

            /*output.SliceToTopology = new Dictionary<ulong, SliceTopology>(output.Nodes.Count);
            foreach(Slice s in output.Nodes.Values)
            {
                output.SliceToTopology[s.Key] = output.GetTopology(s);
            }
            */

            return output;
        }

        internal class CycleInGraphException(ulong[] cycle, string msg = null) : Exception(msg)
        {
            /// <summary>
            /// This set is not a path that describes a cycle.  It is a set of nodes who have cycles that may or may not be the same.
            /// </summary>
            public ulong[] NodesWithACycle = cycle;
        }



        static void BuildMeshingCrossSection(MorphologyGraph graph, MorphologyNode seed, ZDirection CheckDirection, out SortedSet<ulong> NodesAbove, out SortedSet<ulong> NodesBelow, out SortedSet<MorphologyEdge> FollowedEdges)
        {
            NodesAbove = [];
            NodesBelow = [];
            SortedSet<ulong> NewNodesAbove = [];
            SortedSet<ulong> NewNodesBelow = [];

            FollowedEdges = [];

            if (CheckDirection == ZDirection.Increasing)
            {
                NodesBelow.Add(seed.ID);
                NewNodesAbove.UnionWith(seed.GetEdgesAbove(graph));
                FollowedEdges.UnionWith(NewNodesAbove.Select(n => new MorphologyEdge(graph, n, seed.ID)));
            }
            else
            {
                NodesAbove.Add(seed.ID);
                NewNodesBelow.UnionWith(seed.GetEdgesBelow(graph));
                FollowedEdges.UnionWith(NewNodesBelow.Select(n => new MorphologyEdge(graph, n, seed.ID)));
            }

            try
            {
                BuildMeshingCrossSection(graph, ref NodesAbove, ref NodesBelow, NewNodesAbove, NewNodesBelow, ref FollowedEdges);
            }
            catch (CycleInGraphException e)
            {
                //Try to remove the cycle and try again if we succeeded, otherwiese we need to fail this cross section generation
                if (TryRemoveCycle(graph, e.NodesWithACycle))
                {
                    BuildMeshingCrossSection(graph, seed, CheckDirection, out NodesAbove, out NodesBelow, out FollowedEdges);
                    return;
                }
                else
                {
                    ///Hmm... return what we have?
                    Trace.WriteLine($"Bailing out of one MeshingCrossSection build because I found a cycle at {e.NodesWithACycle[0]} I couldn't remove automatically.");
                    return;
                }
            }
        }

        /// <summary>
        /// This returns a meshing cross section, but cycles aren't compatible with the mesh generator, so it has a kludgy boolean return value.  If a cycle is found it should be removed and then the 
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="NodesAbove"></param>
        /// <param name="NodesBelow"></param>
        /// <param name="NewNodesAbove"></param>
        /// <param name="NewNodesBelow"></param>
        /// <param name="FollowedEdges"></param>
        /// <returns></returns>
        private static void BuildMeshingCrossSection(MorphologyGraph graph, ref SortedSet<ulong> NodesAbove, ref SortedSet<ulong> NodesBelow, SortedSet<ulong> NewNodesAbove, SortedSet<ulong> NewNodesBelow, ref SortedSet<MorphologyEdge> FollowedEdges)
        {
            NodesAbove.UnionWith(NewNodesAbove);
            NodesBelow.UnionWith(NewNodesBelow);

            FollowedEdges.UnionWith(NewNodesAbove.SelectMany(n => graph[n].GetEdgesBelow(graph).Select(other => new MorphologyEdge(graph, other, n))));
            FollowedEdges.UnionWith(NewNodesBelow.SelectMany(n => graph[n].GetEdgesAbove(graph).Select(other => new MorphologyEdge(graph, other, n))));

            NewNodesBelow = [.. NewNodesAbove.SelectMany(n => graph[n].GetEdgesBelow(graph))];
            NewNodesAbove = [.. NewNodesBelow.SelectMany(n => graph[n].GetEdgesAbove(graph))];

            var CycleWithAbove = NodesAbove.Intersect(NewNodesBelow).ToArray();
            if (CheckForCycle(CycleWithAbove))
                throw new CycleInGraphException(CycleWithAbove);

            var CycleWithBelow = NodesBelow.Intersect(NewNodesAbove).ToArray();
            if (CheckForCycle(CycleWithBelow))
                throw new CycleInGraphException(CycleWithBelow);

            NewNodesAbove.ExceptWith(NodesAbove);
            NewNodesBelow.ExceptWith(NodesBelow);

            if (NewNodesAbove.Count == 0 && NewNodesBelow.Count == 0)
            {
                return;
            }
            else
            {
                BuildMeshingCrossSection(graph, ref NodesAbove, ref NodesBelow, NewNodesAbove, NewNodesBelow, ref FollowedEdges);
                return;
            }
        }

        private static bool CheckForCycle(ulong[] cycle_ids)
        {
            if (cycle_ids.Length > 0)
            {
                foreach (var id in cycle_ids)
                {
                    Trace.WriteLine($"Location {id} forms a cycle in the morphology graph");
                }

                //Debug.Assert(cycle_ids.Length == 0, string.Format("Cycle found in graph: {0}", cycle_ids[0]));
                return true;
            }

            return false;
        }

        private static bool TryRemoveCycle(MorphologyGraph graph, ulong[] cycle_ids)
        {
            if (cycle_ids.Length == 0)
                return true;

            foreach (var id in cycle_ids)
            {
                //Find a cycle path, find the longest edge, and break it
                var cycle = graph.FindCycle(id);
                if (cycle is null)
                {
                    Trace.WriteLine($"I couldn't find a cycle for location {id}, which is weird because I found one earlier.  Bug in the graph cycle travelling code?");
                }

                //Measure the distance in Z between all nodes in the cycle.  Remove the edge with the largest difference.
                MorphologyNode current = graph[cycle[0]];
                SortedList<double, MorphologyEdge> sortedEdgeLength = [];
                for (int i = 1; i < cycle.Count - 1; i++)
                {
                    MorphologyNode next = graph[cycle[i]];

                    //I'm just using straight Z distance as my metric, but it could be XYZ if it doesn't work well.
                    double distance = current.Z - next.Z;
                    MorphologyEdge edge = new(graph, current.Key, next.Key);
                    sortedEdgeLength.Add(distance, edge);
                }

                var edgeToRemove = sortedEdgeLength.Last().Value;
                graph.RemoveEdge(edgeToRemove);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Populates the lookup table mapping morph nodes to shapes.  Allows user option to simplify shapes.  Ensures all shapes have matching corresponding verticies if they participate in two or more slices
        /// </summary>
        /// <param name="tolerance"></param>
        private async void InitializeSliceTopology(double tolerance = 0)
        {
            try
            {
                this.MorphNodeToShape ??= await SliceGraph.InitializeShapes(this.Graph, tolerance);

                ConcurrentTopologyInitializer concurrentInitializer = new(this);

                this.SliceToTopology = concurrentInitializer.InitializeSliceTopology();

                /*
                //Create corresponding verticies for all shapes
                foreach (var node in this.Nodes.Values)
                {
                    SliceTopology st = GetSliceTopology(node, MorphNodeToShape);

                    //Add corresponding verticies.  Will insert into the polygons without creating new ones, which will update MorphNodeToShape
                    //List<Vector2> correspondingPoints = st.Polygons.AddCorrespondingVertices();

                    //AddPointsBetweenAdjacentCorrespondingVerticies(st.Polygons,  correspondingPoints);
                }
                */
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"InitializeSliceTopology failed: {ex}", "SliceGraph");
                // Log and swallow: caller cannot await async void; leaving MorphNodeToShape/SliceToTopology unchanged is the safe default.
            }
        }

        /// <summary>
        /// Generate a dictionary of polygons we can use as a lookup table for shapes.
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="tolerance"></param>
        /// <returns></returns>
        public static async Task<Dictionary<ulong, IShape2D>> InitializeShapes(MorphologyGraph graph, double tolerance = 0)
        {
            Dictionary<ulong, IShape2D> result = new(graph.Nodes.Count);

            Vector2 translationToCenter = -graph.BoundingBox.CenterPoint.XY();

            List<Task<IShape2D>> tasks = new(graph.Nodes.Count);
            foreach (var node in graph.Nodes.Values)
            {
                if (node.Geometry is null)
                    continue;

                SupportedGeometryType nodeType = node.Geometry.GeometryType();
                switch (nodeType)
                {
                    case SupportedGeometryType.POINT:
                        continue;
                    case SupportedGeometryType.CURVEPOLYGON:
                    case SupportedGeometryType.POLYGON:
                        {
                            //Start a task to simplify the polygon
                            Task<IShape2D> t = new((node_) =>
                            {
                                var poly = ((MorphologyNode)node_).Geometry.ToPolygon();
                                if (poly.BoundingBox.Area < MinAnnotationArea)
                                    return null;

                                poly = poly.Translate(translationToCenter);

                                try
                                {
                                    return poly.Simplify(tolerance);
                                }
                                catch (ArgumentException e)
                                {
                                    Trace.WriteLine(
                                        $"Could not simplify location #{node.ID}.  Using original (more detailed) polygon instead.");
#if DEBUG
                                    Trace.WriteLine($"{e}");
#endif
                                    return poly;
                                }
                            }, node);

                            t.Start();
                            tasks.Add(t);
                        }
                        break;
                    case SupportedGeometryType.POLYLINE:
                        {
                            Task<IShape2D> t = new((node_) => ((MorphologyNode)node_).Geometry.ToPolyLine().Translate(translationToCenter).Simplify(tolerance), node);
                            t.Start();
                            tasks.Add(t);
                        }
                        break;
                }
            }

            foreach (var task in tasks)
            {
                try
                {
                    IShape2D output = await task;
                    if (output is null)
                    {
                        continue;
                    }

                    Debug.Assert(output.BoundingBox.Area > 0);

                    //Rounding exposed a rare bug on 82682, 82680 RPC1 where the inner hole was exactly over the exterior ring of the opposite polygon
                    if (output is Polygon poly)
                    {
                        result.Add((ulong)((MorphologyNode)(task.AsyncState)).ID, poly.Round(Global.SignificantDigits));
                    }
                    else if (output is Polyline line)
                    {
                        result.Add((ulong)((MorphologyNode)(task.AsyncState)).ID, line.Round(Global.SignificantDigits));
                    }
                    else
                    {
                        throw new NotImplementedException($"Initializing unknown shape: {output}");
                    }
                }
                catch (AggregateException e)
                {
                    //Oh well, we'll not simplify this one
                    continue;
                }
            }

            return result;
        }

        public SliceTopology GetTopology(Slice slice)
        {
            SliceToTopology ??= new Dictionary<ulong, SliceTopology>(this.Nodes.Count);

            if (SliceToTopology.TryGetValue(slice.Key, out var topology)) return topology;

            //If we are taking this path there is a danger corresponding verticies won't exist across multiple slices
            topology = GetSliceTopology(slice, MorphNodeToShape);
            SliceToTopology.Add(slice.Key, topology);
            return topology;
        }

        public SliceTopology GetTopology(ulong sliceKey)
        {
            SliceToTopology ??= new Dictionary<ulong, SliceTopology>(this.Nodes.Count);

            if (SliceToTopology.TryGetValue(sliceKey, out var cachedTopology)) return cachedTopology;

            //If we are taking this path there is a danger corresponding verticies won't exist across multiple slices
            var result = GetSliceTopology(sliceKey, MorphNodeToShape);
            Debug.Assert(result.Shapes != null, "Current version only handles polygons, developer needs to figure out why they are missing here.");
            SliceToTopology[sliceKey] = result;

            return SliceToTopology[sliceKey];
        }

        private SliceTopology GetSliceTopology(ulong sliceKey, IReadOnlyDictionary<ulong, IShape2D> polyLookup = null) => GetSliceTopology(this[sliceKey], polyLookup);

        internal SliceTopology GetSliceTopology(Slice group) => GetSliceTopology(group, this.MorphNodeToShape);

        /// <summary>
        /// A shape in a slice together with the data that must stay indexed alongside it.  These used to be four
        /// separate parallel lists, which desynchronized as soon as a shape was filtered out (a polyline, or a
        /// polygon the shape cache dropped), pairing each surviving shape with another shape's Z and upper/lower flag.
        /// </summary>
        private readonly record struct SliceShape(IShape2D Shape, bool IsUpper, double Z, ulong MorphNodeIndex);

        internal SliceTopology GetSliceTopology(Slice group, IReadOnlyDictionary<ulong, IShape2D> polyLookup = null)
        {
            List<SliceShape> sliceShapes = [];
            sliceShapes.AddRange(group.NodesAbove.Select(id => CreateSliceShape(id, true, polyLookup)));
            sliceShapes.AddRange(group.NodesBelow.Select(id => CreateSliceShape(id, false, polyLookup)));

            //Correspondence is computed over every shape in the slice, including shapes we cannot tile, because a
            //polyline still contributes corresponding verticies to the polygons it touches.
            List<IShape2D> ShapeList = [.. sliceShapes.Select(s => s.Shape)];
            var correspondingPoints = ShapeList.AddCorrespondingVertices();

            Polygon[] Polygons = [.. ShapeList.OfType<Polygon>()];
            SliceTopology.AddPointsBetweenAdjacentCorrespondingVerticies(Polygons, correspondingPoints);

            Polyline[] Polylines = [.. ShapeList.OfType<Polyline>()];
            SliceTopology.AddPointsBetweenAdjacentCorrespondingVerticies(Polylines, correspondingPoints);

            //The Bajaj generator only tiles polygons.  Filter the shapes and their per-shape data as a unit so the
            //surviving entries remain indexed in lockstep.
            SliceShape[] tileable = [.. sliceShapes.Where(s => s.Shape is Polygon)];

            if (tileable.Length != sliceShapes.Count)
                Trace.WriteLine($"Slice {group.Key}: {sliceShapes.Count - tileable.Length} of {sliceShapes.Count} shapes are not polygons and were excluded from the mesh.");

            return new SliceTopology(group.Key,
                tileable.Select(s => s.Shape),
                tileable.Select(s => s.IsUpper),
                tileable.Select(s => s.Z),
                tileable.Select(s => s.MorphNodeIndex),
                this.SectionThickness);
        }

        private SliceShape CreateSliceShape(ulong id, bool isUpper, IReadOnlyDictionary<ulong, IShape2D> polyLookup)
        {
            IShape2D shape;

            if (polyLookup is null)
            {
                //Without the shape cache nothing in this topology is recentered, so the raw geometry is consistent.
                shape = Graph[id].Geometry.ToShape2D();
            }
            else if (polyLookup.TryGetValue(id, out var cached))
            {
                shape = cached;
            }
            else
            {
                //The cache omits shapes it could not prepare, such as a polygon below MinAnnotationArea.  Cached
                //shapes are centered on the graph bounding box, so rebuilding without that translation would place
                //this contour a half-volume away from the neighbors it is meant to tile against.
                shape = Graph[id].Geometry.ToPolygon().Translate(TranslationToCenter);
            }

            return new SliceShape(shape, isUpper, Graph[id].Z, id);
        }
    }


    /// <summary>
    /// This represents a group of connected morphology nodes (Location and Location Link rows) that need to be meshed together as a single group.  They can 
    /// span more than two Z levels depending on how annotation occurred but must still branch correctly.  For the 
    /// meshing we simplify this to the set of annotations above and set of annotations below.
    /// 
    /// A mesh is then generated for the slice, and then those meshes can be merged to make a single mesh for an entire structure.
    /// </summary>
    [Serializable]
    public class Slice : Node<ulong, Edge<ulong>>
    {
        /// <summary>
        /// Shapes on the top of our cross section
        /// </summary>
        public readonly SortedSet<ulong> AllNodes;

        /// <summary>
        /// Shapes on the top of our cross section
        /// </summary>
        public readonly SortedSet<ulong> NodesAbove;

        /// <summary>
        /// Shapes on the bottom of our cross section
        /// </summary>
        public readonly SortedSet<ulong> NodesBelow;

        /// <summary>
        /// Internal edges
        /// </summary>
        public readonly SortedSet<MorphologyEdge> InternalEdges;

        public readonly double SliceThickness;

        public bool HasSliceAbove { get; internal set; } = false;
        public bool HasSliceBelow { get; internal set; } = false;

        public Slice(ulong key, SortedSet<ulong> nodesAbove, SortedSet<ulong> nodesBelow, SortedSet<MorphologyEdge> edges) : base(key)
        {
            //this.Graph = graph;
            this.NodesAbove = nodesAbove;
            this.NodesBelow = nodesBelow;
            this.InternalEdges = edges;
            SortedSet<ulong> allNodes = [.. NodesAbove];
            allNodes.UnionWith(NodesBelow);
            AllNodes = allNodes;
        }

        public override string ToString()
        {
            StringBuilder sb = new();

            sb.Append("U:");
            foreach (ulong ID in NodesAbove)
            {
                sb.AppendFormat(" {0}", ID);
            }

            sb.AppendLine(" D:");
            foreach (ulong ID in NodesBelow)
            {
                sb.AppendFormat(" {0}", ID);
            }

            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            Slice group = obj as Slice;
            return group != null &&
                   EqualityComparer<SortedSet<ulong>>.Default.Equals(NodesAbove, group.NodesAbove) &&
                   EqualityComparer<SortedSet<ulong>>.Default.Equals(NodesBelow, group.NodesBelow) &&
                   EqualityComparer<SortedSet<MorphologyEdge>>.Default.Equals(InternalEdges, group.InternalEdges);
        }

        public override int GetHashCode() => HashCode.Combine(NodesAbove, NodesBelow, InternalEdges);
    }
}
