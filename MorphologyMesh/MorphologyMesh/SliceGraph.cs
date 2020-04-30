﻿using System;
using GraphLib;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using Geometry;
using SqlGeometryUtils;
using AnnotationVizLib;
using MorphologyMesh;
using System.Diagnostics;


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
    /// </summary>
    public class SliceGraph : Graph<ulong, Slice, Edge<ulong>>
    {
        readonly MorphologyGraph Graph;

        /// <summary>
        /// Caches the shape of each morphology node in the slice graph.  After corresponding verticies are added this cache is used to ensures each section will get the same input shapes
        /// The map can also be used to support simplifying shapes.
        /// </summary>
        internal Dictionary<ulong, GridPolygon> MorphNodeToShape = null;

        private Dictionary<ulong, SliceTopology> SliceToTopology = null;

        private SliceGraph(MorphologyGraph graph)
        {
            this.Graph = graph;
        }

        internal static SliceGraph Create(MorphologyGraph graph, double tolerance=0)
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

                MorphologyNode Source = graph[e.SourceNodeKey];
                MorphologyNode Target = graph[e.TargetNodeKey];

                ZDirection SearchDirection = Source.Z < Target.Z ? ZDirection.Increasing : ZDirection.Decreasing;

                BuildMeshingCrossSection(graph, Source, SearchDirection, out MeshGroupNodesAbove, out MeshGroupNodesBelow, out MeshGroupEdges);

                Debug.Assert(MeshGroupNodesAbove.Count > 0, "Search should have found at least one node above and below.");
                Debug.Assert(MeshGroupNodesBelow.Count > 0, "Search should have found at least one node above and below.");
                Debug.Assert(MeshGroupEdges.Contains(e), "The edge we used to start the search is not in the search results.");

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
                }
            }

            output.MorphNodeToShape = InitializeShapes(graph, tolerance);
            output.InitializeSliceTopology(tolerance);

            /*output.SliceToTopology = new Dictionary<ulong, SliceTopology>(output.Nodes.Count);
            foreach(Slice s in output.Nodes.Values)
            {
                output.SliceToTopology[s.Key] = output.GetTopology(s);
            }
            */

            return output;
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

            BuildMeshingCrossSection(graph, ref NodesAbove, ref NodesBelow, NewNodesAbove, NewNodesBelow, ref FollowedEdges);
        }

        private static void BuildMeshingCrossSection(MorphologyGraph graph, ref SortedSet<ulong> NodesAbove, ref SortedSet<ulong> NodesBelow, SortedSet<ulong> NewNodesAbove, SortedSet<ulong> NewNodesBelow, ref SortedSet<MorphologyEdge> FollowedEdges)
        {
            NodesAbove.UnionWith(NewNodesAbove);
            NodesBelow.UnionWith(NewNodesBelow);

            FollowedEdges.UnionWith(NewNodesAbove.SelectMany(n => graph[n].GetEdgesBelow(graph).Select(other => new MorphologyEdge(graph, other, n))));
            FollowedEdges.UnionWith(NewNodesBelow.SelectMany(n => graph[n].GetEdgesAbove(graph).Select(other => new MorphologyEdge(graph, other, n))));

            NewNodesBelow = new SortedSet<ulong>(NewNodesAbove.SelectMany(n => graph[n].GetEdgesBelow(graph)));
            NewNodesAbove = new SortedSet<ulong>(NewNodesBelow.SelectMany(n => graph[n].GetEdgesAbove(graph)));

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


        /// <summary>
        /// Populates the lookup table mapping morph nodes to shapes.  Allows user option to simplify shapes.  Ensures all shapes have matching corresponding verticies if they participate in two or more slices
        /// </summary>
        /// <param name="tolerance"></param>
        private void InitializeSliceTopology(double tolerance = 0)
        {
            if(this.MorphNodeToShape == null)
                this.MorphNodeToShape = SliceGraph.InitializeShapes(this.Graph, tolerance);

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
        public static Dictionary<ulong, GridPolygon> InitializeShapes(MorphologyGraph graph, double tolerance = 0)
        {
            var result = new Dictionary<ulong, GridPolygon>(graph.Nodes.Count);

            List<Task<GridPolygon>> tasks = new List<Task<GridPolygon>>(graph.Nodes.Count);
            foreach (var node in graph.Nodes.Values)
            {
                if (node.Geometry == null)
                    continue;

                SupportedGeometryType nodeType = node.Geometry.GeometryType();
                if (nodeType != SupportedGeometryType.CURVEPOLYGON && nodeType != SupportedGeometryType.POLYGON)
                {
                    continue;
                }

                //Start a task to simplify the polygon
                Task<GridPolygon> t = new Task<GridPolygon>((node_) => ((MorphologyNode)node_).Geometry.ToPolygon().Simplify(tolerance), node);

                t.Start();
                tasks.Add(t);
            }

            foreach (var task in tasks)
            {
                try
                {
                    task.Wait();
                }
                catch (AggregateException e)
                {
                    //Oh well, we'll not simplify this one
                    continue;
                }

                if (task.IsCompleted)
                {
                    result[(ulong)((MorphologyNode)(task.AsyncState)).ID] = task.Result;
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

        private SliceTopology GetSliceTopology(ulong sliceKey, Dictionary<ulong, GridPolygon> polyLookup = null)
        {
            return GetSliceTopology(this[sliceKey], polyLookup);
        }

        internal SliceTopology GetSliceTopology(Slice group)
        {
            return this.GetSliceTopology(group, this.MorphNodeToShape);
        }

        internal SliceTopology GetSliceTopology(Slice group, Dictionary<ulong, GridPolygon> polyLookup = null)
        {
            var PolygonList = new List<GridPolygon>();
            var IsUpper = new List<bool>();
            var PolyZ = new List<double>();
            var PolyIndexToMorphNodeIndex = new List<ulong>();

            if (polyLookup != null)
            {
                PolygonList.AddRange(group.NodesAbove.Select(id => polyLookup.ContainsKey(id) ? polyLookup[id] : Graph[id].Geometry.ToPolygon()));
                PolygonList.AddRange(group.NodesBelow.Select(id => polyLookup.ContainsKey(id) ? polyLookup[id] : Graph[id].Geometry.ToPolygon()));
            }
            else
            {
                PolygonList.AddRange(group.NodesAbove.Select(id => Graph[id].Geometry.ToPolygon()));
                PolygonList.AddRange(group.NodesBelow.Select(id => Graph[id].Geometry.ToPolygon()));
            }

            GridPolygon[] Polygons = PolygonList.ToArray();

            PolyIndexToMorphNodeIndex.AddRange(group.NodesAbove);
            PolyIndexToMorphNodeIndex.AddRange(group.NodesBelow);

            IsUpper.AddRange(group.NodesAbove.Select(id => true));
            IsUpper.AddRange(group.NodesBelow.Select(id => false));

            PolyZ.AddRange(group.NodesAbove.Select(id => Graph[id].Z));
            PolyZ.AddRange(group.NodesBelow.Select(id => Graph[id].Z));

            //Todo Monday, this should not be in the constructor or the class I think.
            //Add corresponding points until we've run out of new correspondances
            var correspondingPoints = Polygons.AddCorrespondingVerticies();
            /*
            List<GridVector2> novelCorrespondingPoints = correspondingPoints.ToList();
            do
            {
                var nudgedPoints = SliceTopology.NudgeCorrespondingVerticies(Polygons, novelCorrespondingPoints);
                */
            SliceTopology.AddPointsBetweenAdjacentCorrespondingVerticies(Polygons, correspondingPoints);
            /*
                var NewCorresponingPoints = Polygons.AddCorrespondingVerticies();
//                novelCorrespondingPoints = NewCorresponingPoints.Where(p => correspondingPoints.Contains(p) == false).ToList();
                //correspondingPoints = NewCorresponingPoints;
            }
            while (novelCorrespondingPoints.Count > 0);
            */
            
            SliceTopology output = new SliceTopology(group.Key, Polygons, IsUpper, PolyZ, PolyIndexToMorphNodeIndex);

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

        public SliceTopology(ulong key, IEnumerable<GridPolygon> polygons, IEnumerable<bool> isUpper, IEnumerable<double> polyZ, IEnumerable<ulong> polyIndexToMorphNodeIndex)
            : this(polygons, isUpper, polyZ, polyIndexToMorphNodeIndex)
        {
            SliceKey = key;
        }

        public SliceTopology(IEnumerable<GridPolygon> polygons, IEnumerable<bool> isUpper, IEnumerable<double> polyZ, IEnumerable<ulong> polyIndexToMorphNodeIndex=null)
        {
            SliceKey = 0;

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
            Dictionary<GridVector2, List<PointIndex>> pointToIndexList = new Dictionary<GridVector2, List<PointIndex>>();
            //GridPolygon[] Polygons = this.Polygons;

            for (int iPoly = 0; iPoly < Polygons.Length; iPoly++)// GridPolygon poly in Polygons)
            {
                GridPolygon poly = Polygons[iPoly];
                List<PointIndex> correspondingIndicies = poly.TryGetIndicies(correspondingPoints);
                if (correspondingIndicies == null || correspondingIndicies.Count == 0)
                    continue;

                for (int i = 0; i < correspondingIndicies.Count; i++)
                {
                    PointIndex pi = correspondingIndicies[i];
                    GridVector2 cp = pi.Point(poly);

                    if (pointToIndexList.ContainsKey(cp))
                    {
                        pointToIndexList[cp].Add(pi.Reindex(iPoly));
                    }
                    else
                    {
                        List<PointIndex> listPI = new List<PointIndex>();
                        listPI.Add(pi.Reindex(iPoly));
                        pointToIndexList.Add(cp, listPI);
                    }
                }
            }

            List<GridVector2> UpdatedPoints = new List<GridVector2>();

            foreach (GridVector2 cp in pointToIndexList.Keys)
            {
                List<PointIndex> correspondingIndicies = pointToIndexList[cp];

                GridVector2[] points = correspondingIndicies.Select(ci => ci.PredictPoint(Polygons)).ToArray();
                GridVector2 avg = points.Average();
                  
                try
                {
                    foreach (PointIndex pi in correspondingIndicies)
                    {
                        pi.SetPoint(Polygons, avg);
                    }

                    UpdatedPoints.Add(avg);
                }
                catch(ArgumentException e)
                {
                    foreach (PointIndex pi in correspondingIndicies)
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
        /// Due to details of the implementation of our bajaj algorithm we need to add a point between adjacent corresponding points on a polygon
        /// </summary>
        internal static void AddPointsBetweenAdjacentCorrespondingVerticies(GridPolygon[] Polygons, List<GridVector2> correspondingPoints)
        {
            foreach(GridPolygon poly in Polygons)
            { 
                List<PointIndex> correspondingIndicies = poly.TryGetIndicies(correspondingPoints);
                if (correspondingIndicies == null || correspondingIndicies.Count == 0)
                    continue;

                SortedList<PointIndex, GridVector2> PointsToInsert = new SortedList<PointIndex, GridVector2>();
                correspondingIndicies.Sort(); //Sort the indicies so we can simplify our search.

                IIndexSet loopingIndex = new InfiniteSequentialIndexSet(0, correspondingIndicies.Count, 0);
                PointIndex Current = correspondingIndicies[0];
                for (long i = 0; i < correspondingIndicies.Count; i++)
                {
                    int iNext = (int)loopingIndex[i + 1];
                    PointIndex Next = correspondingIndicies[iNext];

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
                    poly.InsertVertex(addition.Value, addition.Key);
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

            try
            {
                rwLock.EnterWriteLock();

                ulong[] UnprocessedSlicesArray = UnprocessedSlices.ToArray();
                
                for (int iSlice = UnprocessedSlices.Count - 1; iSlice >= 0; iSlice--)
                {
                    TryStartSlice(UnprocessedSlicesArray[iSlice]);
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }

            AllDoneEvent.Wait();

            return this.SliceToTopology;
        }
    }

}
