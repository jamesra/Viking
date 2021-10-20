using AnnotationVizLib;
using Geometry;
using GraphLib;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

        /// <summary>
        /// Caches the shape of each morphology node in the slice graph.  After corresponding verticies are added this cache is used to ensures each section will get the same input shapes
        /// The map can also be used to support simplifying shapes.
        /// </summary>
        internal Dictionary<ulong, IShape2D> MorphNodeToShape = null;

        private Dictionary<ulong, SliceTopology> SliceToTopology = null;

        /// <summary>
        /// The center of the bounding box of all slices in the graph
        /// </summary>
        public GridBox BoundingBox { get { return Graph.BoundingBox; } }
        
        private SliceGraph(MorphologyGraph graph)
        {
            this.Graph = graph;
        }

        internal static async Task<SliceGraph> Create(MorphologyGraph graph, double tolerance=0)
        { 
            SliceGraph output = new SliceGraph(graph);

            SortedSet<MorphologyEdge> Edges = new SortedSet<MorphologyEdge>(graph.Edges.Values);

            Dictionary<ulong, SortedSet<ulong>> MorphNodeToSliceNodes = new Dictionary<ulong, SortedSet<ulong>>(); //Map a morphology node to all slice nodes it appears in.  Used to create edges

            ulong iNextKey = 0;
            while (Edges.Count > 0)
            {
                SortedSet<ulong> MeshGroupNodesAbove;
                SortedSet<ulong> MeshGroupNodesBelow;
                SortedSet<MorphologyEdge> MeshGroupEdges;
                 
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

                BuildMeshingCrossSection(graph, Source, SearchDirection, out MeshGroupNodesAbove, out MeshGroupNodesBelow, out MeshGroupEdges);
                 
                if (graph.Edges.ContainsKey(e)) //If the edge wasn't removed to stop a cycle it should be in the result set
                {
                    Debug.Assert(MeshGroupNodesAbove.Count > 0, "Search should have found at least one node above and below.");
                    Debug.Assert(MeshGroupNodesBelow.Count > 0, "Search should have found at least one node above and below.");
                    Debug.Assert(MeshGroupEdges.Contains(e), "The edge we used to start the search is not in the search results.");
                    if(MeshGroupEdges.Contains(e) == false) //This is an edge cases that shouldn't happen if deleting edges from graphs works, but removing the edge from our list so we don't loop infinitely should fix it
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
                Slice group = new Slice(iNextKey, MeshGroupNodesAbove, MeshGroupNodesBelow, MeshGroupEdges);

                foreach(ulong id in group.AllNodes)
                {
                    if (MorphNodeToSliceNodes.ContainsKey(id) == false)
                        MorphNodeToSliceNodes[id] = new SortedSet<ulong>();

                    MorphNodeToSliceNodes[id].Add(iNextKey);
                }
                 
                output.AddNode(group);

                Edges.ExceptWith(MeshGroupEdges);

                iNextKey = iNextKey + 1; 
            }

            //Create edges between sections in the new graph to indicate how sections need to anneal in the final merged mesh
            foreach(var morph_id in graph.Nodes.Keys)
            {
                bool hasKey = MorphNodeToSliceNodes.TryGetValue(morph_id, out SortedSet<ulong> SlicesForMorphNode);
                if (false == hasKey)
                    continue;

                if (SlicesForMorphNode.Count < 2)
                    continue;

                foreach (var pair in SlicesForMorphNode.ToArray().CombinationPairs<ulong>())
                {
                    Edge<ulong> edge = new Edge<ulong>(pair.A, pair.B, false);

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
                        else if(B.NodesAbove.Contains(morph_id) && A.NodesBelow.Contains(morph_id))
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

        internal class CycleInGraphException : Exception
        {
            /// <summary>
            /// This set is not a path that describes a cycle.  It is a set of nodes who have cycles that may or may not be the same.
            /// </summary>
            public ulong[] NodesWithACycle;

            public CycleInGraphException(ulong[] cycle, string msg=null) : base(msg)
            {
                NodesWithACycle = cycle;
            }
        }



        static void BuildMeshingCrossSection(MorphologyGraph graph, MorphologyNode seed, ZDirection CheckDirection, out SortedSet<ulong> NodesAbove, out SortedSet<ulong> NodesBelow, out SortedSet<MorphologyEdge> FollowedEdges)
        {
            NodesAbove = new SortedSet<ulong>();
            NodesBelow = new SortedSet<ulong>();
            SortedSet<ulong> NewNodesAbove = new SortedSet<ulong>();
            SortedSet<ulong> NewNodesBelow = new SortedSet<ulong>();

            FollowedEdges = new SortedSet<MorphologyEdge>();

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
            catch(CycleInGraphException e)
            {
                //Try to remove the cycle and try again if we succeeded, otherwiese we need to fail this cross section generation
                if(TryRemoveCycle(graph, e.NodesWithACycle))
                {
                    BuildMeshingCrossSection(graph, seed, CheckDirection, out NodesAbove, out NodesBelow, out FollowedEdges);
                    return;
                }
                else
                {
                    ///Hmm... return what we have?
                    Console.WriteLine("Bailing out of one MeshingCrossSection build because I found a cycle at {e.NodesWithACycle[0]} I couldn't remove automatically.");
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

            NewNodesBelow = new SortedSet<ulong>(NewNodesAbove.SelectMany(n => graph[n].GetEdgesBelow(graph)));
            NewNodesAbove = new SortedSet<ulong>(NewNodesBelow.SelectMany(n => graph[n].GetEdgesAbove(graph)));

            var CycleWithAbove = NodesAbove.Intersect(NewNodesBelow).ToArray();
            if (CheckForCycle(CycleWithAbove))
                throw new CycleInGraphException(CycleWithAbove);

            var CycleWithBelow = NodesBelow.Intersect(NewNodesAbove).ToArray();
            if(CheckForCycle(CycleWithBelow))
                throw new CycleInGraphException(CycleWithAbove);

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
                    Trace.WriteLine(string.Format("Location {0} forms a cycle in the morphology graph", id));
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

            foreach(var id in cycle_ids)
            {
                //Find a cycle path, find the longest edge, and break it
                var cycle = graph.FindCycle(id); 
                if(cycle == null)
                {
                    Trace.WriteLine($"I couldn't find a cycle for location {id}, which is weird because I found one earlier.  Bug in the graph cycle travelling code?");
                }

                //Measure the distance in Z between all nodes in the cycle.  Remove the edge with the largest difference.
                MorphologyNode current = graph[cycle[0]];
                SortedList<double, MorphologyEdge> sortedEdgeLength = new SortedList<double, MorphologyEdge>();
                for(int i = 1; i < cycle.Count-1; i++)
                {
                    MorphologyNode next = graph[cycle[i]];

                    //I'm just using straight Z distance as my metric, but it could be XYZ if it doesn't work well.
                    double distance = current.Z - next.Z;
                    MorphologyEdge edge = new MorphologyEdge(graph, current.Key, next.Key);
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
            if(this.MorphNodeToShape == null)
                this.MorphNodeToShape = await SliceGraph.InitializeShapes(this.Graph, tolerance);

            ConcurrentTopologyInitializer concurrentInitializer = new ConcurrentTopologyInitializer(this);

            this.SliceToTopology = concurrentInitializer.InitializeSliceTopology();

            /*
            //Create corresponding verticies for all shapes
            foreach (var node in this.Nodes.Values)
            {
                SliceTopology st = GetSliceTopology(node, MorphNodeToShape);

                //Add corresponding verticies.  Will insert into the polygons without creating new ones, which will update MorphNodeToShape
                //List<GridVector2> correspondingPoints = st.Polygons.AddCorrespondingVerticies();
                 
                //AddPointsBetweenAdjacentCorrespondingVerticies(st.Polygons,  correspondingPoints);
            }
            */

            return;
        }

        /// <summary>
        /// Generate a dictionary of polygons we can use as a lookup table for shapes.
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="tolerance"></param>
        /// <returns></returns>
        public static async Task<Dictionary<ulong, IShape2D>> InitializeShapes(MorphologyGraph graph, double tolerance = 0)
        {
            var result = new Dictionary<ulong, IShape2D>(graph.Nodes.Count);

            GridVector2 translationToCenter = -graph.BoundingBox.CenterPoint.XY();

            List<Task<IShape2D>> tasks = new List<Task<IShape2D>>(graph.Nodes.Count);
            foreach (var node in graph.Nodes.Values)
            {
                if (node.Geometry == null)
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
                            Task<IShape2D> t = new Task<IShape2D>((node_) => ((MorphologyNode)node_).Geometry.ToPolygon().Translate(translationToCenter).Simplify(tolerance), node);
                            t.Start();
                            tasks.Add(t);
                        }
                            break;
                    case SupportedGeometryType.POLYLINE:
                        {
                            Task<IShape2D> t = new Task<IShape2D>((node_) => ((MorphologyNode)node_).Geometry.ToPolyLine().Translate(translationToCenter).Simplify(tolerance), node);
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
                    //Rounding exposed a rare bug on 82682, 82680 RPC1 where the inner hole was exactly over the exterior ring of the opposite polygon
                    if(output is GridPolygon poly)
                    {
                        result[(ulong)((MorphologyNode)(task.AsyncState)).ID] = poly.Round(Global.SignificantDigits);
                    }
                    else if (output is GridPolyline line)
                    {
                        result[(ulong)((MorphologyNode)(task.AsyncState)).ID] = line.Round(Global.SignificantDigits);
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
            if (SliceToTopology == null)
                SliceToTopology = new Dictionary<ulong, SliceTopology>(this.Nodes.Count);

            if (false == SliceToTopology.ContainsKey(slice.Key))
            {
                //If we are taking this path there is a danger corresponding verticies won't exist across multiple slices
                SliceToTopology[slice.Key] = GetSliceTopology(slice, MorphNodeToShape);
            }

            return SliceToTopology[slice.Key];
        }

        public SliceTopology GetTopology(ulong sliceKey)
        {
            if (SliceToTopology == null)
                SliceToTopology = new Dictionary<ulong, SliceTopology>(this.Nodes.Count);

            if (false == SliceToTopology.ContainsKey(sliceKey))
            {
                //If we are taking this path there is a danger corresponding verticies won't exist across multiple slices
                SliceToTopology[sliceKey] = GetSliceTopology(sliceKey, MorphNodeToShape);
            }

            return SliceToTopology[sliceKey];
        }

        private SliceTopology GetSliceTopology(ulong sliceKey, Dictionary<ulong, IShape2D> polyLookup = null)
        {
            return GetSliceTopology(this[sliceKey], polyLookup);
        }

        internal SliceTopology GetSliceTopology(Slice group)
        {
            return this.GetSliceTopology(group, this.MorphNodeToShape);
        }

        internal SliceTopology GetSliceTopology(Slice group, Dictionary<ulong, IShape2D> polyLookup = null)
        {
            var ShapeList = new List<IShape2D>();
            var IsUpper = new List<bool>();
            var ShapeZ = new List<double>();
            var VertexShapeIndexToMorphNodeIndex = new List<ulong>();

            if (polyLookup != null)
            {
                ShapeList.AddRange(group.NodesAbove.Select(id => polyLookup.ContainsKey(id) ? polyLookup[id] : Graph[id].Geometry.ToPolygon()));
                ShapeList.AddRange(group.NodesBelow.Select(id => polyLookup.ContainsKey(id) ? polyLookup[id] : Graph[id].Geometry.ToPolygon()));
            }
            else
            {
                ShapeList.AddRange(group.NodesAbove.Select(id => Graph[id].Geometry.ToShape2D()));
                ShapeList.AddRange(group.NodesBelow.Select(id => Graph[id].Geometry.ToShape2D()));
            }


            VertexShapeIndexToMorphNodeIndex.AddRange(group.NodesAbove);
            VertexShapeIndexToMorphNodeIndex.AddRange(group.NodesBelow);

            IsUpper.AddRange(group.NodesAbove.Select(id => true));
            IsUpper.AddRange(group.NodesBelow.Select(id => false));

            ShapeZ.AddRange(group.NodesAbove.Select(id => Graph[id].Z));
            ShapeZ.AddRange(group.NodesBelow.Select(id => Graph[id].Z));


            //

            //Todo Monday, this should not be in the constructor or the class I think.
            //Add corresponding points until we've run out of new correspondances
            var correspondingPoints = ShapeList.AddCorrespondingVerticies();

            /*
            List<GridVector2> novelCorrespondingPoints = correspondingPoints.ToList();
            do
            {
                var nudgedPoints = SliceTopology.NudgeCorrespondingVerticies(Polygons, novelCorrespondingPoints);
                */
            GridPolygon[] Polygons = ShapeList.Where(s => s is GridPolygon).Cast<GridPolygon>().ToArray();
            SliceTopology.AddPointsBetweenAdjacentCorrespondingVerticies(Polygons, correspondingPoints);

            GridPolyline[] Polylines = ShapeList.Where(l => l is GridPolyline).Cast<GridPolyline>().ToArray();
            SliceTopology.AddPointsBetweenAdjacentCorrespondingVerticies(Polylines, correspondingPoints);
            
            /*
                var NewCorresponingPoints = Polygons.AddCorrespondingVerticies();
//                novelCorrespondingPoints = NewCorresponingPoints.Where(p => correspondingPoints.Contains(p) == false).ToList();
                //correspondingPoints = NewCorresponingPoints;
            }
            while (novelCorrespondingPoints.Count > 0);
            */

            SliceTopology output = new SliceTopology(group.Key, Polygons, IsUpper, ShapeZ, VertexShapeIndexToMorphNodeIndex);

            return output;
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

        public bool HasSliceAbove { get; internal set; } = false;
        public bool HasSliceBelow { get; internal set; } = false;

        public Slice(ulong key, SortedSet<ulong> nodesAbove, SortedSet<ulong> nodesBelow, SortedSet<MorphologyEdge> edges) : base(key)
        {
            //this.Graph = graph;
            this.NodesAbove = nodesAbove;
            this.NodesBelow = nodesBelow;
            this.InternalEdges = edges;
            var allNodes = new SortedSet<ulong>(NodesAbove);
            allNodes.UnionWith(NodesBelow);
            AllNodes = allNodes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

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
            var group = obj as Slice;
            return group != null &&
                   EqualityComparer<SortedSet<ulong>>.Default.Equals(NodesAbove, group.NodesAbove) &&
                   EqualityComparer<SortedSet<ulong>>.Default.Equals(NodesBelow, group.NodesBelow) &&
                   EqualityComparer<SortedSet<MorphologyEdge>>.Default.Equals(InternalEdges, group.InternalEdges);
        }

        public override int GetHashCode()
        {
            var hashCode = -668766393;
            hashCode = hashCode * -1521134295 + EqualityComparer<SortedSet<ulong>>.Default.GetHashCode(NodesAbove);
            hashCode = hashCode * -1521134295 + EqualityComparer<SortedSet<ulong>>.Default.GetHashCode(NodesBelow);
            return hashCode;
        }
    }

    /// <summary>
    /// Describes the shapes and relationships for a given slice
    /// </summary>
    public struct SliceTopology
    {
        //TODO: Document limitations for shapes we know should not link to each other in the final model by using LocationLink entries.

        /// <summary>
        /// The slice key if this topology was generated from a Slice node in a SliceGraph
        /// </summary>
        public readonly ulong SliceKey;

        /// <summary>
        /// All polygons in the topology
        /// </summary>
        public readonly GridPolygon[] Polygons;

        /// <summary>
        /// True if the polygon belongs to the upper set of shapes
        /// </summary>
        public readonly bool[] IsUpper;

        /// <summary>
        /// Z level of each Polygon
        /// </summary>
        public readonly double[] PolyZ;

        /// <summary>
        /// Map Polygons[] index from this topology to the morphology node key generating that polygon in the parent SliceGraph
        /// </summary>
        public readonly ulong[] PolyIndexToMorphNodeIndex;

        /// <summary>
        /// Map a UpperPolygons index to Polygons index
        /// </summary>
        public readonly ImmutableSortedSet<int> UpperPolyIndicies;

        /// <summary>
        /// Map a LowerPolygons index to Polygons index
        /// </summary>
        public readonly ImmutableSortedSet<int> LowerPolyIndicies;

        /// <summary>
        /// Set of Polygons in the upper set
        /// </summary>
        internal readonly GridPolygon[] UpperPolygons;

        /// <summary>
        /// Set of Polygons in the lower set
        /// </summary>
        internal readonly GridPolygon[] LowerPolygons;

        /// <summary>
        /// The translation vector to position this slice in world space.
        /// </summary>
        //public readonly GridVector2 Offset;

        public SliceTopology(ulong key, IEnumerable<GridPolygon> polygons, IEnumerable<bool> isUpper, IEnumerable<double> polyZ, IEnumerable<ulong> polyIndexToMorphNodeIndex)
            : this(polygons, isUpper, polyZ, polyIndexToMorphNodeIndex)
        {
            SliceKey = key;
        }

        public SliceTopology(IEnumerable<GridPolygon> polygons, IEnumerable<bool> isUpper, IEnumerable<double> polyZ, IEnumerable<ulong> polyIndexToMorphNodeIndex=null)
        {
            SliceKey = 0;

            //GridVector2 Center = polygons.BoundingBox().Center;

            //Translate all polygons as close to the origin as possible.  We'll move them back once we assemble the mesh.
            //Polygons = polygons.Select(p => p.Translate(-Center)).ToArray();
            //Offset = Center;
            Polygons = polygons.ToArray();
            IsUpper = isUpper.ToArray();
            PolyZ = polyZ.ToArray();

            PolyIndexToMorphNodeIndex = polyIndexToMorphNodeIndex == null ? null : polyIndexToMorphNodeIndex.ToArray();

            //Assign polys to sets for convienience later
            CalculateUpperAndLowerPolygons(IsUpper, Polygons, out UpperPolygons, out UpperPolyIndicies, out LowerPolygons, out LowerPolyIndicies);
        }

        /// <summary>
        /// The delaunay implementation floating point rounding errors are most common on colinear points.  To mitigate this I nudge corresponding points to match the expected curvature of the shape the correlate with
        /// </summary>
        internal static List<GridVector2> NudgeCorrespondingVerticies(GridPolygon[]  Polygons, List<GridVector2> correspondingPoints)
        {
            Dictionary<GridVector2, List<PolygonIndex>> pointToIndexList = new Dictionary<GridVector2, List<PolygonIndex>>();
            //GridPolygon[] Polygons = this.Polygons;

            for (int iPoly = 0; iPoly < Polygons.Length; iPoly++)// GridPolygon poly in Polygons)
            {
                GridPolygon poly = Polygons[iPoly];
                List<PolygonIndex> correspondingIndicies = poly.TryGetIndicies(correspondingPoints);
                if (correspondingIndicies == null || correspondingIndicies.Count == 0)
                    continue;

                for (int i = 0; i < correspondingIndicies.Count; i++)
                {
                    PolygonIndex pi = correspondingIndicies[i];
                    GridVector2 cp = pi.Point(poly);

                    if (pointToIndexList.ContainsKey(cp))
                    {
                        pointToIndexList[cp].Add(pi.Reindex(iPoly));
                    }
                    else
                    {
                        List<PolygonIndex> listPI = new List<PolygonIndex>();
                        listPI.Add(pi.Reindex(iPoly));
                        pointToIndexList.Add(cp, listPI);
                    }
                }
            }

            List<GridVector2> UpdatedPoints = new List<GridVector2>();

            foreach (GridVector2 cp in pointToIndexList.Keys)
            {
                List<PolygonIndex> correspondingIndicies = pointToIndexList[cp];

                GridVector2[] points = correspondingIndicies.Select(ci => ci.PredictPoint(Polygons)).ToArray();
                GridVector2 avg = points.Average();
                  
                try
                {
                    foreach (PolygonIndex pi in correspondingIndicies)
                    {
                        pi.SetPoint(Polygons, avg);
                    }

                    UpdatedPoints.Add(avg);
                }
                catch(ArgumentException e)
                {
                    foreach (PolygonIndex pi in correspondingIndicies)
                    {
                        pi.SetPoint(Polygons, cp);
                    }

                    UpdatedPoints.Add(cp);
                }
                
            }

            return UpdatedPoints;
        }
        /*
                SortedList<PointIndex, GridVector2> PointsToInsert = new SortedList<PointIndex, GridVector2>();

                correspondingIndicies.Sort(); //Sort the indicies so we can simplify our search.

                IIndexSet loopingIndex = new InfiniteSequentialIndexSet(0, correspondingIndicies.Count, 0);
                PointIndex Current = correspondingIndicies[0];
                for (long i = 0; i < correspondingIndicies.Count; i++)
                {
                    int iNext = (int)loopingIndex[i + 1];
                    PointIndex Next = correspondingIndicies[iNext];

                    if (Current.Next == Next)
                    {
                        //This means two corresponding points are adjacent and we need to insert a midpoint into the polygon between them.
                        GridVector2[] midPoint = CatmullRom.FitCurveSegment(Current.Previous.Point(poly),
                                                   Current.Point(poly),
                                                   Next.Point(poly),
                                                   Next.Next.Point(poly),
                                                   new double[] { 0.5 });

                        //Adding the point will change index of all PointIndex values so we wait until the end
                        PointsToInsert.Add(Current, midPoint[0]);
                    }

                    Current = Next;
                }

                //Reverse the order of our list of points to add so we do not break polygon indicies.  Then insert our points
                foreach (var addition in PointsToInsert.Reverse())
                {
                    poly.AddVertex(addition.Value, addition.Key);
                }
            }
        }
        */


        /// <summary>
        /// We need to handle the case where a single vertex is on the other side of the contour boundary and creates
        /// two corresponding vertices which are tightly grouped
        /// 
        //       3
        ///     / \
        /// A--2-B-4--C
        ///   /     \
        ///  1       5
        /// </summary>
        internal static void RemoveAdjacentCorrespondingVerticies(GridPolygon[] Polygons, List<GridVector2> correspondingPoints)
        {
            foreach (GridPolygon poly in Polygons)
            {
                List<PolygonIndex> correspondingIndicies = poly.TryGetIndicies(correspondingPoints);
                if (correspondingIndicies == null || correspondingIndicies.Count == 0)
                    continue;

            }
        }

        /// <summary>
        /// We need to handle the case where the face generated for a corresponding edge will contain other verticies.
        /// We can do this by subdividing the edge between 1-2 and A-B
        /// 
        //         3---4
        ///       /     \
        /// A----2B---C--D5
        /// | X /         \
        /// |  /           \
        /// | /             \
        /// 1                6
        /// </summary>
        internal static void HandleCorrespondingFaceContainsVertex(GridPolygon[] Polygons, List<GridVector2> correspondingPoints)
        {
            GridRectangle bbox = Polygons.BoundingBox();
            bbox = GridRectangle.Scale(bbox, 1.05); //Grow the box slightly so the QuadTree will never resize for a rounding error
            QuadTree<List<PolygonIndex>> tree = new QuadTree<List<PolygonIndex>>(bbox);

            PolySetVertexEnum indexEnum = new PolySetVertexEnum(Polygons);
            foreach(PolygonIndex index in indexEnum)
            {
                GridVector2 p = index.Point(Polygons);
                List<PolygonIndex> existing = tree.FindNearest(p, out GridVector2 foundPoint, out double distance);
                if (foundPoint == p) //A corresponding point has already been added
                {
                    existing.Add(index);
                }
                else
                {
                    existing = new List<PolygonIndex>(2);
                    existing.Add(index);
                    tree.Add(p, existing);
                }
            }

            var PointIndexArrays = Polygons.IndiciesForPoints(correspondingPoints);

            foreach (PolygonIndex[] indicies in PointIndexArrays)
            {
                if (indicies.Length == 0)
                    continue;

                double minDistance;
                GridVector2 vertexPosition = indicies[0].Point(Polygons); //The corresponding point position
                var nearestIndexList = tree.FindNearestPoints(vertexPosition, 2); //Find the nearest two points. The first should be ourselves at 0 distance.  The 2nd should be the closest point to us.

                if(nearestIndexList.Count < 2)
                {
                    throw new InvalidOperationException("We should be able to find at least two points when searching a QuadTree containing multiple polygons");
                }

                Debug.Assert(nearestIndexList[0].Point == vertexPosition, "I expected the vertex to be the closest vertex to itself, why wasn't it found?");
                
                minDistance = nearestIndexList[1].Distance;
                var nearestIndex = nearestIndexList[1].Value;

                foreach (PolygonIndex pi in indicies)
                {
                    GridPolygon poly = pi.Polygon(Polygons);
                    
                    GridVector2 vertex = pi.Point(Polygons);
                    GridVector2 next = pi.Next.Point(Polygons);
                    GridVector2 prev = pi.Previous.Point(Polygons);

                    if (nearestIndex.Contains(pi.Next) == false) //Don't add a vertex that is already there and risk a rounding error
                    {
                        GridLineSegment lineToNext = new GridLine(vertex, next).ToLine(minDistance);
                        poly.AddVertex(lineToNext.B);
                    }

                    if(nearestIndex.Contains(pi.Previous) == false) //Don't add a vertex that is already there and risk a rounding error
                    {
                        GridLineSegment lineToPrev = new GridLine(vertex, prev).ToLine(minDistance);
                        poly.AddVertex(lineToPrev.B);
                    }
                }
            }
        }

        /// <summary>
        /// We need to handle the case where the face generated for a corresponding edge will contain other verticies.
        /// We can do this by subdividing the edge between 1-2 and A-B
        /// 
        //         3---4
        ///       /     \
        /// A----2B---C--D5
        /// | X /         \
        /// |  /           \
        /// | /             \
        /// 1                6
        /// 
        /// This implementation simply adds additional verticies that bracket the corresponding vertex at equidistant points to the nearest non-corresponding vertex
        /// </summary>
        internal static void BracketCorrespondingPoints(GridPolygon[] Polygons, List<GridVector2> correspondingPoints)
        {
            foreach (GridPolygon poly in Polygons)
            {
                List<PolygonIndex> correspondingIndicies = poly.TryGetIndicies(correspondingPoints);
                if (correspondingIndicies == null || correspondingIndicies.Count == 0)
                    continue;

                SortedList<PolygonIndex, GridVector2> PointsToInsert = new SortedList<PolygonIndex, GridVector2>();
                correspondingIndicies.Sort(); //Sort the indicies so we can simplify our search. 
            }
        }

        /// <summary>
        /// Due to details of the implementation of our bajaj algorithm we need to add a point between adjacent corresponding points on a polygon
        /// </summary>
        internal static void AddPointsBetweenAdjacentCorrespondingVerticies(GridPolygon[] Polygons, List<GridVector2> correspondingPoints)
        {
            foreach(GridPolygon poly in Polygons)
            { 
                List<PolygonIndex> correspondingIndicies = poly.TryGetIndicies(correspondingPoints);
                if (correspondingIndicies == null || correspondingIndicies.Count == 0)
                    continue;

                SortedList<PolygonIndex, GridVector2> PointsToInsert = new SortedList<PolygonIndex, GridVector2>();
                correspondingIndicies.Sort(); //Sort the indicies so we can simplify our search.

                IIndexSet loopingIndex = new InfiniteSequentialIndexSet(0, correspondingIndicies.Count, 0);
                PolygonIndex Current = correspondingIndicies[0];
                for (long i = 0; i < correspondingIndicies.Count; i++)
                {
                    int iNext = (int)loopingIndex[i + 1];
                    PolygonIndex Next = correspondingIndicies[iNext];

                    if(Current.Next == Next)
                    {
                        //This means two corresponding points are adjacent and we need to insert a midpoint into the polygon between them.
                        GridVector2[] midPoint = CatmullRom.FitCurveSegment(Current.Previous.Point(poly),
                                                   Current.Point(poly),
                                                   Next.Point(poly),
                                                   Next.Next.Point(poly),
                                                   new double[] { 0.5 });

                        //Adding the point will change index of all PointIndex values so we wait until the end
                        PointsToInsert.Add(Current.Next, midPoint[0]);
                    }

                    Current = Next; 
                }

                //Reverse the order of our list of points to add so we do not break polygon indicies.  Then insert our points
                foreach(var addition in PointsToInsert.Reverse())
                {
                    //Trace.WriteLine(string.Format("Add vertex after {0}", addition));
                    //Insert the vertex, adjust the size of the ring in case we've already inserted into it.
                    poly.InsertVertex(addition.Value, addition.Key.ReindexToSize(poly));
                }
            }
        }

        /// <summary>
        /// Due to details of the implementation of our bajaj algorithm we need to add a point between adjacent corresponding points on a polygon
        /// </summary>
        internal static void AddPointsBetweenAdjacentCorrespondingVerticies(GridPolyline[] Polylines, List<GridVector2> correspondingPoints)
        {
            foreach (GridPolyline line in Polylines)
            {
                List<PolylineIndex> correspondingIndicies = line.TryGetIndicies(correspondingPoints);
                if (correspondingIndicies == null || correspondingIndicies.Count == 0)
                    continue;

                SortedList<int, GridVector2> PointsToInsert = new SortedList<int, GridVector2>();
                correspondingIndicies.Sort(); //Sort the indicies so we can simplify our search.
                 
                PolylineIndex Current = correspondingIndicies[0];
                for (long i = 0; i < correspondingIndicies.Count-1; i++)
                {
                    PolylineIndex Next = correspondingIndicies[(int)i + 1];

                    if (Current.Next == Next)
                    { 
                        //This means two corresponding points are adjacent and we need to insert a midpoint into the polygon between them.
                        GridVector2[] midPoint = CatmullRom.FitCurveSegment(line.Points,
                                                   Current.iVertex,
                                                   new double[] {0.5}
                                                   );

                        //Adding the point will change index of all PointIndex values so we wait until the end
                        PointsToInsert.Add(Current.iVertex, midPoint[0]);
                    }

                    Current = Next;
                }

                //Reverse the order of our list of points to add so we do not break polygon indicies.  Then insert our points
                foreach (var addition in PointsToInsert.Reverse())
                {
                    //Trace.WriteLine(string.Format("Add vertex after {0}", addition));
                    //Insert the vertex, adjust the size of the ring in case we've already inserted into it.
                    line.Insert(addition.Key, addition.Value);
                }
            }
        }

        private static void CalculateUpperAndLowerPolygons(bool[] IsUpper, GridPolygon[] Polygons, out GridPolygon[] UpperPolygons, out ImmutableSortedSet<int> UpperPolyIndicies, out GridPolygon[] LowerPolygons, out ImmutableSortedSet<int> LowerPolyIndicies)
        {
            int nUpper = IsUpper.Count(u => u == true);
            int nLower = Polygons.Length - nUpper;

            UpperPolygons = new GridPolygon[nUpper];
            LowerPolygons = new GridPolygon[nLower];

            int[] UpperPolygonIndex = new int[nUpper];
            int[] LowerPolygonIndex = new int[nLower];

            int iUpper = 0;
            int iLower = 0;
            for (int i = 0; i < IsUpper.Length; i++)
            {
                if(IsUpper[i])
                {
                    UpperPolygons[iUpper] = Polygons[i];
                    UpperPolygonIndex[iUpper] = i;
                    iUpper += 1;
                }
                else
                {
                    LowerPolygons[iLower] = Polygons[i];
                    LowerPolygonIndex[iLower] = i; 
                    iLower += 1;
                }
            }

            UpperPolyIndicies = UpperPolygonIndex.ToImmutableSortedSet<int>();
            LowerPolyIndicies = LowerPolygonIndex.ToImmutableSortedSet<int>();

            return;
        }
         
    }

    /// <summary>
    /// Creates the topology for all nodes in a SliceGraph in parallel while ensuring that at no time is a single shape being modified for two slice nodes at the same time.
    /// </summary>
    internal class ConcurrentTopologyInitializer
    {
        SliceGraph Graph;

        SortedSet<ulong> UnprocessedSlices = null;
        SortedSet<ulong> SlicesWithActiveTasks = new SortedSet<ulong>();
        SortedSet<ulong> CompletedSlices = new SortedSet<ulong>();

        System.Threading.ReaderWriterLockSlim rwLock = new System.Threading.ReaderWriterLockSlim();
        System.Threading.ManualResetEventSlim AllDoneEvent = new System.Threading.ManualResetEventSlim();

        Dictionary<ulong, SliceTopology> SliceToTopology;

        public ConcurrentTopologyInitializer(SliceGraph graph)
        {
            Graph = graph;
            UnprocessedSlices = new SortedSet<ulong>(Graph.Nodes.Keys);
            SliceToTopology = new Dictionary<ulong, SliceTopology>(Graph.Nodes.Count);
        }

        private void OnTopologyComplete(Slice s, SliceTopology st)
        {
            try
            {
                rwLock.EnterWriteLock();

                SliceToTopology.Add(s.Key, st);

                SlicesWithActiveTasks.Remove(s.Key);
                CompletedSlices.Add(s.Key);

                foreach(ulong adjacent in s.Edges.Keys)
                {
                    TryStartSlice(adjacent);
                }

                if(UnprocessedSlices.Count == 0 && SlicesWithActiveTasks.Count == 0)
                {
                    AllDoneEvent.Set();
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Return true if a task can be safely launched for this slice
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private bool CanStartSlice(Slice node)
        { 
            if (UnprocessedSlices.Contains(node.Key) == false)
                return false;

            //Do not process a slice if the adjacent slices are being processed and could change the polygons it would be compared against
            if (node.Edges.Keys.Any(key => SlicesWithActiveTasks.Contains(key)))
            {
                return false;
            }

            return true; 
        }

        /// <summary>
        /// If a slice is eligible to be processed then start a task.
        /// </summary>
        /// <param name="slice_id"></param>
        /// <returns></returns>
        private Task TryStartSlice(ulong slice_id)
        {
            Slice slice = Graph[slice_id];

            if (CanStartSlice(slice) == false)
                return null;

            UnprocessedSlices.Remove(slice_id);
            SlicesWithActiveTasks.Add(slice_id);

            Task topologyTask = Task.Run(() =>
            {
                SliceTopology st;
                try
                {
                    st = Graph.GetSliceTopology(slice);
                    this.OnTopologyComplete(slice, st);
                }
                catch(Exception e)
                {
                    this.OnTopologyComplete(slice, new SliceTopology());
                }
            });

            return topologyTask;
        }

        /// <summary>
        /// Populates the lookup table mapping morph nodes to shapes.  Allows user option to simplify shapes.  Ensures all shapes have matching corresponding verticies if they participate in two or more slices
        /// </summary>
        /// <param name="tolerance"></param>
        public Dictionary<ulong, SliceTopology> InitializeSliceTopology(double tolerance = 0)
        {
            var MorphNodeToShape = Graph.MorphNodeToShape;
             
            List<Slice> SlicesToStart = new List<Slice>(UnprocessedSlices.Count);
            bool TasksStarted = false;
            try
            {
                rwLock.EnterWriteLock();

                ulong[] UnprocessedSlicesArray = UnprocessedSlices.ToArray();
                
                for (int iSlice = UnprocessedSlices.Count - 1; iSlice >= 0; iSlice--)
                {
                    var outputTask = TryStartSlice(UnprocessedSlicesArray[iSlice]);
                    TasksStarted = TasksStarted || outputTask != null;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
            
            //We need to ensure there are tasks to wait on. This was an edge case for structures with one annotation.
            if(TasksStarted)
               AllDoneEvent.Wait();

            return this.SliceToTopology;
        }
    }

}
