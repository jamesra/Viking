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
using Viking.AnnotationServiceTypes.Interfaces;


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
    /// A SliceGraph translates all polygons by <see cref="XYOrigin"/> (typically the parent cell's
    /// location AABB center so synapses mesh in the same XY frame as the cell). Restore with that
    /// origin when drawing or exporting. Z stays in volume.
    /// </summary>
    public class SliceGraph : Graph<ulong, Slice, Edge<ulong>>
    {
        readonly MorphologyGraph Graph;

        public double SectionThickness => this.Graph.SectionThickness;

        /// <summary>
        /// Volume XY subtracted from every cached shape. Parent cell and its children should share this origin.
        /// </summary>
        public Vector2 XYOrigin { get; }

        /// <summary>
        /// Polygons with an area below this we do not bother to render in the slice graph
        /// </summary>
        static readonly double MinAnnotationArea = 0.25;

        /// <summary>
        /// Caches the shape of each morphology node in the slice graph.  After corresponding verticies are added this cache is used to ensures each section will get the same input shapes
        /// The map can also be used to support simplifying shapes.
        /// </summary>
        internal Dictionary<ulong, IShape2D> MorphNodeToShape = null;

        /// <summary>
        /// The translation InitializeShapes applied to every cached shape, so any shape rebuilt outside that cache
        /// can be placed in the same centered space.
        /// </summary>
        private Vector2 TranslationToCenter => -XYOrigin;

        private Dictionary<ulong, SliceTopology> SliceToTopology = null;

        private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, string> _failedTopologySlices = new();

        /// <summary>
        /// Slices whose topology could not be built, mapped to the sections they cover.
        ///
        /// These slices fall back to an empty topology so dependent slices can still run, which means they
        /// contribute no geometry at all.  A run that loses slices this way otherwise completes successfully and
        /// looks identical to a clean one, so callers are expected to report this rather than only trace it.
        /// </summary>
        public IReadOnlyDictionary<ulong, string> FailedTopologySlices => _failedTopologySlices;

        public int FailedTopologyCount => _failedTopologySlices.Count;

        internal void RecordTopologyFailure(ulong sliceKey, string sectionText) =>
            _failedTopologySlices[sliceKey] = sectionText;

        /// <summary>
        /// Placement origin for this structure's mesh (own locations, not child subgraphs).
        /// </summary>
        public Box BoundingBox => Graph.NodesBoundingBox;

        /// <summary>
        /// Volume section numbers (location UnscaledZ) of the annotations that form <paramref name="slice"/>.
        /// Used when reporting topology or meshing failures so the console shows a section, not only a slice key.
        /// </summary>
        public IReadOnlyList<long> GetSectionNumbers(Slice slice)
        {
            ArgumentNullException.ThrowIfNull(slice);
            return [.. slice.AllNodes
                .Select(id => Graph[id].Location.UnscaledZ)
                .Distinct()
                .OrderBy(z => z)];
        }

        /// <summary>
        /// Formats <see cref="GetSectionNumbers"/> for console and trace messages.
        /// </summary>
        public string FormatSectionNumbers(Slice slice)
        {
            IReadOnlyList<long> sections = GetSectionNumbers(slice);
            if (sections.Count == 0)
                return "unknown section";
            if (sections.Count == 1)
                return $"section {sections[0]}";
            return $"sections {string.Join(", ", sections)}";
        }

        private SliceGraph(MorphologyGraph graph, Vector2 xyOrigin)
        {
            this.Graph = graph;
            XYOrigin = xyOrigin;
        }

        /// <summary>
        /// Build a slice graph. <paramref name="xyOrigin"/> is the volume XY subtracted from shapes
        /// (defaults to this graph's own location AABB). Pass a parent cell origin so child synapses share that frame.
        /// </summary>
        public static async Task<SliceGraph> Create(MorphologyGraph graph, double tolerance = 0, Vector2? xyOrigin = null)
        {
            //An empty morphology graph has no bounding box; downstream code (InitializeShapes ->
            //graph.NodesBoundingBox.CenterPoint) would otherwise dereference a default Box and throw an opaque
            //NullReferenceException.  Fail fast with an actionable message instead.
            if (graph.Nodes.Count == 0 && graph.Subgraphs.IsEmpty)
                throw new ArgumentException(
                    "Cannot create a SliceGraph from an empty MorphologyGraph (no nodes). " +
                    "This usually means the OData query returned no annotations for the requested IDs " +
                    "(e.g. location IDs were queried as structure IDs, or the IDs/endpoint are wrong).",
                    nameof(graph));

            using var _phase = MeshPhaseTimings.Measure(MeshPhase.SliceGraphCreate, graph.Nodes.Count);

            Vector2 origin = xyOrigin ?? graph.NodesBoundingBox.CenterPoint.XY();
            SliceGraph output = new(graph, origin);

            SortedSet<MorphologyEdge> Edges = [.. graph.Edges.Values];

            Dictionary<ulong, SortedSet<ulong>> MorphNodeToSliceNodes = []; //Map a morphology node to all slice nodes it appears in.  Used to create edges

            ulong iNextKey = 0;
            while (Edges.Count > 0)
            {

                MorphologyEdge e = Edges.First();

                MorphologyNode Source = graph[e.SourceNodeKey];
                MorphologyNode Target = graph[e.TargetNodeKey];

                ZDirection SearchDirection = Source.Z < Target.Z ? ZDirection.Increasing : ZDirection.Decreasing;

                BuildMeshingCrossSection(graph, Source, Target, SearchDirection, out var MeshGroupNodesAbove, out SortedSet<ulong> MeshGroupNodesBelow, out SortedSet<MorphologyEdge> MeshGroupEdges);

                if (MeshGroupNodesAbove.Count == 0 || MeshGroupNodesBelow.Count == 0 || !MeshGroupEdges.Contains(e))
                {
                    LogSkippedCrossSection(e, MeshGroupNodesAbove.Count, MeshGroupNodesBelow.Count, MeshGroupEdges.Contains(e));
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

            output.MorphNodeToShape = await InitializeShapes(graph, -origin, tolerance);
            await output.InitializeSliceTopology(tolerance);

            /*output.SliceToTopology = new Dictionary<ulong, SliceTopology>(output.Nodes.Count);
            foreach(Slice s in output.Nodes.Values)
            {
                output.SliceToTopology[s.Key] = output.GetTopology(s);
            }
            */

            return output;
        }

        static void BuildMeshingCrossSection(MorphologyGraph graph, MorphologyNode seed, MorphologyNode partner, ZDirection CheckDirection, out SortedSet<ulong> NodesAbove, out SortedSet<ulong> NodesBelow, out SortedSet<MorphologyEdge> FollowedEdges)
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
                IncludePartnerOnOppositeSide(graph, seed, partner, NodesBelow, NewNodesAbove, ref FollowedEdges);
                FollowedEdges.UnionWith(NewNodesAbove.Select(n => new MorphologyEdge(graph, n, seed.ID)));
            }
            else
            {
                NodesAbove.Add(seed.ID);
                NewNodesBelow.UnionWith(seed.GetEdgesBelow(graph));
                IncludePartnerOnOppositeSide(graph, seed, partner, NodesAbove, NewNodesBelow, ref FollowedEdges);
                FollowedEdges.UnionWith(NewNodesBelow.Select(n => new MorphologyEdge(graph, n, seed.ID)));
            }

            BuildMeshingCrossSection(graph, ref NodesAbove, ref NodesBelow, NewNodesAbove, NewNodesBelow, ref FollowedEdges);
        }

        /// <summary>
        /// Same-section links are invisible to GetEdgesAbove/Below. The seed edge's other endpoint must still land on the opposite side.
        /// </summary>
        static void IncludePartnerOnOppositeSide(MorphologyGraph graph, MorphologyNode seed, MorphologyNode partner, SortedSet<ulong> seedSide, SortedSet<ulong> oppositeSide, ref SortedSet<MorphologyEdge> followedEdges)
        {
            if (partner is null || partner.ID == seed.ID || seedSide.Contains(partner.ID) || oppositeSide.Contains(partner.ID))
                return;

            oppositeSide.Add(partner.ID);
            followedEdges.Add(new MorphologyEdge(graph, partner.ID, seed.ID));
        }

        static void LogSkippedCrossSection(MorphologyEdge edge, int aboveCount, int belowCount, bool includesSeedEdge)
        {
            Trace.WriteLine($"Skipping mesh cross-section for edge {edge}: above={aboveCount}, below={belowCount}, includesSeedEdge={includesSeedEdge}");
        }

        /// <summary>
        /// Expands a meshing cross section above/below a seed. Morphology graphs may contain cycles (e.g. vasculature loops);
        /// when frontier expansion would revisit a node on the opposite side, that branch is pruned without mutating the graph.
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

            //Both frontiers have to be derived from the sets we arrived with.  Assigning to NewNodesBelow first and
            //then reading it back to build NewNodesAbove walks down and straight back up, so the upward expansion of
            //the nodes below is never explored.  The edges to those nodes are recorded above regardless, which left
            //the slice holding a link to a contour it does not contain and no way to tile across it.
            SortedSet<ulong> NextNodesBelow = [.. NewNodesAbove.SelectMany(n => graph[n].GetEdgesBelow(graph))];
            SortedSet<ulong> NextNodesAbove = [.. NewNodesBelow.SelectMany(n => graph[n].GetEdgesAbove(graph))];

            ulong[] cycleClosureAbove = [.. NodesAbove.Intersect(NextNodesBelow)];
            if (cycleClosureAbove.Length > 0)
                PruneCycleClosure("above", cycleClosureAbove, ref NextNodesBelow, NodesAbove);

            ulong[] cycleClosureBelow = [.. NodesBelow.Intersect(NextNodesAbove)];
            if (cycleClosureBelow.Length > 0)
                PruneCycleClosure("below", cycleClosureBelow, ref NextNodesAbove, NodesBelow);

            NextNodesAbove.ExceptWith(NodesAbove);
            NextNodesBelow.ExceptWith(NodesBelow);

            if (NextNodesAbove.Count == 0 && NextNodesBelow.Count == 0)
            {
                return;
            }
            else
            {
                BuildMeshingCrossSection(graph, ref NodesAbove, ref NodesBelow, NextNodesAbove, NextNodesBelow, ref FollowedEdges);
                return;
            }
        }

        static void PruneCycleClosure(string side, ulong[] nodeIds, ref SortedSet<ulong> nextFrontier, SortedSet<ulong> oppositeSide)
        {
            foreach (ulong id in nodeIds)
                Trace.WriteLine($"Location {id} closes a morphology cycle during mesh cross-section expansion ({side}); pruning frontier without removing graph edges.");

            nextFrontier.ExceptWith(oppositeSide);
        }

        /// <summary>
        /// Populates the lookup table mapping morph nodes to shapes.  Allows user option to simplify shapes.  Ensures all shapes have matching corresponding verticies if they participate in two or more slices
        /// </summary>
        /// <param name="tolerance"></param>
        private async Task InitializeSliceTopology(double tolerance = 0)
        {
            try
            {
                this.MorphNodeToShape ??= await SliceGraph.InitializeShapes(this.Graph, TranslationToCenter, tolerance);

                ConcurrentTopologyInitializer concurrentInitializer = new(this);

                this.SliceToTopology = await concurrentInitializer.InitializeSliceTopologyAsync(tolerance);

                RefreshMovedShapesFromCachedShapes();


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
                // Log and swallow: a partially built graph is still usable by the callers, and leaving
                // MorphNodeToShape/SliceToTopology unchanged is the safe default.
            }
        }

        /// <summary>
        /// A slice keeps a reference to the cached contour, so verticies a later slice inserts are picked up
        /// automatically - except on a shape virtual overlap moved, which is a copy taken before those insertions and
        /// can therefore be missing verticies its neighbour has.  The composite welds slices on (morph node, vertex
        /// index), and an index only means the same point in both slices if they list the same verticies, so a stale
        /// copy leaves that seam open.
        ///
        /// Every topology exists by the time this runs, so the cached contours are final and each moved copy can be
        /// brought up to date.  Mutating in place is deliberate: the topology's upper and lower arrays hold the same
        /// references, and the mesh is not built until later.
        /// </summary>
        private void RefreshMovedShapesFromCachedShapes()
        {
            if (SliceToTopology is null || MorphNodeToShape is null)
                return;

            foreach (SliceTopology topology in SliceToTopology.Values)
            {
                if (topology.Shapes is null
                    || topology.HasVirtualOverlapTranslation == false
                    || topology.ShapeIndexToMorphNodeIndex is null)
                    continue;

                for (int i = 0; i < topology.Shapes.Length; i++)
                {
                    Vector2 offset = topology.GetVirtualOverlapOffset(i);
                    if (offset == Vector2.Zero)
                        continue;

                    if (MorphNodeToShape.TryGetValue(topology.ShapeIndexToMorphNodeIndex[i], out IShape2D cached) == false)
                        continue;

                    //Polylines are left alone so the fork partition's cached vertex ranges stay valid.
                    if (topology.Shapes[i] is not Polygon moved || cached is not Polygon source)
                        continue;

                    //Interior rings would have to be reconciled the same way, and a contour with holes is not the
                    //case this addresses, so leave those alone rather than half-updating them.
                    if (moved.InteriorRings.Count > 0 || source.InteriorRings.Count > 0)
                        continue;

                    //Rebuilt from the cached ring rather than patched vertex by vertex.  Inserting the missing points
                    //into the copy would give it the same points but not necessarily at the same indices, since an
                    //insertion can land at index zero and rotate the ring, and the weld key is the index.  Assigning
                    //the whole ring makes the order identical by construction.  Mutating this shape rather than
                    //replacing it matters: the topology's upper and lower arrays hold the same reference.
                    moved.ExteriorRing = [.. source.ExteriorRing.Select(p => p + offset)];
                }
            }
        }

        /// <summary>
        /// Generate a dictionary of polygons we can use as a lookup table for shapes.
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="tolerance"></param>
        /// <returns></returns>
        public static Task<Dictionary<ulong, IShape2D>> InitializeShapes(MorphologyGraph graph, double tolerance = 0) =>
            InitializeShapes(graph, -graph.NodesBoundingBox.CenterPoint.XY(), tolerance);

        /// <summary>
        /// Cache simplified shapes translated by <paramref name="translationToCenter"/> (usually âˆ’cell XY origin).
        /// </summary>
        public static async Task<Dictionary<ulong, IShape2D>> InitializeShapes(MorphologyGraph graph, Vector2 translationToCenter, double tolerance = 0)
        {
            Dictionary<ulong, IShape2D> result = new(graph.Nodes.Count);

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
                                var morphNode = (MorphologyNode)node_;
                                try
                                {
                                    //Pass tolerance so rings that exceed MaxPolygonRingPointsBeforeSimplify are
                                    //Douglas–Peucker reduced before Polygon construction (avoids the huge-polygon assert).
                                    var poly = morphNode.Geometry.ToPolygon(tolerance);
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
                                            $"Could not simplify location #{morphNode.ID}.  Using original (more detailed) polygon instead.");
#if DEBUG
                                        Trace.WriteLine($"{e}");
#endif
                                        return poly;
                                    }
                                }
                                catch (Exception e)
                                {
                                    Trace.WriteLine(
                                        $"Skipping shape initialization for location #{morphNode.ID}: {e.Message}");
                                    return null;
                                }
                            }, node);

                            t.Start();
                            tasks.Add(t);
                        }
                        break;
                    case SupportedGeometryType.POLYLINE:
                        {
                            Task<IShape2D> t = new((node_) => ((MorphologyNode)node_).Geometry.ToPolyLine(tolerance).Translate(translationToCenter).Simplify(tolerance), node);
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
                catch (Exception e)
                {
                    Trace.WriteLine($"Could not initialize shape for a morphology node: {e.Message}");
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
            Debug.Assert(result.Shapes != null, "Slice topology initialisation returned no shapes.");
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
        private readonly record struct SliceShape(IShape2D Shape, bool IsUpper, double Z, ulong MorphNodeIndex, LocationType LocationType, Circle SourceCircle);

        internal SliceTopology GetSliceTopology(Slice group, IReadOnlyDictionary<ulong, IShape2D> polyLookup = null)
        {
            using var _phase = MeshPhaseTimings.Measure(MeshPhase.SliceTopology);

            List<SliceShape> sliceShapes = [];
            sliceShapes.AddRange(group.NodesAbove.Select(id => CreateSliceShape(id, true, polyLookup)));
            sliceShapes.AddRange(group.NodesBelow.Select(id => CreateSliceShape(id, false, polyLookup)));

            //Correspondence is computed over every shape in the slice, including shapes we cannot tile, because a
            //polyline still contributes corresponding verticies to the polygons it touches.
            List<IShape2D> ShapeList = [.. sliceShapes.Select(s => s.Shape)];
            bool[] sliceIsUpper = [.. sliceShapes.Select(s => s.IsUpper)];
            IShape2D[] workingShapes = [.. ShapeList];

            //Virtual overlap is measured over every shape in the slice, before tileability filtering, so a fork is
            //recognised from the annotator's links rather than from whatever survived the filter.
            bool[,] sliceLinks = BuildShapeLinkMatrix(group, sliceShapes, reportUnlinked: false);
            Vector2[] virtualOverlapOffsets = SliceTopology.TryTranslateNonOverlappingShapes(workingShapes, sliceIsUpper, sliceLinks);
            if (virtualOverlapOffsets is not null)
            {
                for (int i = 0; i < sliceShapes.Count; i++)
                    sliceShapes[i] = sliceShapes[i] with { Shape = workingShapes[i] };
                ShapeList = [.. workingShapes];
            }

            var correspondingPoints = ShapeList.AddCorrespondingVertices();

            Polygon[] Polygons = [.. ShapeList.OfType<Polygon>()];
            SliceTopology.AddPointsBetweenAdjacentCorrespondingVerticies(Polygons, correspondingPoints);

            Polyline[] Polylines = [.. ShapeList.OfType<Polyline>()];
            SliceTopology.AddPointsBetweenAdjacentCorrespondingVerticies(Polylines, correspondingPoints);

            ShareCorrespondenceWithCachedShapes(sliceShapes, virtualOverlapOffsets, polyLookup);

            //Polygons always tile. Polylines tile only when this slice has no polygon (gap-junction / raft
            //ribbon). A polyline on a polygon slice stays correspondence-only after the intersection verts above.
            bool sliceHasPolygon = Polygons.Length > 0;
            int[] tileableSourceIndex = [.. Enumerable.Range(0, sliceShapes.Count).Where(i => IsTileableForBajaj(sliceShapes[i].Shape, sliceHasPolygon))];
            SliceShape[] tileable = [.. tileableSourceIndex.Select(i => sliceShapes[i])];

            //Offsets are indexed by shape, so they have to be filtered alongside the shapes they belong to.
            Vector2[] tileableOffsets = virtualOverlapOffsets is null
                ? null
                : [.. tileableSourceIndex.Select(i => virtualOverlapOffsets[i])];

            if (tileable.Length != sliceShapes.Count)
                Trace.WriteLine($"Slice {group.Key}: {sliceShapes.Count - tileable.Length} of {sliceShapes.Count} shapes were excluded from tiling (correspondence-only polylines or unsupported types).");

            return new SliceTopology(group.Key,
                tileable.Select(s => s.Shape),
                tileable.Select(s => s.IsUpper),
                tileable.Select(s => s.Z),
                tileable.Select(s => s.MorphNodeIndex),
                this.SectionThickness,
                tileableOffsets,
                BuildShapeLinkMatrix(group, tileable),
                buildForkPartition: true,
                tileable.Select(s => s.LocationType),
                tileable.Select(s => s.SourceCircle));
        }

        /// <summary>
        /// Correspondence has to run after virtual overlap, because the verticies it inserts are the intersections
        /// between shapes and these shapes do not intersect until they are moved.  Moving a shape produces a private
        /// translated copy though, so those verticies land somewhere the other slice sharing that contour will never
        /// see them.  Copy them onto the cached contour, in its own untranslated coordinates, so they become visible
        /// to every slice that uses it.
        ///
        /// Polylines are excluded: <see cref="PolylineForkPartition"/> caches vertex indices and arc lengths for them
        /// when the topology is built, and inserting points would silently invalidate those ranges.
        /// </summary>
        private static void ShareCorrespondenceWithCachedShapes(IReadOnlyList<SliceShape> sliceShapes,
                                                               Vector2[] virtualOverlapOffsets,
                                                               IReadOnlyDictionary<ulong, IShape2D> polyLookup)
        {
            if (virtualOverlapOffsets is null || polyLookup is null)
                return;

            for (int i = 0; i < sliceShapes.Count; i++)
            {
                //A shape that did not move was never copied: it still is the cached shape, so it needs nothing.
                Vector2 offset = virtualOverlapOffsets[i];
                if (offset == Vector2.Zero)
                    continue;

                if (polyLookup.TryGetValue(sliceShapes[i].MorphNodeIndex, out IShape2D cached) == false)
                    continue;

                if (sliceShapes[i].Shape is not Polygon moved || cached is not Polygon target)
                    continue;

                foreach (Vector2 vertex in moved.ExteriorRing)
                    target.AddVertex(vertex - offset);
            }
        }

        /// <summary>
        /// Record which pairs of tileable shapes the annotator actually joined with a LocationLink.
        ///
        /// The matrix is indexed by position in <paramref name="tileable"/>, so callers must pass exactly the shape
        /// list the indices will be interpreted against: the filtered set for the returned SliceTopology, or the
        /// full slice for a pass that runs before filtering.
        /// </summary>
        private static bool[,] BuildShapeLinkMatrix(Slice group, IReadOnlyList<SliceShape> tileable, bool reportUnlinked = true)
        {
            //A node can legitimately appear on both sides of a slice, so map to the first shape index we saw for it.
            Dictionary<ulong, int> nodeToShape = new(tileable.Count);
            for (int i = 0; i < tileable.Count; i++)
                nodeToShape.TryAdd(tileable[i].MorphNodeIndex, i);

            bool[,] linked = new bool[tileable.Count, tileable.Count];

            //A shape is always considered linked to itself so contour and same-shape edges are never gated.
            for (int i = 0; i < tileable.Count; i++)
                linked[i, i] = true;

            foreach (MorphologyEdge edge in group.InternalEdges)
            {
                if (nodeToShape.TryGetValue(edge.SourceNodeKey, out int a) == false)
                    continue;

                if (nodeToShape.TryGetValue(edge.TargetNodeKey, out int b) == false)
                    continue;

                linked[a, b] = true;
                linked[b, a] = true;
            }

            //Only cross-band pairs are gated, so only those are worth reporting.  A slice with unlinked cross-band
            //pairs is a fork or a doubled-back chain, which is where mesh defects concentrate; naming them makes the
            //difference between "this slice changed because of the gate" and "this slice changed for another reason".
            List<string> unlinked = [];
            for (int i = 0; i < tileable.Count; i++)
            {
                for (int j = i + 1; j < tileable.Count; j++)
                {
                    if (tileable[i].IsUpper == tileable[j].IsUpper)
                        continue;

                    if (linked[i, j] == false)
                        unlinked.Add($"{tileable[i].MorphNodeIndex}/{tileable[j].MorphNodeIndex}");
                }
            }

            if (reportUnlinked && unlinked.Count > 0)
                Trace.WriteLine($"Slice {group.Key}: {unlinked.Count} unlinked cross-band shape pair(s) will not be tiled: {string.Join(", ", unlinked)}");

            return linked;
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
                //The cache omits shapes it could not prepare (tiny polygons, simplify failures). Cached
                //shapes are centered on the graph bounding box, so rebuild with the same translation.
                //Use ToShape2D so a polyline cache miss does not abort the entire slice via ToPolygon.
                shape = Graph[id].Geometry.ToShape2D().Translate(TranslationToCenter);
            }

            LocationType locationType = Graph[id].Location.TypeCode;
            Circle sourceCircle = default;
            if (locationType == LocationType.CIRCLE)
            {
                try
                {
                    Circle circle = Graph[id].Geometry.ToCircle();
                    if (polyLookup is not null)
                        circle = circle.Translate(TranslationToCenter);
                    sourceCircle = circle;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Slice shape {id}: could not read circle geometry ({ex.Message}); capping will use polygon rules.");
                    locationType = LocationType.POLYGON;
                }
            }

            return new SliceShape(shape, isUpper, Graph[id].Z, id, locationType, sourceCircle);
        }

        /// <summary>
        /// Polygons always enter GenerateFaces. Polylines tile only when the slice has no polygon
        /// (gap-junction / raft ribbon). A polyline that merely crosses a cell is correspondence-only.
        /// </summary>
        internal static bool IsTileableForBajaj(IShape2D shape, bool sliceHasPolygon)
        {
            if (shape is Polygon)
                return true;
            if (shape is Polyline)
                return sliceHasPolygon == false;
            return false;
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
