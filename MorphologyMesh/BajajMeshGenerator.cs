using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnnotationVizLib;
using Geometry;
using Geometry.Meshing;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using TriangleNet;
using TriangleNet.Meshing;
using TriangleNet.Geometry;
using System.Collections;
using System.Diagnostics;
using RTree;

namespace MorphologyMesh
{

    public enum CONTOUR_RELATION
    {
        Disjoint,
        Enclosure, 
        Intersects
    }

    [Flags]
    public enum SliceChordTestType
    {
        Correspondance = 1,     //Allow the chord if the endpoints share an X,Y position
        ChordIntersection = 2,  //Allow the chord if it does not intersect an existing chord
        Theorem2 = 4,           //Allow the chord if the endpoints are on the correct side of the contours
        Theorem4 = 8,           //Allow the chord if the chord is only entirely inside or outside the polygons but not both
        LineOrientation = 16    //Allow the chord if the contours are not more than 90 degrees different in orientation
    }

    public static class BajajMeshGenerator
    {
        /// <summary>
        /// Convert a morphology graph to an unprocessed mesh graph
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        public static MeshGraph ConvertToMeshGraph(MorphologyGraph graph)
        {
            MeshGraph meshGraph = new MeshGraph();

            Dictionary<ulong, IShape2D> IDToContour = FindCorrespondences(graph);

            meshGraph.SectionThickness = graph.SectionThickness;

            //Create a graph where each node is a set of verticies.
            ConcurrentBag<MeshNode> nodes = new ConcurrentBag<MeshNode>();

#if !DEBUG
            graph.Nodes.Values.AsParallel().ForAll(node =>
            {
                MeshNode newNode = SmoothMeshGraphGenerator.CreateNode(node.Key, IDToContour[node.Key], node.Z, false);
                newNode.MeshGraph = meshGraph;
                newNode.Contour = node.Geometry.ToShape2D();
                nodes.Add(newNode);
            });
#else 
            foreach (var node in graph.Nodes.Values)
            {
                MeshNode newNode = SmoothMeshGraphGenerator.CreateNode(node.Key, IDToContour[node.Key], node.Z, false);
                newNode.MeshGraph = meshGraph;
                nodes.Add(newNode);
            }
#endif

            MeshNode meshNode = null;
            while (nodes.TryTake(out meshNode))
            {
                meshGraph.AddNode(meshNode);
            }

            foreach (MorphologyEdge edge in graph.Edges.Values)
            {
                //Find correspondences between contours

                MeshEdge mEdge = SmoothMeshGraphGenerator.CreateEdge(graph.Nodes[edge.SourceNodeKey], graph.Nodes[edge.TargetNodeKey]);
                meshGraph.AddEdge(mEdge);
            }

            foreach (MeshNode node in meshGraph.Nodes.Values.Where(n => n.GetEdgesAbove().Length > 0).ToArray())
            {
                CreatePortsForBranch(node, node.GetEdgesAbove().SelectMany(e => node.Edges[e]).ToArray());
            }

            foreach (MeshNode node in meshGraph.Nodes.Values.Where(n => n.GetEdgesBelow().Length > 0).ToArray())
            {
                CreatePortsForBranch(node, node.GetEdgesBelow().SelectMany(e => node.Edges[e]).ToArray());
            }

            /*
            //Create multiple ports for branches
            foreach (MeshNode node in meshGraph.Nodes.Values.Where(n => n.GetEdgesAbove().Length > 1).ToArray())
            {
                CreatePortsForBranch(node, node.GetEdgesAbove().SelectMany(e => node.Edges[e]).ToArray());
            }

            foreach (MeshNode node in meshGraph.Nodes.Values.Where(n => n.GetEdgesBelow().Length > 1).ToArray())
            { 
                CreatePortsForBranch(node, node.GetEdgesBelow().SelectMany(e => node.Edges[e]).ToArray());
            }
             */

            return meshGraph;
        }
         

        private static Dictionary<ulong, IShape2D> FindCorrespondences(MorphologyGraph graph)
        {  
            Dictionary<ulong, IShape2D> IDToShape = graph.Nodes.AsParallel().ToDictionary(n => n.Key,  n => n.Value.Geometry.ToShape2D());
              
            foreach(MorphologyEdge e in graph.Edges.Values)
            {
                IShape2D sourceShape = IDToShape[e.SourceNodeKey];
                IShape2D targetShape = IDToShape[e.TargetNodeKey];

                if(sourceShape.ShapeType == ShapeType2D.POLYGON && targetShape.ShapeType == ShapeType2D.POLYGON)
                {
                    GridPolygon sourcePoly = sourceShape as GridPolygon;
                    GridPolygon targetPoly = targetShape as GridPolygon;

                    sourcePoly.AddPointsAtIntersections(targetPoly);
                    targetPoly.AddPointsAtIntersections(sourcePoly);

                    IDToShape[e.SourceNodeKey] = sourcePoly;
                    IDToShape[e.TargetNodeKey] = targetPoly;
                }
            }

            return IDToShape;
        }
        
        private static void AddIndexSetToMeshIndexMap(Dictionary<GridVector3, long> map, Geometry.Meshing.DynamicRenderMesh<ulong> mesh, Geometry.Meshing.IIndexSet set)
        {
            Geometry.Meshing.IVertex[] verts = mesh.GetVerts(set).ToArray();
            long[] mesh_indicies = set.ToArray();

            for (int iVert = 0; iVert < mesh_indicies.Length; iVert++)
            {
                map.Add(verts[iVert].Position, mesh_indicies[iVert]);
            }
        }

        /// <summary>
        /// Build a map so we can navigate from a vertex back to a mesh index from a port
        /// </summary>
        /// <param name="mesh">The mesh all ports in Nodes should index into</param>
        /// <param name="Nodes">All nodes containing cap ports that index into the mesh</param>
        /// <returns></returns>
        private static Dictionary<GridVector3, long> CreateVertexToMeshIndexMap(Geometry.Meshing.DynamicRenderMesh<ulong> mesh, IEnumerable<ConnectionVerticies> ports)
        {
            Dictionary<GridVector3, long> map = new Dictionary<GridVector3, long>();

            foreach (ConnectionVerticies port in ports)
            {
                AddIndexSetToMeshIndexMap(map, mesh, port.ExternalBorder);

                foreach (var innerBorder in port.InternalBorders)
                {
                    AddIndexSetToMeshIndexMap(map, mesh, innerBorder);
                }
            }

            return map;
        }

        public static bool Theorem1()
        {
            throw new NotImplementedException();
        }
        
        /// <summary>
        /// Theorem2 requires that the orientation of the contours connected by the slice chord match. 
        /// </summary>
        /// <param name="polygons">Contours on projection slice</param>
        /// <param name="NearestContour">Nearest vertex on projection slice</param>
        /// <param name="p">Point projected</param>
        /// <returns></returns>
        public static bool Theorem2(IReadOnlyList<GridPolygon> Polygons, PointIndex vertex, PointIndex NearestContour)
        {
            //return EdgeTypeExtensions.OrientationsAreMatched(vertex, NearestContour, Polygons);
            
            GridVector2 p1 = vertex.Point(Polygons);
            GridVector2 p2 = NearestContour.Point(Polygons);

            if (p1 == p2) //Overlapping vertex always goes in the OTV table
            {
                return true;
            }
            else
            {
                GridLineSegment SliceChord = new GridLineSegment(p1, p2);

                bool MatchingOrientations = vertex.IsInner == NearestContour.IsInner;
                bool IsCorrectSide;

                GridVector2[] adjacent1 = NearestContour.ConnectedVerticies(Polygons);
                GridVector2[] pqr = new GridVector2[] { adjacent1[0], p2, adjacent1[1] };

                GridVector2[] adjacent2 = vertex.ConnectedVerticies(Polygons);
                GridVector2[] mno = new GridVector2[] { adjacent2[0], p1, adjacent2[1] };

                IsCorrectSide = p1.IsLeftSide(pqr) != p2.IsLeftSide(mno);
                
                if(!MatchingOrientations)
                {
                    return !IsCorrectSide; 
                }

                return IsCorrectSide;
            }
            
        }

        public static bool Theorem4(IReadOnlyList<GridPolygon> slicePolygons, PointIndex NearestContour, GridVector2 p1)
        {
            GridVector2 p2 = NearestContour.Point(slicePolygons);

            GridLineSegment ContourLine = new GridLineSegment(p1, p2);

            foreach(GridPolygon poly in slicePolygons)
            {
                if (!Theorem4(poly, ContourLine))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Theorem 4 requries that a line segment does not occupy space both internal and external to the polygon.
        /// Lines that fall over a polygon segment are acceptable as long as the rest of the line qualifies.
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="line"></param>
        /// <returns></returns>
        public static bool Theorem4(IReadOnlyList<GridPolygon> polygons, GridLineSegment line)
        {  
            foreach (GridPolygon poly in polygons)
            {
                if (!Theorem4(poly, line))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Theorem 4 requries that a line segment does not occupy space both internal and external to the polygon.
        /// Lines that fall over a polygon segment are acceptable as long as the rest of the line qualifies.
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="line"></param>
        /// <returns></returns>
        public static bool Theorem4(GridPolygon poly, GridLineSegment line)
        {
            List<GridVector2> intersections;

            return !LineIntersectionExtensions.Intersects(line, poly, true, out intersections);
        }

        private static bool TestOptimalTilingForVertex(PointIndex vertex, GridPolygon[] Polygons, IReadOnlyList<GridPolygon> SameLevelPolys, IReadOnlyList<GridPolygon> AdjacentLevelPolys, 
                                                       PointIndex candidate, RTree<SliceChord> chordTree, SliceChordTestType TestsToRun)
        {
            GridVector2 p1 = vertex.Point(Polygons);
            GridVector2 p2 = candidate.Point(Polygons);
            if (p1 == p2)
                return true;

            GridLineSegment ChordLine = new GridLineSegment(p1, p2);

            if ((TestsToRun & SliceChordTestType.ChordIntersection) > 0)
            {
                

                List<SliceChord> existingChords = chordTree.Intersects(ChordLine.BoundingBox.ToRTreeRect(0));
                if (existingChords.Any(c => c.Line.Intersects(ChordLine, true)))
                    return false;
            }

            bool AngleOrientation = true;
            bool T2 = true;
            bool T2Opp = true;
            bool T4 = true;
            bool T4Opp = true;

            if((TestsToRun & SliceChordTestType.LineOrientation) > 0)
            {
                AngleOrientation = EdgeTypeExtensions.OrientationsAreMatched(vertex, candidate, Polygons);
                if (!AngleOrientation)
                    return false;
            }

            if ((TestsToRun & SliceChordTestType.Theorem2) > 0)
            {
                T2 = Theorem2(Polygons, vertex, candidate);
                if (!T2)
                    return false;
            }

            //bool T2 = true;

            if ((TestsToRun & SliceChordTestType.Theorem4) > 0)
            {
                T4Opp = Theorem4(AdjacentLevelPolys, ChordLine);
                if (!T4Opp)
                    return false;

                T4 = Theorem4(SameLevelPolys, ChordLine);
                if (!T4)
                    return false;
                
            }

            return AngleOrientation && T2 && T2Opp && T4 && T4Opp;
            //return Theorem2(OppositeContours, candidate, p) && Theorem4(OppositeContours, ContourLine) && Theorem4(Contours, ContourLine);
        }
        
        /// <summary>
        /// Locate the best slice chord partner for a given vertex
        /// </summary>
        /// <param name="vertex">Vertex we are testing</param>
        /// <param name="Polygons">Polygon array verticies refer to</param>
        /// <param name="SameLevelPolys">Polygons in the array at the same Z level as the vertex</param>
        /// <param name="AdjacentLevelPolys">Polygons in the array at a different Z level as the vertex</param>
        /// <param name="OppositeVertexTree">Lookup data structure for verticies on different Z levels</param>
        /// <param name="chordTree">Lookup data structure for existing slice chords</param>
        /// <returns></returns>
        private static PointIndex? FindOptimalTilingForVertex(PointIndex vertex, GridPolygon[] Polygons, IReadOnlyList<GridPolygon> SameLevelPolys, IReadOnlyList<GridPolygon> AdjacentLevelPolys,
                                                              QuadTree<PointIndex> OppositeVertexTree, RTree<SliceChord> chordTree, SliceChordTestType TestsToRun)
        {
            double distance;
            GridVector2 p = vertex.Point(Polygons);
            PointIndex NearestPoint = OppositeVertexTree.FindNearest(p, out distance);

            if(TestOptimalTilingForVertex(vertex, Polygons, SameLevelPolys, AdjacentLevelPolys, NearestPoint, chordTree, TestsToRun))
            {
                return NearestPoint;
            }

            //OK, the closest point is not a match.  Expand the search.
            int iNextTest = 1;
            int BatchSize = 1;
            int BatchMultiple = 10;
            SortedList<double, PointIndex> NearestList = null;

            while (true)
            {
                if (iNextTest >= OppositeVertexTree.Count)
                    return new PointIndex?(); 

                if((NearestList == null || iNextTest >= NearestList.Count))
                {
                    BatchSize *= BatchMultiple;
                    NearestList = OppositeVertexTree.FindNearestPoints(p, BatchSize);

                    if(NearestList.Count < BatchSize && iNextTest >= NearestList.Count)
                    {
                        return new PointIndex?();
                    }
                }

                if (iNextTest < NearestList.Count)
                {
                    PointIndex testPoint = NearestList.Values[iNextTest];

                    if (TestOptimalTilingForVertex(vertex, Polygons, SameLevelPolys, AdjacentLevelPolys, testPoint, chordTree, TestsToRun))
                        return testPoint;
                }

                iNextTest++;
            }            
        }

        private static SortedList<int, List<GridPolygon>> PolyByLevel(GridPolygon[] polys, double[] PolyZ)
        {
            SortedList<int, List<GridPolygon>> levels = new SortedList<int, List<GridPolygon>>();

            List<int> ZLevels = PolyZ.Distinct().Select(z => (int)z).ToList();

            foreach (int Z in ZLevels)
            {
                List<GridPolygon> level = polys.Where((p, i) => PolyZ[i] == Z).ToList();
                levels.Add(Z, level);
            }

            return levels;
        }

        public static void CreateOptimalTilingVertexTable(GridPolygon[] polygons, double[] PolyZ, SliceChordTestType TestsToRun, out ConcurrentDictionary<PointIndex, PointIndex> OTVTable)
        {
            RTree<SliceChord> chordTree = new RTree<SliceChord>();
            CreateOptimalTilingVertexTable(new PolySetVertexEnum(polygons), polygons, PolyZ, TestsToRun, out OTVTable, ref chordTree);
        }

        public static void CreateOptimalTilingVertexTable(IEnumerable<PointIndex> VerticiesToMap, IEnumerable<PointIndex> CandidateVerticies, GridPolygon[] polygons, double[] PolyZ, SliceChordTestType TestsToRun, out ConcurrentDictionary<PointIndex, PointIndex> OTVTable, ref RTree<SliceChord> chordTree)
        { 
            SortedList<int, QuadTree<PointIndex>> LevelTree = CreateQuadTreesForVerticies(CandidateVerticies, polygons, PolyZ);

            ////////////////////////////////////////////////////
            CreateOptimalTilingVertexTable(VerticiesToMap, polygons, PolyZ, LevelTree, TestsToRun, out OTVTable, ref chordTree);
        }

        /// <summary>
        /// Find the optimal tiling vertex for the passed verticies
        /// </summary>
        /// <param name="VerticiesToMap"></param>
        /// <param name="polygons"></param>
        /// <param name="PolyZ"></param>
        /// <param name="OTVTable"></param>
        public static void CreateOptimalTilingVertexTable(IEnumerable<PointIndex> VerticiesToMap, GridPolygon[] polygons, double[] PolyZ, SliceChordTestType TestsToRun, out ConcurrentDictionary<PointIndex, PointIndex> OTVTable, ref RTree<SliceChord> chordTree)
         {
            SortedList<int, QuadTree<PointIndex>> LevelTree = CreateQuadTreesForPolygons(polygons, PolyZ);

            ////////////////////////////////////////////////////
            CreateOptimalTilingVertexTable(VerticiesToMap, polygons, PolyZ, LevelTree, TestsToRun, out OTVTable, ref chordTree);
        }

        public static void CreateOptimalTilingVertexTable(IEnumerable<PointIndex> VerticiesToMap, GridPolygon[] polygons, double[] PolyZ, SortedList<int, QuadTree<PointIndex>> CandidateTreeByLevel, SliceChordTestType TestsToRun,
                                                          out ConcurrentDictionary<PointIndex, PointIndex> OTVTable, ref RTree<SliceChord> chordTree)
        {
            SortedList<int, List<GridPolygon>> levels = PolyByLevel(polygons, PolyZ);
            Debug.Assert(levels.Keys.Count == 2);
            
            OTVTable = new ConcurrentDictionary<Geometry.PointIndex, Geometry.PointIndex>();


            foreach (var polygroup in VerticiesToMap.GroupBy(v => v.iPoly))
            {
                int iPoly = polygroup.Key;
                GridPolygon poly = polygons[iPoly];
                int Z = (int)PolyZ[iPoly];
                int AdjacentZ = (int)PolyZ.Where(adjz => adjz != Z).First();

                QuadTree<PointIndex> tree = CandidateTreeByLevel[AdjacentZ];

                List<GridPolygon> SameLevelPolys = levels[Z];
                List<GridPolygon> AdjacentLevelPolys = levels[AdjacentZ];

                foreach (PointIndex i in polygroup)
                {
                    GridVector2 p1 = i.Point(poly);
                    PointIndex? NearestOnOtherLevel = FindOptimalTilingForVertex(i, polygons, SameLevelPolys, AdjacentLevelPolys, tree, chordTree, TestsToRun);
                    if (NearestOnOtherLevel.HasValue)
                    {
                        OTVTable.TryAdd(i, NearestOnOtherLevel.Value);
                    }
                }
            }
        }



        public static SortedList<int, QuadTree<PointIndex>> CreateQuadTreesForPolygons(IReadOnlyList<GridPolygon> polygons, double[] PolyZ)
        {
            SortedList<int, QuadTree<PointIndex>> LevelTree = new SortedList<int, QuadTree<PointIndex>>();

            //Build a quad tree of all points at a given level
            foreach (double Z in PolyZ.Distinct())
            {
                LevelTree.Add((int)Z, new QuadTree<PointIndex>(polygons.Where((p,i) => PolyZ[i] == Z).ToArray().BoundingBox()));
            }


            for (int iPoly = 0; iPoly < polygons.Count; iPoly++)
            {
                GridPolygon poly = polygons[iPoly];
                int Z = (int)PolyZ[iPoly];
                QuadTree<PointIndex> tree = LevelTree[Z];
                foreach (PointIndex i in new PolygonVertexEnum(poly, iPoly))
                {
                    GridVector2 p1 = i.Point(poly);
                    tree.Add(p1, i);
                }
            }

            return LevelTree;
        }

        public static SortedList<int, QuadTree<PointIndex>> CreateQuadTreesForVerticies(IEnumerable<PointIndex> Candidates, IReadOnlyList<GridPolygon> polygons, double[] PolyZ)
        {
            SortedList<int, QuadTree<PointIndex>> LevelTree = new SortedList<int, QuadTree<PointIndex>>();

            //Build a quad tree of all points at a given level
            foreach (double Z in PolyZ.Distinct())
            {
                LevelTree.Add((int)Z, new QuadTree<PointIndex>(polygons.Where((p, i) => PolyZ[i] == Z).ToArray().BoundingBox()));
            }

            foreach(var VertGroup in Candidates.GroupBy(p => PolyZ[p.iPoly]))
            {
                int Z = (int)VertGroup.Key;
                QuadTree<PointIndex> tree = LevelTree[Z];
                foreach(PointIndex i in VertGroup)
                {
                    GridVector2 p1 = i.Point(polygons);
                    tree.Add(p1, i);
                }
            }

            return LevelTree;
        }

        private static void CreatePortsForBranch(MeshNode node, MeshEdge[] edges)
        {
            MeshGraph graph = node.MeshGraph;

            //OK, Voronoi diagram the shapes.  Create new ports.
            MeshNode[] other_nodes;
            other_nodes = edges.Select(e => graph.Nodes[e.SourceNodeKey == node.Key ? e.TargetNodeKey : e.SourceNodeKey]).ToArray();

            //Build a set of all polygons in the nodes
            List<GridPolygon> Polygons = new List<GridPolygon>();
            Polygons.Add(node.CapPort.ToPolygon(node.Mesh));

            //Need a map of verticies to Polygon/Index number
            foreach (MeshNode branchNode in other_nodes)
            {
                Polygons.Add(branchNode.CapPort.ToPolygon(branchNode.Mesh));
            }

            //Build a single mesh with all components of the branch
            foreach (MeshNode other_node in other_nodes)
            {
                SmootherMeshGenerator.MergeMeshes(node, other_node);
            }

            List<MeshNode> AllNodes = new List<MorphologyMesh.MeshNode>();
            AllNodes.Add(node);
            AllNodes.AddRange(other_nodes);

            Dictionary<GridVector3, long> VertexToMeshIndex = CreateVertexToMeshIndexMap(node.Mesh, AllNodes.Select(n => n.CapPort));

            //Create a map so we can record where every vertex came from in the set of points
            //TODO: Overlapping verticies are not going to be handled...  
            Dictionary<GridVector2, List<PointIndex>> pointToPoly = GridPolygon.CreatePointToPolyMap(Polygons.ToArray());

            //Create a map from the index into our array of polygons to the actual node ID the polygon came from.
            ulong[] PolygonToNodeKey = new ulong[other_nodes.Length + 1];
            PolygonToNodeKey[0] = node.Key;

            for (int iNode = 0; iNode < other_nodes.Length; iNode++)
            {
                PolygonToNodeKey[iNode + 1] = other_nodes[iNode].Key;
            }

            //Triangulate the ports together
            //IMesh mesh = pointToPoly.Keys.Triangulate();
            IMesh mesh = Polygons.ToArray().Triangulate();

            //OK, here there be dragons.  What to do with the mesh...
            GridTriangle[] triangles = mesh.ToTriangles().ToArray();

            GridLineSegment[] lines = mesh.ToLines().ToArray();

            //First remove any triangles that have a line segment in empty space.  i.e. across an internal polygon or outside the external boundary
            /*
            for (int iTri = 0; iTri < triangles.Length; iTri++)
            {
                GridTriangle triangle = triangles[iTri];

                //OK, determine if the triangle should recieve a face in the final mesh
                if (!(pointToPoly.ContainsKey(triangle.p1) && pointToPoly.ContainsKey(triangle.p2) && pointToPoly.ContainsKey(triangle.p3)))
                    continue;

                PointIndex A = pointToPoly[triangle.p1].First();
                PointIndex B = pointToPoly[triangle.p2].First();
                PointIndex C = pointToPoly[triangle.p3].First();

                PointIndex[] P = new PointIndex[] { A, B, C };

                double[] Z = P.Select(p => AllNodes[p.iPoly].Z).ToArray();
                double AZ = AllNodes[A.iPoly].Z;
                double BZ = AllNodes[B.iPoly].Z;
                double CZ = AllNodes[C.iPoly].Z;

                //Determine the mesh index for the verticies
                long[] iMesh = triangle.Points.Select((v, i) => VertexToMeshIndex[v.ToGridVector3(Z[i])]).ToArray();
                long iMeshA = VertexToMeshIndex[triangle.p1.ToGridVector3(AZ)];
                long iMeshB = VertexToMeshIndex[triangle.p2.ToGridVector3(BZ)];
                long iMeshC = VertexToMeshIndex[triangle.p3.ToGridVector3(CZ)];

                //OK, march through the cases for our triangles

            }
            */
            //GridLineSegment[] lines = mesh.ToLines().ToArray();

            Dictionary<GridVector2, SortedSet<PointIndex>> pointToConnectedPolys = new Dictionary<GridVector2, SortedSet<PointIndex>>();

            /*
            GridVector2[] midpoints = lines.Select(l => l.PointAlongLine(0.5)).AsParallel().ToArray();

            //Figure out which verticies are included in the port
            //Verticies of a line between two shapes are included 
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                GridLineSegment l = lines[i];

                if (!pointToPoly.ContainsKey(l.A) || !pointToPoly.ContainsKey(l.B))
                    continue;

                PointIndex APolyIndex = pointToPoly[l.A].First();
                PointIndex BPolyIndex = pointToPoly[l.B].First();

                int APoly = APolyIndex.iPoly;
                int BPoly = APolyIndex.iPoly;

                //Line between the same polygon.  We can ignore but add it to the list for that vertex (Right thing to do?)
                if (APoly == BPoly)
                {
                    if (APolyIndex.IsInner ^ BPolyIndex.IsInner)
                    {
                        CreateOrAddToSet(pointToConnectedPolys, l.A, BPolyIndex);
                        CreateOrAddToSet(pointToConnectedPolys, l.B, APolyIndex);
                    }

                    CreateOrAddToSet(pointToConnectedPolys, l.A, APolyIndex);
                    CreateOrAddToSet(pointToConnectedPolys, l.B, BPolyIndex);
                    continue;
                }

                GridPolygon A = Polygons[APoly];
                GridPolygon B = Polygons[BPoly];

                GridVector2 midpoint = midpoints[i];//l.PointAlongLine(0.5);
                bool midInA = A.Contains(midpoint);
                bool midInB = B.Contains(midpoint);

                //Line midpoint should be in one poly but not both
                if (!(midInA ^ midInB))
                {
                    continue;
                }

                CreateOrAddToSet(pointToConnectedPolys, l.A, APolyIndex);
                CreateOrAddToSet(pointToConnectedPolys, l.A, BPolyIndex);
                CreateOrAddToSet(pointToConnectedPolys, l.B, APolyIndex);
                CreateOrAddToSet(pointToConnectedPolys, l.B, BPolyIndex);
            }
            */

            for (int iEdge = 0; iEdge < edges.Length; iEdge++)
            {
                MeshEdge edge = edges[iEdge];

                GeneratePortFromTriangulation(node, edge, pointToConnectedPolys, PolygonToNodeKey);
            }
        }

        //private static void BuildMeshForBranch(MeshNode node, )

        /// <summary>
        /// Replace the external port for the node connected to the edge using data provided by triangulating the exterior and interior polygons of shapes on both sides of the edge
        /// </summary>
        /// <param name="node"></param>
        /// <param name="edge"></param>
        /// <param name="pointToConnectedPolys">A map indicating which polygon verticies each vertex is connected to in the triangulation</param>
        /// <returns></returns>
        private static void Triangulation(MeshNode node, MeshEdge edge, Dictionary<GridVector2, SortedSet<PointIndex>> pointToConnectedPolys, ulong[] PolygonToNodeKey)
        {
            ConnectionVerticies port = edge.GetPortForNode(node.Key);
            ConnectionVerticies other_port = edge.GetOppositePortForNode(node.Key);

            bool[] VertexInPort = new bool[port.ExternalBorder.Count];
            for (int iVertex = 0; iVertex < port.ExternalBorder.Count; iVertex++)
            {
                GridVector2 v = node.Mesh.Verticies[(int)port.ExternalBorder[iVertex]].Position.XY();
                if (!pointToConnectedPolys.ContainsKey(v))
                {
                    VertexInPort[iVertex] = iVertex > 0 ? VertexInPort[iVertex - 1] : false;
                    continue;
                }

                SortedSet<PointIndex> Connected = pointToConnectedPolys[v];

                SortedSet<ulong> ConnectedKeys = new SortedSet<ulong>(Connected.Select(i => PolygonToNodeKey[i.iPoly]));

                VertexInPort[iVertex] = ConnectedKeys.Contains(edge.SourceNodeKey) && ConnectedKeys.Contains(edge.TargetNodeKey);
            }

            long[] PortIndices = new long[VertexInPort.Sum(b => b ? 1 : 0)];
            int iAdded = 0;
            for (int iVertex = 0; iVertex < port.ExternalBorder.Count; iVertex++)
            {
                if (!VertexInPort[iVertex])
                    continue;

                PortIndices[iAdded] = port.ExternalBorder[iVertex];
                iAdded += 1;
            }

            PortIndices = PortIndices.Distinct().ToArray();
            if (PortIndices.Length >= 3)
            {
                port.ExternalBorder = new Geometry.Meshing.IndexSet(PortIndices);
            }
        }


        /// <summary>
        /// Replace the external port for the node connected to the edge using data provided by triangulating the exterior and interior polygons of shapes on both sides of the edge
        /// </summary>
        /// <param name="node"></param>
        /// <param name="edge"></param>
        /// <param name="pointToConnectedPolys">A map indicating which polygon verticies each vertex is connected to in the triangulation</param>
        /// <returns></returns>
        private static void GeneratePortFromTriangulation(MeshNode node, MeshEdge edge, Dictionary<GridVector2, SortedSet<PointIndex>> pointToConnectedPolys, ulong[] PolygonToNodeKey)
        {
            ConnectionVerticies port = edge.GetPortForNode(node.Key);
            ConnectionVerticies other_port = edge.GetOppositePortForNode(node.Key);


            bool[] VertexInPort = new bool[port.ExternalBorder.Count];
            for (int iVertex = 0; iVertex < port.ExternalBorder.Count; iVertex++)
            {
                GridVector2 v = node.Mesh.Verticies[(int)port.ExternalBorder[iVertex]].Position.XY();
                if (!pointToConnectedPolys.ContainsKey(v))
                {
                    VertexInPort[iVertex] = iVertex > 0 ? VertexInPort[iVertex - 1] : false;
                    continue;
                }

                SortedSet<PointIndex> Connected = pointToConnectedPolys[v];

                SortedSet<ulong> ConnectedKeys = new SortedSet<ulong>(Connected.Select(i => PolygonToNodeKey[i.iPoly]));

                VertexInPort[iVertex] = ConnectedKeys.Contains(edge.SourceNodeKey) && ConnectedKeys.Contains(edge.TargetNodeKey);
            }

            long[] PortIndices = new long[VertexInPort.Sum(b => b ? 1 : 0)];
            int iAdded = 0;
            for (int iVertex = 0; iVertex < port.ExternalBorder.Count; iVertex++)
            {
                if (!VertexInPort[iVertex])
                    continue;

                PortIndices[iAdded] = port.ExternalBorder[iVertex];
                iAdded += 1;
            }

            PortIndices = PortIndices.Distinct().ToArray();
            if (PortIndices.Length >= 3)
            {
                port.ExternalBorder = new Geometry.Meshing.IndexSet(PortIndices);
            }
        }
        /*
        private static IIndexSet IdentifyOppositePortFromTriangulation(IIndexSet source, IMesh triangulation, DynamicRenderMesh<ulong> Mesh, Dictionary<GridVector2, List<PointIndex>> pointToPoly, GridPolygon[] Polygons)
        {
            GridLineSegment[] lines = triangulation.ToLines().ToArray();

            List<GridTriangle> tri = triangulation.ToTriangles();

            List<long> OppositePort = new List<long>();
            List<long> SourcePort = new List<long>();

            GridVector3[] portPositionsXYZ = source.Select(i => Mesh[i].Position).ToArray();
            GridVector2[] portPositions = portPositionsXYZ.Select(p => p.XY()).ToArray();

            SortedSet<GridVector2> sortedPortPositions = new SortedSet<GridVector2>(portPositions);

            GridLineSegment[] portLines = lines.Where(l => sortedPortPositions.Contains(l.A) || sortedPortPositions.Contains(l.B)).ToArray();

            //Find the first vertex in the mesh that connects ports
            int[] portMeshIndicies = triangulation.IndiciesForPointsXY(portPositions.Select(p => p.XY()));

            SortedSet<GridVector2> oppositePointsInPort = new SortedSet<GridVector2>();

            foreach (long index in source)
            {
                GridVector3 p = Mesh[index].Position;

                GridLineSegment[] attachedLines = portLines.Where(l => l.IsEndpoint(p.XY())).ToArray();
                //GridVector2[] attachedPoints = attachedLines.Select(l => l.OppositeEndpoint(p.XY())).ToArray();

                //oppositePointsInPort.UnionWith(attachedPoints);

                foreach (GridLineSegment line in attachedLines)
                {
                    GridVector2 attachedPoint = line.OppositeEndpoint(p.XY());

                    PointIndex APoly = pointToPoly[line.A].First();
                    PointIndex BPoly = pointToPoly[line.B].First();

                    if (!IsLineOnSurface(APoly, BPoly, Polygons, line.PointAlongLine(0.5)))
                        continue;

                    oppositePointsInPort.Add(attachedPoint);
                }


                                
            }
            
        }
        */

        private static GridTriangle[] RemoveTrianglesOutsidePolygons(GridTriangle[] triangles, GridLineSegment[] lines, GridPolygon[] polygons, Dictionary<GridVector2, PointIndex> pointToPoly)
        {
            GridVector2[] midpoints = lines.Select(l => l.PointAlongLine(0.5)).ToArray();

            bool[] KeepTriangle = triangles.Select(t => true).ToArray();

            Dictionary<GridLineSegment, SortedSet<int>> lineToTriangles = new Dictionary<GridLineSegment, SortedSet<int>>(); //List all triangles a line segment is part of
            for (int iTri = 0; iTri < triangles.Length; iTri++)
            {
                GridTriangle tri = triangles[iTri];
                foreach (GridLineSegment l in tri.Segments)
                {
                    if (!lineToTriangles.ContainsKey(l))
                    {
                        lineToTriangles.Add(l, new SortedSet<int>());
                    }

                    lineToTriangles[l].Add(iTri);
                }
            }
            bool[] KeepLine = lines.Select(l => true).ToArray();

            for (int iLine = 0; iLine < lines.Length; iLine++)
            {
                GridLineSegment l = lines[iLine];
                PointIndex APolyIndex = pointToPoly[l.A];
                PointIndex BPolyIndex = pointToPoly[l.B];

                //Same polygon, check if it is inside
                if (APolyIndex.iPoly == BPolyIndex.iPoly)
                {
                    GridPolygon poly = polygons[APolyIndex.iPoly];
                    //Check that the indicies are not adjacent.  If they are part of a border retain the line
                    if (PointIndex.IsBorderLine(APolyIndex, BPolyIndex, poly))
                        continue;

                    //Check if the line falls outside our polygon, in which case we don't want it;
                    KeepLine[iLine] = poly.Contains(midpoints[iLine]);
                }
                else
                {
                    GridPolygon[] connectedPolys = new GridPolygon[] { polygons[APolyIndex.iPoly], polygons[BPolyIndex.iPoly] };

                }
            }

            return triangles.Where((t, i) => KeepTriangle[i]).ToArray();
        }

        /// <summary>
        /// Add an integer to the dictionary for the key.  Creates the SortedSet if needed
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="key"></param>
        /// <param name="iPoly"></param>
        private static void CreateOrAddToSet(Dictionary<GridVector2, SortedSet<PointIndex>> dict, GridVector2 key, PointIndex iPoly)
        {
            if (!dict.ContainsKey(key))
            {
                dict[key] = new SortedSet<PointIndex>();
            }

            dict[key].Add(iPoly);
        }

        internal static Dictionary<GridVector2, int> CreatePointToPolyMap(IReadOnlyList<GridPolygon> Polygons)
        {
            Dictionary<GridVector2, int> pointToPoly = new Dictionary<GridVector2, int>();
            for (int iPoly = 0; iPoly < Polygons.Count; iPoly++)
            {
                GridPolygon poly = Polygons[iPoly];
                GridVector2[] polyPoints = poly.ExteriorRing;
                foreach (GridVector2 p in poly.ExteriorRing)
                {
                    if (!pointToPoly.ContainsKey(p))
                        pointToPoly.Add(p, iPoly);
                }
            }

            return pointToPoly;
        }

        public static bool IsLineOnSurface(PointIndex APoly, PointIndex BPoly, GridPolygon[] Polygons, GridVector2 midpoint)
        {
            GridPolygon A = Polygons[APoly.iPoly];
            GridPolygon B = Polygons[BPoly.iPoly];

            if (APoly.iPoly != BPoly.iPoly)
            {
                bool midInA = A.Contains(midpoint);
                bool midInB = B.Contains(midpoint);

                if (!(midInA ^ midInB)) //Midpoint in both or neither polygon. Line may be on exterior surface
                {
                    return false; //Exclude from port.  Line covers empty space
                }
                else //Midpoint in one or the other polygon, but not both
                {
                    if (APoly.IsInner ^ BPoly.IsInner) //One or the other is an interior polygon, but not both
                    {
                        if (A.InteriorPolygonContains(midpoint) ^ B.InteriorPolygonContains(midpoint))
                        {
                            //Include in port.
                            //Line runs from exterior ring to the near side of an overlapping interior hole
                            return true;
                        }
                        else //Find out if the midpoint is contained by the same polygon with the inner polygon
                        {
                            if ((midInA && APoly.IsInner) || (midInB && BPoly.IsInner))
                            {
                                return true;// lineViews[i].Color = Color.Gold;
                            }
                            else
                            {
                                return false; //Not sure if this is correct.  Never saw it in testing. //lineViews[i].Color = Color.Pink;
                            }
                        }
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            else if (APoly.iPoly == BPoly.iPoly)
            {
                bool midInA = A.Contains(midpoint);
                bool midInB = midInA;

                if (PointIndex.IsBorderLine(APoly, BPoly, Polygons[APoly.iPoly]))
                {
                    //Line is part of the border, either internal or external
                    return true;
                }

                if (!midInA)
                {
                    //Line does not pass through solid space
                    return false;
                }
                else
                {
                    //Two options, the line is outside other shapes or inside other shapes.
                    //If outside other shapes we want to keep this edge, otherwise it is discarded
                    bool LineIntersectsAnyOtherPoly = Polygons.Where((p, iP) => iP != APoly.iPoly).Any(p => p.Contains(midpoint));
                    if (APoly.IsInner ^ BPoly.IsInner)
                    {
                        return !LineIntersectsAnyOtherPoly;
                    }
                    else
                    {
                        return !LineIntersectsAnyOtherPoly;
                    }
                }
            }

            throw new ArgumentException("Unhandled case in IsLineOnSurface");
        }
    }
} 
