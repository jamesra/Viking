using AnnotationVizLib;
using Geometry;
using Geometry.Meshing;
using GraphLib;
using SqlGeometryUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;


namespace MorphologyMesh
{
    public class SliceChordRTree : RTree.RTree<MorphologyMesh.ISliceChord>
    {

    }

    /// <summary>
    /// Represents a quad tree for points in the above or below shape set for a mesh group
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct SliceTopologyQuadTrees<T>
    {
        public QuadTreeWithUniqueValues<T> Above;
        public QuadTreeWithUniqueValues<T> Below;

        public ImmutableArray<int> UpperPolyIndicies;
        public ImmutableArray<int> LowerPolyIndicies;

        public SliceTopologyQuadTrees(QuadTreeWithUniqueValues<T> aboveQuad, QuadTreeWithUniqueValues<T> belowQuad, IEnumerable<int> upperPolyIndicies, IEnumerable<int> lowerPolyIndicies)
        {
            Above = aboveQuad;
            Below = belowQuad;

            UpperPolyIndicies = [.. upperPolyIndicies];
            LowerPolyIndicies = [.. lowerPolyIndicies];
        }

        public SliceTopologyQuadTrees(QuadTreeWithUniqueValues<T> aboveQuad, QuadTreeWithUniqueValues<T> belowQuad, ImmutableArray<int> upperPolyIndicies, ImmutableArray<int> lowerPolyIndicies)
        {
            Above = aboveQuad;
            Below = belowQuad;

            UpperPolyIndicies = upperPolyIndicies;
            LowerPolyIndicies = lowerPolyIndicies;
        }

        /// <summary>
        /// Return the QuadTreeWithUniqueValues for the points on the opposite side of the polygon
        /// </summary>
        /// <param name="iPoly"></param>
        /// <returns></returns>
        public readonly QuadTreeWithUniqueValues<T> GetOppositeSide(int iPoly) => UpperPolyIndicies.Contains(iPoly) ? Below : Above;

    }


    public class OTVTable : System.Collections.Concurrent.ConcurrentDictionary<Geometry.IShapeIndex, Geometry.IShapeIndex> { }

    public enum CONTOUR_RELATION
    {
        Disjoint,
        Enclosure,
        Intersects
    }

    public enum ZDirection
    {
        Increasing,
        Decreasing
    }

    /// <summary>
    /// Flag enumeration indicating a set of tests.
    /// </summary>
    [Flags]
    public enum SliceChordTestType
    {
        /// <summary>
        /// No test flags set
        /// </summary>
        None = 0,
        /// <summary>
        /// Allow the chord if the endpoints share an X,Y position
        /// </summary>
        Correspondance = 1,
        /// <summary>
        /// Allow the chord if it does not intersect an existing chord
        /// </summary>
        ChordIntersection = 2,
        /// <summary>
        /// Allow the chord if the endpoints are on the correct side of the contours
        /// </summary>
        Theorem2 = 4,
        /// <summary>
        /// Allow if the chord is only entirely inside or outside the polygons but not both
        /// </summary>
        Theorem4 = 8,
        /// <summary>
        /// Allow the chord if the contours are not more than 90 degrees different in orientation
        /// </summary>
        LineOrientation = 16,
        /// <summary>
        /// Allow the chord if the edge is considered valid according to EdgeType criteria
        /// </summary>
        EdgeType = 32,
        /// <summary>
        /// Allow the chord if the chord will not intersect an existing face
        /// </summary>
        Face = 64
    }

    [Flags]
    public enum SliceChordPriority
    {
        Distance = 1, //Add chords shortest to longest
        Orientation = 2, //Add chords with the closest orientation of contours first
    }

    /// <summary>
    /// Stores the results for a single origin and all tested candidates
    /// </summary>
    public class SliceChordOriginTestResultsCache
    {
        readonly Dictionary<int, SliceChordTestType> KnownCandidateFailures = [];

        public SliceChordOriginTestResultsCache()
        {

        }

        public SliceChordTestType GetFailures(int Target, SliceChordTestType requested)
        {
            if (KnownCandidateFailures.TryGetValue(Target, out SliceChordTestType knownFailures))
            {
                return knownFailures & requested;
            }

            return SliceChordTestType.None;
        }

        public void RecordFailure(int Target, SliceChordTestType failures)
        {
            //Don't bother if nothing failed
            if (failures == SliceChordTestType.None)
            {
                return;
            }

            if (KnownCandidateFailures.TryGetValue(Target, out SliceChordTestType knownFailures))
                KnownCandidateFailures[Target] = failures | knownFailures;
            else
                KnownCandidateFailures.Add(Target, failures);
        }

        /// <summary>
        /// Removes a target vertex. 
        /// </summary>
        /// <param name="Origin"></param>
        public bool Remove(int Target) => KnownCandidateFailures.Remove(Target);

        /// <summary>
        /// Clear all results
        /// </summary>
        public void Clear() => KnownCandidateFailures.Clear();
    }

    public class SliceChordsTestResultsCache
    {
        /// <summary>
        /// Record failures.  First level is the origin, then the targets for that origin.
        /// </summary>
        private readonly Dictionary<int, SliceChordOriginTestResultsCache> Failures;

        public SliceChordsTestResultsCache()
        {
            Failures = [];
        }

        /// <summary>
        /// Given a set of tests, returns which tests are known to have failed
        /// </summary>
        /// <param name="candidate"></param>
        /// <param name="requested"></param>
        /// <returns></returns>
        public SliceChordOriginTestResultsCache GetFailuresForOrigin(int Origin)
        {
            if (Failures.TryGetValue(Origin, out SliceChordOriginTestResultsCache knownCandidates))
            {
                return knownCandidates;
            }
            else
            {
                SliceChordOriginTestResultsCache Obj = new();
                Failures.Add(Origin, Obj);
                return Obj;
            }
        }

        /// <summary>
        /// Given a set of tests, returns which tests are known to have failed
        /// </summary>
        /// <param name="candidate"></param>
        /// <param name="requested"></param>
        /// <returns></returns>
        public SliceChordTestType GetFailures(int Origin, int Target, SliceChordTestType requested) => Failures.TryGetValue(Origin, out var knownCandidates) == false ? SliceChordTestType.None : knownCandidates.GetFailures(Target, requested);

        /// <summary>
        /// Add the failure flag to the candidate slice chord between Origin and Target.  This can prevent retesting the same chord later.
        /// </summary>
        /// <param name="Origin"></param>
        /// <param name="Target"></param>
        /// <param name="failures"></param>
        public void RecordFailure(int Origin, int Target, SliceChordTestType failures)
        {
            //Don't bother if nothing failed
            if (failures == SliceChordTestType.None)
            {
                return;
            }


            if (false == Failures.TryGetValue(Origin, out SliceChordOriginTestResultsCache knownCandidates))
            {
                knownCandidates = new SliceChordOriginTestResultsCache();
                Failures.Add(Origin, knownCandidates);
            }

            knownCandidates.RecordFailure(Target, failures);
        }

        /// <summary>
        /// Removes a vertex.  This is done when we know the vertex is complete and no longer under consideration
        /// </summary>
        /// <param name="Origin"></param>
        public void Remove(int Origin) => Failures.Remove(Origin);

        /// <summary>
        /// Clear all results
        /// </summary>
        public void Clear() => Failures.Clear();
    }



    public static class BajajMeshGenerator
    {

        public delegate void OnMeshGeneratedEventHandler(BajajGeneratorMesh mesh, bool Success);

        /*
        /// <summary>
        /// Convert a morphology graph to an unprocessed mesh graph
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        public static List<BajajGeneratorMesh> ConvertToMesh(MorphologyGraph graph, OnMeshGeneratedEventHandler OnMeshGenerated = null)
        {
            Trace.WriteLine("Begin Slice graph construction");
            SliceGraph sliceGraph = SliceGraph.Create(graph, 2.0).Result;
            Trace.WriteLine("End Slice graph construction");

            return ConvertToMesh(sliceGraph, OnMeshGenerated);
        }
        */

        /// Convert a morphology graph to an unprocessed mesh graph
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        public static async Task<List<BajajGeneratorMesh>> ConvertToMesh(SliceGraph sliceGraph, OnMeshGeneratedEventHandler OnMeshGenerated = null)
        {
            //List<MeshingGroup> MeshingGroups = CalculateMeshingGroups(graph);
            List<BajajGeneratorMesh> listBajajMeshGenerators = [];

            List<Task<BajajGeneratorMesh>> meshGenTasks = [];

            //var SimplerPolygon = CreateSimplerPolygonLookup(graph, 2.0);

            foreach (Slice slice in sliceGraph.Nodes.Values)
            {
                //Trace.WriteLine(string.Format("Creating group {0}", group.ToString()));

                //var sliceTopology = sliceGraph.GetTopology(slice);

                meshGenTasks.Add(Task<BajajGeneratorMesh>.Factory.StartNew(() => new BajajGeneratorMesh(sliceGraph.GetTopology(slice), slice)));

                //                BajajGeneratorMesh mesh = new BajajGeneratorMesh(Polygons.Select(p => p.Simplify(1.0)).ToList(), PolyZ, IsUpper);
                //              listBajajMeshGenerators.Add(mesh);
            }

            var meshGenTaskArray = meshGenTasks.ToArray();
            while (meshGenTasks.Any())
            {
                try
                {
                    var finishedTask = Task.WhenAny(meshGenTasks);

                    try
                    {
                        var t = finishedTask.Result;
                        if (t.Status == TaskStatus.RanToCompletion)
                        {
                            listBajajMeshGenerators.Add(t.Result);
                        }
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine($"Exception generating mesh {finishedTask.Result.AsyncState}");
                    }
                    finally
                    {
                        meshGenTasks.Remove(finishedTask.Result);
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Exception generating mesh {e}");
                }
            }

            //listBajajMeshGenerators.AddRange(meshGenTasks.Select(t => t.Result));

            listBajajMeshGenerators.Sort(Comparer<BajajGeneratorMesh>.Create((a, b) => a.AverageZ.CompareTo(b.AverageZ)));  //Sorting the bajaj generators before launching tasks is optional but built the model in a predictable order for debug viewing
            List<Task> bajajTasks = [];

            BajajGeneratorMesh[] BajajGeneratorMeshArray = [.. listBajajMeshGenerators];
            //TODO: THis should be parallelizable
            for (int iMesh = 0; iMesh < BajajGeneratorMeshArray.Length; iMesh++)
            {
                //BajajGeneratorMesh mesh = listBajajMeshGenerators[iMesh];
                bajajTasks.Add(Task.Factory.StartNew((i) =>
                   {
                       try
                       {
                           GenerateFaces(BajajGeneratorMeshArray[(int)i]);
                           OnMeshGenerated?.Invoke(BajajGeneratorMeshArray[(int)i], true);
                       }
                       catch
                       {
                           OnMeshGenerated?.Invoke(BajajGeneratorMeshArray[(int)i], false);
                       }
                   }, iMesh));

                //try
                //{
                //GenerateFaces(mesh);
                /*}
                catch (Exception e)
                {
                    Trace.WriteLine(string.Format("Exception building mesh {0}:\n{1}", listBajajMeshGenerators[iMesh].ToString(), e));
                    continue;
                }*/
            }

            foreach (var t in bajajTasks)
            {
                await t;
            }

            //Task<BajajGeneratorMesh>.Factory.ContinueWhenAll(bajajTasks);

            /*
            int counter = 0;
            for(int iTask = 0; iTask < bajajTasks.Count; iTask++)
            {
                var t = bajajTasks[iTask];
                try
                {
                    t.Wait(500);
                    counter++;
                    Trace.WriteLine(string.Format("{0} completed", counter));
                }
                catch(Exception e)
                {
                    Trace.WriteLine(string.Format("Exception building mesh {0}:\n{1}", listBajajMeshGenerators[iTask].ToString(), e));
                    continue; 
                }
            }*/


            /*
            int counter = 0;
            for (int iTask = 0; iTask < bajajTasks.Count; iTask++)
            {
                var t = bajajTasks[iTask];
                try
                {
                    t.Wait(500);
                    counter++;
                    Trace.WriteLine(string.Format("{0} completed", counter));
                }
                catch (Exception e)
                {
                    Trace.WriteLine(string.Format("Exception building mesh {0}:\n{1}", listBajajMeshGenerators[iTask].ToString(), e));
                    continue;
                }
            }
            */


            //MeshGraph meshGraph = new MeshGraph();
            /*
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
*/
            return null;//listBajajMeshGenerators;
        }

        public static void GenerateFaces(BajajGeneratorMesh mesh)
        {
            //Trace.WriteLine(string.Format("Creating mesh {0}", mesh.ToString()));

            AddDelaunayEdges(mesh);
            var RegionPairingGraph = GenerateRegionGraph(mesh);

            //Remove the edges we know are bad
            mesh.RemoveInvalidEdges();

            //Ensure corresponding verticies have a face (Legacy, unused in test case last I checked)
            CompleteCorrespondingVertexFaces(mesh);

            SliceChordRTree rTree = mesh.CreateChordTree(mesh.ShapeZ);
            List<OTVTable> listOTVTables = RegionPairingGraph.MergeAndCloseRegionsPass(mesh, rTree);

            var IncompleteVerticies = IdentifyIncompleteVerticies(mesh);

            List<MorphMeshVertex> FirstPassIncompleteVerticies = FirstPassSliceChordGeneration(mesh, mesh.ShapeZ);

            BajajMeshGenerator.FirstPassFaceGeneration(mesh);

            try
            {
                //2nd pass region detection to locate missing faces
                MorphMeshRegionGraph SecondPassRegions = MorphRenderMesh.SecondPassRegionDetection(mesh, FirstPassIncompleteVerticies);
                SecondPassRegions.MergeAndCloseRegionsPass(mesh, rTree);
            }
            catch (Exception e)
            {
                Trace.WriteLine(string.Format("Exception building mesh {0}\n{1}", mesh.ToString(), e));
            }

            BajajMeshGenerator.FirstPassFaceGeneration(mesh);

            if (mesh.Slice != null)
            {

                if (mesh.Slice.HasSliceAbove == false)
                    mesh.CapMeshEnd(true);

                if (mesh.Slice.HasSliceBelow == false)
                    mesh.CapMeshEnd(false);

            }

            mesh.EnsureFacesHaveExternalNormals();
            //mesh.RecalculateNormals();
        }

        private static Dictionary<GridVector2, List<int>> CreatePointToIndexMap(BajajGeneratorMesh mesh)
        {
            Dictionary<GridVector2, List<int>> result = new(mesh.Verticies.Count);
            foreach (MorphMeshVertex v in mesh.Verticies)
            {
                GridVector2 p = v.Position.XY();
                if (result.ContainsKey(p))
                {
                    result[p].Add(v.Index);
                }
                else
                {
                    result.Add(p, [v.Index]);
                }
            }

            return result;
        }

        public static void AddDelaunayEdges(BajajGeneratorMesh mesh, TriangulationMesh<Vertex2D<List<int>>>.ProgressUpdate OnProgress = null)
        {
            Geometry.Meshing.TriangulationMesh<Vertex2D<List<int>>> triMesh = null;

            //Create a map of the verticies present at each point, we expect one vertex usually, but two verticies for corresponding verticies
            //Then use the keys of the dictionary to create a Vertex2D array that we'll triangulate.  Once the triangulation is done
            //feed the existing contour edges into that triangulation as constraints.  Then add the faces to the passed mesh and classify the 
            //edges created for those faces.

            Dictionary<GridVector2, List<int>> pointToIndexMap = CreatePointToIndexMap(mesh);

            Dictionary<int, int> MeshToTriMesh = new(mesh.Verticies.Count);
            Dictionary<int, List<int>> TriMeshToMesh = new(mesh.Verticies.Count);

            GridVector2[] points = [.. pointToIndexMap.Keys];

            //Adjust the points to the average values to avoid floating point precision errors
            GridVector2 avg = points.Average();
            GridVector2[] translated_points = [.. points.Select(p => p - avg)];

            var verts = points.Select((p, i) => new Vertex2D<List<int>>(translated_points[i], pointToIndexMap[p])).ToArray();
            triMesh = Geometry.GenericDelaunayMeshGenerator2D<Vertex2D<List<int>>>.TriangulateToMesh(verts, OnProgress);

            foreach (var v in verts)
            {
                List<int> listIndicies = v.Data;//pointToIndexMap[v.Position];
                foreach (int i in listIndicies)
                {
                    MeshToTriMesh[i] = v.Index; //Map the mesh vertex ID to the vertex ID in the triangulation.  This can be a many to one mapping for corresponding verticies.
                }

                TriMeshToMesh[v.Index] = listIndicies; //Map the triangulations vertex ID to the mesh verticies.  This is a one to many mapping for corresponding verticies
            }

            var ContourEdges = mesh.MorphEdges.Where(e => e.Type == EdgeType.CONTOUR);
            foreach (var edge in ContourEdges)
            {
                int A = MeshToTriMesh[edge.A];
                int B = MeshToTriMesh[edge.B];
                triMesh.AddConstrainedEdge(new Geometry.Meshing.ConstrainedEdge(A, B), OnProgress);
            }

            foreach (IFace f in triMesh.Faces)
            {
                List<int> A_List = TriMeshToMesh[f.iVerts[0]];
                List<int> B_List = TriMeshToMesh[f.iVerts[1]];
                List<int> C_List = TriMeshToMesh[f.iVerts[2]];

                if (A_List.Count == 1 && B_List.Count == 1 && C_List.Count == 1)
                {
                    MorphMeshFace mesh_face = new(A_List[0], B_List[0], C_List[0]);
                    mesh.AddFace(mesh_face);
                }
                else
                {
                    /* Adding edges instead of faces seems like a good idea, but it causes a lot of issues with extra, incorrect, and missing faces on corresponding verticies even though it solves some cases*/
                    /*
                    //Add the edges, but not the face
                    foreach(var combo in A_List.CombinationPairs(B_List))
                    {
                        if(mesh.Contains(combo.A, combo.B) == false)
                        {
                            MorphMeshEdge edge = new MorphMeshEdge(EdgeType.UNKNOWN, combo.A, combo.B);
                            mesh.AddEdge(edge);
                        }
                    }

                    foreach (var combo in B_List.CombinationPairs(C_List))
                    {
                        if (mesh.Contains(combo.A, combo.B) == false)
                        {
                            MorphMeshEdge edge = new MorphMeshEdge(EdgeType.UNKNOWN, combo.A, combo.B);
                            mesh.AddEdge(edge);
                        }
                    }

                    foreach (var combo in C_List.CombinationPairs(A_List))
                    {
                        if (mesh.Contains(combo.A, combo.B) == false)
                        {
                            MorphMeshEdge edge = new MorphMeshEdge(EdgeType.UNKNOWN, combo.A, combo.B);
                            mesh.AddEdge(edge);
                        }
                    }
                    */
                }
            }

            //For corresponding verticies, we'll create edges where
            //int[] triMeshCorrespondingVerts = TriMeshToMesh.Where(item => item.Value.Count > 1).Select(item => item.Key).ToArray();


            mesh.ClassifyMeshEdges();
            //BajajGeneratorMesh.AddTriangulationEdgesToMesh(triMesh, mesh);
        }

        /*
        /// <summary>
        /// Add all edges from a delaunay triangulation to the mesh which are valid
        /// </summary>
        /// <param name="mesh"></param>
        public static void AddDelaunayEdges(BajajGeneratorMesh mesh)
        {
            IMesh triMesh = mesh.Polygons.Triangulate();

            BajajMeshGenerator.AddTriangulationEdgesToMesh(triMesh, mesh);

            mesh.ClassifyMeshEdges();
        }
        */

        public static MorphMeshRegionGraph GenerateRegionGraph(BajajGeneratorMesh mesh)
        {
            //Identify our trouble areas. 
            mesh.IdentifyRegionsViaFaces();

            //Identify probable mappings between regions
            MorphMeshRegionGraph RegionPairingGraph = GenerateRegionConnectionGraph(mesh);

            //Remove invalid edges
            //RemoveInvalidEdges(mesh);

            //Close the nodes with no edges
            //CloseRegionsFirstPass(mesh, RegionPairingGraph.Nodes.Values.Where(v => v.Edges.Count == 0).Select(v => v.Key).ToList());
            /*
            List<MorphMeshRegion> regions = RegionPairingGraph.Nodes.Where(n => n.Value.Edges.Count == 0).Select(n => n.Key).ToList();
            foreach(MorphMeshRegion unconnectedRegion in regions)
            {
                RegionPairingGraph.RemoveNode(unconnectedRegion);
            }
            */

            return RegionPairingGraph;
        }

        /// <summary>
        /// Create edges in our mesh based on a triangulation.  These edges will be categorized later and some discarded.
        /// </summary>
        /// <param name="triMesh"></param>
        /// <param name="output"></param>
        /*
        public static void AddTriangulationEdgesToMesh(IMesh2D<IVertex2D> triMesh, MorphRenderMesh output)
        {
            var pointToPoly = GridPolygon.CreatePointToPolyMap(output.Shapes.Select(p => p as GridPolygon).ToArray());

            GridVector2[] vertArray = triMesh.Verticies.Select(v => new GridVector2(v.Position.X, v.Position.Y)).ToArray();
            Dictionary<int, int[]> TriIndexToMeshIndex = new Dictionary<int, int[]>();

            //SortedList<MorphMeshVertex, MorphMeshVertex> CorrespondingVerticies = new SortedList<MorphMeshVertex, MorphMeshVertex>();

            double[] PolyZ = output.ShapeZ;
            
            */

        /*Ensure all triangulation points are in the mesh*/

        /*
        for (int iVert = 0; iVert < vertArray.Length; iVert++)
        {
            GridVector2 vert = vertArray[iVert];
            List<PolygonIndex> listPointIndicies = pointToPoly[vert];

            double[] PointZs = listPointIndicies.Select(p => PolyZ[p.iPoly]).ToArray();

            PolygonIndex pIndex = listPointIndicies[0];
            GridVector3 vert3 = vert.ToGridVector3(PolyZ[pIndex.iPoly]);

            MorphMeshVertex meshVertex = output.GetOrAddVertex(pIndex, vert3);

            TriIndexToMeshIndex[iVert] = new int[] { meshVertex.Index };

            if (listPointIndicies.Count > 1)
            {
                //We have a CORRESPONDING pair on two sections
                //We need to add these later or they mess up our indexing for faces
                List<int> meshIndicies = new List<int>
                {
                    meshVertex.Index
                };
                for (int i = 1; i < listPointIndicies.Count; i++)
                {
                    PolygonIndex pOtherIndex = listPointIndicies[i];
                    if (pIndex.iPoly == pOtherIndex.iPoly)
                        continue;

                    GridVector3 otherVert3 = vert.ToGridVector3(PolyZ[pOtherIndex.iPoly]);
                    MorphMeshVertex correspondingVertex = output.GetOrAddVertex(pOtherIndex, otherVert3);
                    //CorrespondingVerticies[meshVertex] = correspondingVertex;
                    meshIndicies.Add(correspondingVertex.Index);
                }

                TriIndexToMeshIndex[iVert] = meshIndicies.ToArray();
            }
        }

        //Because we took verticies from mesh the indicies should line up
        foreach (TriangleNet.Topology.Triangle tri in triMesh.Triangles)
        {
            int[] tri_face = new int[] { tri.GetVertexID(0), tri.GetVertexID(1), tri.GetVertexID(2) };
            int[] face = tri_face.SelectMany(f => TriIndexToMeshIndex[f]).ToArray();

            //Here we need to check for a corresponding edge being involved.  If we don't we can get an edge that should not exist in the mesh that face generation can follow to produce an incorrect mesh
            //A corresponding edge will have two vertex entries in the table, so we check for four or more verticies in the face to go down this special path
            if (face.Length > 4)
            {
                continue;
                //throw new NotImplementedException("Unexpected number of faces for Delaunay Triangulation conversion to mesh.  Expected each face to have three edges.");
            }
            else if (face.Length == 4)
            {
                */
        /*
        This code does generate faces around a corresponding vertex.  However the bajaj code that executes later produces smoother faces around corresponding points so I
        do not generate faces for triangles that contain corresponding verticies.
        */
        /***************

        //We need to make sure the face isn't twisted
        List<int> sortedFace = new List<int>(4);
        int[] correspondingEdge = tri_face.Where(f => TriIndexToMeshIndex[f].Length > 1).SelectMany(f => TriIndexToMeshIndex[f]).ToArray();
        System.Diagnostics.Debug.Assert(correspondingEdge.Length == 2); //I only wrote this for the case of a single corresponding edge.  While possible in theory, the multiple case should not occur in practice

        EdgeKey correspondingEdgeKey = new EdgeKey(correspondingEdge[0], correspondingEdge[1]);

        //Once we add two faces to the edge we are done
        if (output[correspondingEdgeKey].Faces.Count == 2)
            continue;

        MorphMeshVertex[] CorrespondingVerts = new MorphMeshVertex[] { output.GetVertex(correspondingEdge[0]), output.GetVertex(correspondingEdge[1]) }.OrderBy(v => v.Position.Z).ToArray();
        MorphMeshVertex[] OtherVerts = face.Where(f => f != correspondingEdgeKey.A && f != correspondingEdgeKey.B).Select(f => output.GetVertex(f)).OrderBy(f => f.Position.Z).ToArray();

        int[] vertsA = new int[] { CorrespondingVerts[0].Index, CorrespondingVerts[1].Index, OtherVerts[0].Index };
        int[] vertsB = new int[] { OtherVerts[0].Index, CorrespondingVerts[1].Index, OtherVerts[1].Index };

        MorphMeshFace FaceA = new MorphMeshFace(vertsA);
        MorphMeshFace FaceB = new MorphMeshFace(vertsB);

        //output.SplitFace(quadFace);
        output.AddFace(FaceA);
        output.AddFace(FaceB);
        *******************/

        /*

    }
    else
    {
        GridVector2[] verts = tri_face.Select(f => vertArray[f]).ToArray();

        if (verts.AreClockwise())
        {
            output.AddFace(new MorphMeshFace(face[1], face[0], face[2]));
        }
        else
        {
            output.AddFace(new MorphMeshFace(face));
        }
    }
}

return;
}
*/

        /*
        /// <summary>
        /// This is a specialized criteria function that quickly checks for faces for corresponding verticies.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="checkedEdges"></param>
        /// <param name="current"></param>
        /// <param name="candidate"></param>
        /// <returns></returns>
        private static bool CorrespondingVertexCloseableFaceCriteriaFunction(Stack<int> path, SortedSet<IEdgeKey> checkedEdges, IVertex current, IEdgeKey candidate)
        {

        }
        */

        /// <summary>
        /// We need to handle the case where a single vertex is on the other side of the contour boundary and creates
        /// two corresponding vertices which are tightly grouped.
        /// 
        //       3
        ///     / \
        /// A--2-B-4--C
        ///   /     \
        ///  1       5
        ///  
        /// This should only be called after the mesh is created when we know there are no faces for edges
        /// </summary>
        public static void CompleteAdjacentCorrespondingVertexFaces(MorphRenderMesh mesh)
        {
            //Identify any verticies who have a corresponding vertex previous and after thier position
            /*
            var polyEnum = new PolySetVertexEnum(mesh.Shapes);
            foreach (PolygonIndex pIndex in polyEnum)
            {
                MorphMeshVertex vert = mesh[pIndex];

                if (vert.Corresponding.HasValue)
                    continue;
            }
            */
        }

        public static void CompleteCorrespondingVertexFaces(MorphRenderMesh mesh)
        {
            //Corresponding edges should have two faces if they are complete

            MorphMeshEdge[] edges = [.. mesh.MorphEdges.Where(e => e.Type == EdgeType.CORRESPONDING && e.Faces.Count < 2)];

            foreach (MorphMeshEdge edge in edges)
            {
                MorphMeshVertex vA = mesh[edge.A];
                MorphMeshVertex vB = mesh[edge.B];

                //MorphMeshVertex vUpper = mesh.IsUpperShape[vA.PolyIndex.Value.iPoly] ? vA : vB;
                //MorphMeshVertex vLower = vUpper == vA ? vB : vA;

                List<MorphMeshVertex> VertsToCheck = [vA, vB];

                //TODO: I probably don't need the where statement below because I know the vertex is not face complete because the attached corresponding edge is not complete
                //I also should probably collect all of the possible faces, then select the option with the smallest perimeter. 
                foreach (MorphMeshVertex v in VertsToCheck.Where(vT => !vT.IsFaceSurfaceComplete(mesh)))
                {
                    if (edge.Faces.Count == 2)
                        break;

                    List<int> Face = null;
                    Face = mesh.FindAnyCloseableFace(vA.Index, vB, edge, MaxPathLength: 4);

                    //Check for an existing pathway for a face, if it exists, use it to be consistent with the model
                    if (Face?.Count <= 4)
                    {
                        MorphMeshFace face = new(Face);
                        //mesh.AddFace(face);  //Split face will add the faces, so there is no need to add before we split

                        if (Face.Count == 4)
                        {
                            mesh.SplitFace(face);
                        }
                    }
                    else //Face is null, so there isn't an obvious mapping
                    {
                        //We cannot count on the order of the verticies returned in Face. 
                        //If we want to get correct CCW winding it takes extra work
                        int iVA = v.Index;
                        int iVB = v.Corresponding.Value;

                        //int iVLower = mesh.IsUpperShape[vA.PolyIndex.Value.iPoly] ? iVB : iVA;
                        //int iVUpper = iVLower == iVB ? iVA : iVB;

                        if (v.ShapeIndex is not PolygonIndex vPolyIndex)
                        {
                            Trace.WriteLine("Cannot close faces between polygons and polylines yet.");
                            break;
                        }

                        if (mesh[iVB].ShapeIndex is not PolygonIndex vCorrespondingIndex)
                        {
                            Trace.WriteLine("Cannot close faces between polygons and polylines yet.");
                            break;
                        }

                        if (mesh.Shapes[vCorrespondingIndex.iPoly] is not GridPolygon oppositePolygon)
                            throw new ArgumentException("PolygonIndex does not point to a polygon");

                        //Check all of the edge cases 
                        EdgeType NNType = mesh.GetContourEdgeTypeWithOrientation(vPolyIndex.Next, vCorrespondingIndex.Next);
                        EdgeType NPType = mesh.GetContourEdgeTypeWithOrientation(vPolyIndex.Next, vCorrespondingIndex.Previous);
                        EdgeType PPType = mesh.GetContourEdgeTypeWithOrientation(vPolyIndex.Previous, vCorrespondingIndex.Previous);
                        EdgeType PNType = mesh.GetContourEdgeTypeWithOrientation(vPolyIndex.Previous, vCorrespondingIndex.Next);

                        bool NNValid = NNType.IsValid() || NNType == EdgeType.FLIPPED_DIRECTION;
                        bool NPValid = NPType.IsValid() || NPType == EdgeType.FLIPPED_DIRECTION;
                        bool PPValid = PPType.IsValid() || PPType == EdgeType.FLIPPED_DIRECTION;
                        bool PNValid = PNType.IsValid() || PNType == EdgeType.FLIPPED_DIRECTION;

                        int nFacesFound = 0;

                        if (NNValid)
                        {
                            int[] TriFace = [mesh[vPolyIndex.Next].Index, iVA, iVB];

                            MorphMeshFace face = new(TriFace);
                            mesh.AddFace(face);
                            TriFace = [mesh[vCorrespondingIndex.Next].Index, mesh[vPolyIndex.Next].Index, iVB];
                            face = new MorphMeshFace(TriFace);

                            if (FaceContainsVerticies(mesh, face, out MorphMeshVertex[] contained_verts) == false)
                            {
                                mesh.AddFace(face);
                                nFacesFound++;
                            }
                        }

                        if (NPValid)
                        {
                            int[] TriFace = [mesh[vPolyIndex.Next].Index, iVA, iVB];
                            MorphMeshFace face = new(TriFace);
                            mesh.AddFace(face);
                            TriFace = [mesh[vCorrespondingIndex.Previous].Index, mesh[vPolyIndex.Next].Index, iVB];
                            face = new MorphMeshFace(TriFace);
                            if (FaceContainsVerticies(mesh, face, out MorphMeshVertex[] contained_verts) == false)
                            {
                                mesh.AddFace(face);
                                nFacesFound++;
                            }
                        }

                        if (PPValid)
                        {
                            int[] TriFace = [mesh[vPolyIndex.Previous].Index, iVA, iVB];
                            MorphMeshFace face = new(TriFace);
                            mesh.AddFace(face);
                            TriFace = [mesh[vCorrespondingIndex.Previous].Index, mesh[vPolyIndex.Previous].Index, iVB];
                            face = new MorphMeshFace(TriFace);
                            if (FaceContainsVerticies(mesh, face, out MorphMeshVertex[] contained_verts) == false)
                            {
                                mesh.AddFace(face);
                                nFacesFound++;
                            }
                        }

                        if (PNValid)
                        {
                            int[] TriFace = [mesh[vPolyIndex.Previous].Index, iVA, iVB];
                            MorphMeshFace face = new(TriFace);
                            mesh.AddFace(face);
                            TriFace = [mesh[vCorrespondingIndex.Next].Index, mesh[vPolyIndex.Previous].Index, iVB];
                            face = new MorphMeshFace(TriFace);
                            if (FaceContainsVerticies(mesh, face, out MorphMeshVertex[] contained_verts) == false)
                            {
                                mesh.AddFace(face);
                                nFacesFound++;
                            }
                        }

                        //Once in a while there are not two valid edges to complete the face.  
                        //TODO: This case would be better handled by triangulating verticies contained in the face.  It would solve some of the known failures in mesh generation.
                        if (nFacesFound == 1)
                        {
                            break; //This prevents overlapping faces from check the corresponding face
                        }


                        /*

                        bool NextContains = oppositePolygon.Contains(vPolyIndex.Next.Point(mesh.Polygons));
                        bool PrevContains = oppositePolygon.Contains(vPolyIndex.Previous.Point(mesh.Polygons));

                        bool FlipContainsTest = vCorrespondingIndex.IsInner; // false;// vPolyIndex.IsInner ^ vCorrespondingIndex.IsInner;

                        if(FlipContainsTest)
                        {
                            NextContains = !NextContains;
                            PrevContains = !PrevContains;
                        }

                        if (NextContains == false)
                        {
                            int iOther = mesh[vPolyIndex.Next].Index;
                            int[] TriFace = new int[] { iOther, iVA, iVB };
                            MorphMeshFace face = new MorphMeshFace(TriFace);
                            mesh.AddFace(face);
                        }

                        if(PrevContains == false)
                        {
                            int iOther = mesh[vPolyIndex.Previous].Index;
                            int[] TriFace = new int[] { iOther, iVA, iVB };
                            MorphMeshFace face = new MorphMeshFace(TriFace);
                            mesh.AddFace(face);
                        }

                        */

                        /*
                        Debug.Assert(Math.Abs(iVLower - iVUpper) == 1 || (Math.Abs(iVLower - iVUpper) == Face.Count - 1));

                        int iOther = iVLower - 1;
                        //bool CounterClockwise = true;
                        if (iOther < 0 || iOther == iVUpper)
                        {
                            iOther = iVLower + 1;
                            //CounterClockwise = false;
                            if (iOther >= Face.Count || iOther == iVUpper)
                            {
                                iOther = iVUpper - 1;
                              //  CounterClockwise = true;
                                if (iOther < 0 || iOther == iVLower)
                                {
                                    iOther = iVUpper + 1;
                                //    CounterClockwise = false;
                                    if (iOther < 0 || iOther == iVLower)
                                    {
                                        throw new ArgumentException("Can't find third vertex to create face for corresponding edge");
                                    }
                                }
                            }
                        }
                        */

                        //I used to try to get winding correct, the implementation wasn't correct.  Now I handle it at the end of mesh generation.
                        //int[] TriFace = CounterClockwise ? new int[] { iOther, iVLower, iVUpper } : new int[] { iOther, iVUpper, iVLower};

                        /*
                        int[] TriFace = new int[] { iOther, iVLower, iVUpper };
                        MorphMeshFace face = new MorphMeshFace(TriFace.Select(i => Face[i])); 
                        mesh.AddFace(face);
                        */
                    }
                }
            }
        }

        /// <summary>
        /// Returns any verticies that are inside the XY projection of a given face.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="face"></param>
        /// <param name="contained_verts"></param>
        /// <returns>True if the face contains verticies</returns>
        private static bool FaceContainsVerticies(MorphRenderMesh mesh, MorphMeshFace face, out MorphMeshVertex[] contained_verts)
        {
            GridTriangle tri;
            try
            {
                tri = new GridTriangle([.. face.iVerts.Select(i => mesh.Verticies[i].Position.XY())]);
            }
            catch (ArgumentException)
            {
                //A zero size triangle means it cannot contain verticies
                contained_verts = [];
                return false;
            }

            contained_verts = [.. mesh.Verticies.Where(v => face.iVerts.Contains(v.Index) == false && tri.Contains(v.Position.XY()))];
            return contained_verts.Length > 0;
        }

        public static MorphMeshRegionGraph GenerateRegionConnectionGraph(BajajGeneratorMesh mesh)
        {
            MorphMeshRegionGraph graph = new();

            ///----------- Create data structures ---------- 
            SortedDictionary<int, MorphMeshRegion> VertToRegion = [];
            SortedSet<int> AllRegionVerts = [];
            Dictionary<MorphMeshRegion, SortedSet<MorphMeshEdge>> RegionToEdges = [];

            foreach (MorphMeshRegion region in mesh.Regions)
            {
                foreach (int vert in region.Verticies)
                {
                    //TODO: How to handle a vertex shared by two regions?
                    if (!VertToRegion.ContainsKey(vert))
                        VertToRegion.Add(vert, region);
                }

                AllRegionVerts.UnionWith(region.Verticies);
                graph.AddNode(new Node<MorphMeshRegion, MorphMeshRegionGraphEdge>(region));
                RegionToEdges.Add(region, []);
            }

            //-------------------------------------------------
            //Find all edges that connect regions
            IEdgeKey[] EdgesConnectingRegions = [.. mesh.Edges.Keys.Where(e => AllRegionVerts.Contains(e.A) && AllRegionVerts.Contains(e.B))];

            //Create edges in the graph
            foreach (IEdgeKey edge in EdgesConnectingRegions)
            {
                var RegionA = VertToRegion[edge.A];
                var RegionB = VertToRegion[edge.B];

                if (RegionA == RegionB)
                    continue;

                if (!RegionA.Type.IsValidPair(RegionB.Type))
                    continue;

                if (RegionA.ZLevel.SetEquals(RegionB.ZLevel))
                    continue;

                MorphMeshRegionGraphEdge graphEdge = new(RegionA, RegionB);
                if (!graph.Edges.ContainsKey(graphEdge))
                {
                    graph.AddEdge(graphEdge);
                }

                MorphMeshEdge mme = mesh[edge];
                RegionToEdges[RegionA].Add(mme);
                RegionToEdges[RegionB].Add(mme);
            }

            //----------------------------------------------------

            //Add weights to the edges based on the average distance between the edges
            foreach (MorphMeshRegionGraphEdge edge in graph.Edges.Values)
            {
                var AllAEdges = RegionToEdges[edge.SourceNodeKey];
                var AllBEdges = RegionToEdges[edge.TargetNodeKey];

                SortedSet<MorphMeshEdge> EdgeSet = [.. AllAEdges];
                EdgeSet.IntersectWith(AllBEdges);

                //The weight is the mean length of all edges
                Debug.Assert(EdgeSet.Count > 0); //How are we an edge in the graph if there are no edges in the mesh?
                double avgLength = EdgeSet.Average(e => mesh.ToSegment(e.Key).Length);

                edge.Weight = avgLength;
            }

            return graph;
        }

        /// <summary>
        /// Identify verticies that do not have a complete set of faces between contour edges
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static List<MorphMeshVertex> IdentifyIncompleteVerticies(this MorphRenderMesh mesh)
        {
            return [.. mesh.Verticies.Where(v => v as MorphMeshVertex != null &&
                                        !((MorphMeshVertex)v).IsFaceSurfaceComplete(mesh))
                                        .Select(v => (MorphMeshVertex)v)];
        }

        #region SliceChordGeneration

        /// <summary>
        /// Try to add the slice chord unless it crosses an existing chord or forms an invalid EdgeType
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="sc"></param>
        /// <param name="ChordRTree"></param>
        /// <returns></returns>
        private static bool TryAddSliceChord(BajajGeneratorMesh mesh, SliceChord sc, SliceChordRTree ChordRTree, SliceChordTestType Tests)
        {
            if (BajajMeshGenerator.IsSliceChordValid(sc.Origin, mesh.Shapes, mesh.GetSameLevelShapes(sc), mesh.GetAdjacentLevelShapes(sc), sc.Target, ChordRTree, Tests, out SliceChordTestType failures))
            {

                MorphMeshEdge edge = new(EdgeTypeExtensions.GetEdgeType(sc.Line, mesh.Shapes[sc.Origin.iShape], mesh.Shapes[sc.Target.iShape]), mesh[sc.Origin].Index, mesh[sc.Target].Index);
                if (mesh.Contains(edge))
                    return false;

                mesh.AddEdge(edge);
                ChordRTree.Add(sc.Line.BoundingBox.ToRTreeRect(0), sc);

                return true;
            }
            else
            {
                mesh.SliceChordCandidateCache.RecordFailure(mesh[sc.Origin].Index, mesh[sc.Target].Index, failures);
            }

            return false;
        }

        /// <summary>
        /// Generate slice chords for the remaining unknown chords.  Returns a list of incomplete verticies.
        /// </summary>
        /// <param name="mesh">The mesh, which may contain edges we cannot cross</param>
        public static List<MorphMeshVertex> FirstPassSliceChordGeneration(BajajGeneratorMesh mesh, ICollection<double> ZLevels)
        {
            SliceChordRTree rTree = mesh.CreateChordTree(ZLevels);

            mesh.CloseFaces();
            List<MorphMeshVertex> IncompleteVerticies = [.. mesh.MorphVerticies.Where(v => false == v.IsFaceSurfaceComplete(mesh))];

            SliceChordTestType[] PassCriteria =
            [
                SliceChordTestType.Correspondance | SliceChordTestType.ChordIntersection | SliceChordTestType.Theorem2 | SliceChordTestType.EdgeType | SliceChordTestType.Theorem4 | SliceChordTestType.LineOrientation,
                SliceChordTestType.Correspondance | SliceChordTestType.ChordIntersection | SliceChordTestType.Theorem2 | SliceChordTestType.EdgeType | SliceChordTestType.Theorem4,
                SliceChordTestType.Correspondance | SliceChordTestType.ChordIntersection | SliceChordTestType.EdgeType | SliceChordTestType.Theorem4 | SliceChordTestType.LineOrientation,
                SliceChordTestType.Correspondance | SliceChordTestType.ChordIntersection | SliceChordTestType.EdgeType | SliceChordTestType.Theorem4,
                //SliceChordTestType.Correspondance | SliceChordTestType.Theorem2 | SliceChordTestType.LineOrientation
            ];

            //Precalulate the quad treeWithUniqueValues data structures
            var VertexQuadTrees = mesh.CreateQuadTreesForContours();

            //Run each set of increasingly loose criteria over the chords.
            foreach (SliceChordTestType passTestCriteria in PassCriteria)
            {
                while (SliceChordGenerationPass(mesh, rTree, IncompleteVerticies, passTestCriteria, VertexQuadTrees) == true)
                {
                    mesh.CloseFaces(IncompleteVerticies.Cast<Geometry.Meshing.IVertex>());
                    IncompleteVerticies = [.. IncompleteVerticies.Where(v => false == v.IsFaceSurfaceComplete(mesh))];
                }
            }

            mesh.SliceChordCandidateCache.Clear();
            /*
            while (SliceChordGenerationPass(mesh, rTree, IncompleteVerticies, FirstPassTests) == true)
            {
                //Try to remove any verticies we've completed the faces for from the search
                mesh.CloseFaces(IncompleteVerticies.Cast<Geometry.Meshing.IVertex>());
                IncompleteVerticies = IncompleteVerticies.Where(v => false == v.IsFaceSurfaceComplete(mesh)).ToList();
            }
            */
            /*
             while (SliceChordGenerationPass(mesh, rTree, IncompleteVerticies, SecondPassTests) == true)
            {
                //Try to remove any verticies we've completed the faces for from the search
                mesh.CloseFaces(IncompleteVerticies.Cast<Geometry.Meshing.IVertex>());
                IncompleteVerticies = IncompleteVerticies.Where(v => false == v.IsFaceSurfaceComplete(mesh)).ToList();
            }
            */
            /*
            
            while (SliceChordGenerationPass(mesh, rTree, IncompleteVerticies, ThirdPassTests) == true)
            {
                //Try to remove any verticies we've completed the faces for from the search
                mesh.CloseFaces(IncompleteVerticies.Cast<Geometry.Meshing.IVertex>());
                IncompleteVerticies = IncompleteVerticies.Where(v => false == v.IsFaceSurfaceComplete(mesh)).ToList();
            }
            */

            mesh.CloseFaces(IncompleteVerticies.Cast<Geometry.Meshing.IVertex>());
            IncompleteVerticies = [.. IncompleteVerticies.Where(v => false == v.IsFaceSurfaceComplete(mesh))];
            return IncompleteVerticies;
        }


        /// <summary>
        /// Generate slice chords for the remaining unknown chords, returns true if any chords were generated
        /// </summary>
        /// <param name="mesh">The mesh, which may contain edges we cannot cross</param>
        /// <param name="LevelTree">An optional parameter containing quadtrees for verticies on the upper and lower polygon sets.  It can be calculated once and passed as this parameter or left null and the function will build it.</param>
        private static bool SliceChordGenerationPass(BajajGeneratorMesh mesh, SliceChordRTree rTree, List<MorphMeshVertex> IncompleteVerticies, SliceChordTestType TestSuite, SliceTopologyQuadTrees<MorphMeshVertex>? LevelTree = null)
        {

            if (LevelTree.HasValue == false)
                LevelTree = mesh.CreateQuadTreesForContours();

            BajajMeshGenerator.CreateOptimalTilingVertexTable(mesh, IncompleteVerticies,
                                                              LevelTree.Value, TestSuite,
                                                              out ConcurrentDictionary<MorphMeshVertex, MorphMeshVertex> OTVTable, ref rTree);

            List<SliceChord> CandidateChords = CreateChordCandidateList(mesh, OTVTable);

            ///Starting with the shortest chord, add all of the slice chords that do not intersect an existing chord
            //SliceChordRTree AddedChords = rTree;//new RTree.RTree<SliceChord>();
            CandidateChords = [.. CandidateChords.OrderBy(sc => sc.Line.Length)];

            bool addedChord = false;
            int numAdded = 0;
            foreach (SliceChord sc in CandidateChords)
            {
                bool addedThisChord = TryAddSliceChord(mesh, sc, rTree, TestSuite);
                addedChord = addedChord || addedThisChord;
                if (addedThisChord)
                {
                    numAdded += 1;
                    //Console.WriteLine(string.Format("Added {0} Remaining: {1}", sc, CandidateChords.Count));
                }
            }

            //Console.WriteLine(string.Format("*** Added {0} Chords this pass ***", numAdded));

            return addedChord;
        }

        /// <summary>
        /// Using the existing slice chords determine if any faces can be added using existing edges
        /// </summary>
        public static void FirstPassFaceGeneration(MorphRenderMesh mesh, List<MorphMeshVertex> incompleteVerts = null)
        {
            //We know that all faces have a contour as part of the triangle
            incompleteVerts ??= [.. IdentifyIncompleteVerticies(mesh)];

            while (incompleteVerts.Count > 0)
            {
                MorphMeshVertex v = incompleteVerts[0];
                incompleteVerts.RemoveAt(0);

                List<int> face_path = mesh.IdentifyIncompleteFace(v, MaxFaceVerts: 4);
                if (face_path != null && face_path.Count <= 4)
                {
                    MorphMeshFace face = new(face_path);
                    if (face.IsTriangle)
                    {
                        mesh.AddFace(face);
                    }
                    else if (face.IsQuad)
                    {
                        var verts = mesh[face_path].ToArray();
                        double[] VertZLevels = [.. verts.Select(vert => vert.Position.Z).Distinct()];

                        //This was changed just before I quit for the night
                        //int NumVertZLevels = verts.Where(vert => vert.Position.Z == VertZLevels[0]).Count();
                        int NumVertZLevels = VertZLevels.Distinct().Count();
                        if (NumVertZLevels == 2)
                        {
                            mesh.AddFace(face);
                            mesh.SplitFace(face);
                        }
                        else if (NumVertZLevels == 1 || NumVertZLevels == (verts.Length - 1))
                        {
                            //Only one of the verts is on a particular Z Level   
                            var LevelA = verts.Where(vert => vert.Position.Z == VertZLevels[0]).ToArray();
                            var LevelB = verts.Where(vert => vert.Position.Z != VertZLevels[0]).ToArray();

                            Geometry.Meshing.IVertex anchor;
                            //Geometry.Meshing.IVertex[] opposite_verts;
                            if (LevelA.Length == 1)
                            {
                                anchor = LevelA[0];
                                //opposite_verts = LevelB;
                            }
                            else
                            {
                                anchor = LevelB.Length == 1 ? LevelB[0] : LevelA.Any() ? LevelA[0] : LevelB[0];
                                //opposite_verts = LevelA;
                            }

                            int iFaceAnchor = face_path.IndexOf(anchor.Index);

                            int iA = iFaceAnchor + 1;
                            int iB = iFaceAnchor + 2;
                            int iC = iFaceAnchor + 3;

                            if (iA >= face_path.Count)
                                iA -= face_path.Count;

                            if (iB >= face_path.Count)
                                iB -= face_path.Count;

                            if (iC >= face_path.Count)
                                iC -= face_path.Count;

                            int O = face_path[iFaceAnchor];
                            int A = face_path[iA];
                            int B = face_path[iB];
                            int C = face_path[iC];

                            MorphMeshFace XAB = new(O, A, B);
                            MorphMeshFace XBC = new(O, B, C);

                            mesh.AddFace(XAB);
                            mesh.AddFace(XBC);
                        }

                    }
                }
                else
                {
                    continue; //Skip this vertex since we could not make a face
                }

                //Check to see if we can add another face if the vertex is not complete yet and we just added a face successfully
                if (v.IsFaceSurfaceComplete(mesh) == false)
                {
                    incompleteVerts.Insert(0, v);
                }
            }
            //mesh.CloseFaces();
        }


        /// <summary>
        /// Convert the OTV table into a set of slice chord candidates
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="OTVTable"></param>
        /// <returns></returns>
        public static List<SliceChord> CreateChordCandidateList(MorphRenderMesh mesh, OTVTable OTVTable)
        {
            List<SliceChord> CandidateChords = [];

            //Create a sorted list of proposed chord lengths
            foreach (IShapeIndex i1 in OTVTable.Keys)
            {
                if (OTVTable.TryGetValue(i1, out IShapeIndex i2))
                {
                    GridVector2 p1 = i1.Point(mesh.Shapes);
                    GridVector2 p2 = i2.Point(mesh.Shapes);

                    if (p1 != p2)
                    {
                        SliceChord sc = new(i1, i2, mesh.Shapes);
                        CandidateChords.Add(sc);
                    }
                    else
                    {
                        //This is a corresponding contour, both at the same X,Y position, add it to our list.
                        MorphMeshEdge edge = new(EdgeType.CORRESPONDING, mesh[i1].Index, mesh[i2].Index);
                        mesh.AddEdge(edge);
                    }
                }
            }

            return CandidateChords;
        }


        /// <summary>
        /// Convert the OTV table into a set of slice chord candidates
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="OTVTable"></param>
        /// <returns></returns>
        private static List<SliceChord> CreateChordCandidateList(MorphRenderMesh mesh, ConcurrentDictionary<MorphMeshVertex, MorphMeshVertex> OTVTable)
        {
            List<SliceChord> CandidateChords = [];

            //Create a sorted list of proposed chord lengths
            foreach (MorphMeshVertex i1 in OTVTable.Keys)
            {
                if (OTVTable.TryGetValue(i1, out MorphMeshVertex i2))
                {
                    GridVector2 p1 = i1.Position.XY();
                    GridVector2 p2 = i2.Position.XY();

                    if (p1 != p2)
                    {
                        SliceChord sc = new(i1.ShapeIndex, i2.ShapeIndex, mesh.Shapes);
                        CandidateChords.Add(sc);
                    }
                    else
                    {
                        //This is a corresponding contour, both at the same X,Y position, add it to our list.
                        MorphMeshEdge edge = new(EdgeType.CORRESPONDING, i1.Index, i2.Index);
                        mesh.AddEdge(edge);
                    }
                }
            }

            return CandidateChords;
        }

        /// <summary>
        /// Attempts to add each SliceChord in the OTV table to our mesh.  
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="OTVTable"></param>
        /// <param name="rTree"></param>
        /// <param name="Tests">A set of flags indicating tests.  Chords must pass the flagged tests before being added.</param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public static int TryAddOTVTable(BajajGeneratorMesh mesh, OTVTable OTVTable, SliceChordRTree rTree, SliceChordTestType Tests, SliceChordPriority priority)
        {
            List<SliceChord> CandidateChords = CreateChordCandidateList(mesh, OTVTable);

            CandidateChords = priority switch
            {
                SliceChordPriority.Distance => [.. CandidateChords.OrderBy(sc => sc.Line.Length)],
                SliceChordPriority.Orientation => [.. CandidateChords.OrderBy(sc => EdgeTypeExtensions.Orientation(sc.Origin, sc.Target, mesh.Shapes))],
                _ => throw new ArgumentException("Unexpected slice chord priority"),
            };

            //List<SliceChord> NovelCandidateChords = CandidateChords.Where(sc => !mesh.IsAnEdge(mesh[sc.Origin].Index, mesh[sc.Target].Index)).ToList();

            int count = 0;
            foreach (SliceChord sc in CandidateChords)
            {
                //TODO: Probably need to check that the chords are all created
                count += TryAddSliceChord(mesh, sc, rTree, Tests) ? 1 : 0;
            }

            return count;
        }


        #endregion

        private static void AddIndexSetToMeshIndexMap(Dictionary<GridVector3, long> map, Geometry.Meshing.Mesh3D<IVertex3D<ulong>> mesh, Geometry.IIndexSet set)
        {
            Geometry.Meshing.IVertex3D[] verts = [.. mesh[set]];
            long[] mesh_indicies = [.. set];

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
        private static Dictionary<GridVector3, long> CreateVertexToMeshIndexMap(Geometry.Meshing.Mesh3D<IVertex3D<ulong>> mesh, IEnumerable<ConnectionVerticies> ports)
        {
            Dictionary<GridVector3, long> map = [];

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

        public static bool Theorem1() => throw new NotImplementedException();

        /// <summary>
        /// Theorem2 requires that the orientation of the contours connected by the slice chord match. 
        /// </summary>
        /// <param name="polygons">Contours on projection slice</param>
        /// <param name="NearestContour">Nearest vertex on projection slice</param>
        /// <param name="p">Point projected</param>
        /// <returns></returns>
        public static bool Theorem2(IReadOnlyList<IShape2D> Polygons, IShapeIndex vertex, IShapeIndex NearestContour)
        {
            if (!(vertex is PolygonIndex v && NearestContour is PolygonIndex nc))
                return true; //Polylines do not have an orientation since they are visible from both sides

            //return EdgeTypeExtensions.OrientationsAreMatched(vertex, NearestContour, Polygons);

            GridVector2 p1 = vertex.Point(Polygons);
            GridVector2 p2 = NearestContour.Point(Polygons);

            if (p1 == p2) //Overlapping vertex always goes in the OTV table
            {
                return true;
            }
            else
            {
                GridLineSegment SliceChord = new(p1, p2);

                bool MatchingOrientations = vertex.IsInner == NearestContour.IsInner;
                /*
                if (!MatchingOrientations && (vertex.IsInner ^ NearestContour.IsInner))
                {
                    GridPolygon pA = Polygons[vertex.iPoly];
                    GridPolygon pB = Polygons[NearestContour.iPoly];

                    bool ExternalContourVertexInsideHole = pA.InteriorPolygonContains(p2) || pB.InteriorPolygonContains(p1);
                    if(ExternalContourVertexInsideHole)
                    {
                        if(!pA.IsVertex(p2) && !pB.IsVertex(p1))
                        {
                            MatchingOrientations = !MatchingOrientations;
                        }
                        
                    }
                }*/

                GridVector2[] adjacent1 = nc.ConnectedVerticies(Polygons);
                GridVector2[] pqr = [adjacent1[0], p2, adjacent1[1]];

                GridVector2[] adjacent2 = v.ConnectedVerticies(Polygons);
                GridVector2[] mno = [adjacent2[0], p1, adjacent2[1]];

                bool IsCorrectSide = p1.IsLeftSide(pqr) != p2.IsLeftSide(mno);

                if (!MatchingOrientations)
                {
                    return !IsCorrectSide;
                }

                return IsCorrectSide;
            }

        }

        public static bool Theorem4(IReadOnlyList<IShape2D> sliceShapes, IShapeIndex NearestContour, GridVector2 p1)
        {
            GridVector2 p2 = NearestContour.Point(sliceShapes);

            GridLineSegment ContourLine = new(p1, p2);

            foreach (IShape2D poly in sliceShapes)
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
        public static bool Theorem4(IReadOnlyList<IShape2D> shapes, GridLineSegment line)
        {
            foreach (IShape2D shape in shapes)
            {
                if (!Theorem4(shape, line))
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
        public static bool Theorem4(IShape2D shape, GridLineSegment line)
        {
            if (shape is GridPolygon poly)
                return !LineIntersectionExtensions.Intersects(line, poly, true, out List<GridVector2> intersections);

            if (shape is GridPolyline polyline)
                return !polyline.Intersects(line);

            throw new NotImplementedException();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="vertex"></param>
        /// <param name="Shapes"></param>
        /// <param name="SameLevelShapes"></param>
        /// <param name="AdjacentLevelShapes"></param>
        /// <param name="candidate"></param>
        /// <param name="chordTree"></param>
        /// <param name="TestsToRun"></param>
        /// <param name="results">Flags are set for any failing tests, though other tests may also fail but were not run due to short circuiting.</param>
        /// <returns></returns>
        public static bool IsSliceChordValid(IShapeIndex vertex, IShape2D[] Shapes, in IReadOnlyList<IShape2D> SameLevelShapes, in IReadOnlyList<IShape2D> AdjacentLevelShapes,
                                                       IShapeIndex candidate, SliceChordRTree chordTree, SliceChordTestType TestsToRun, out SliceChordTestType results)
        {
            results = SliceChordTestType.None;

            GridVector2 p1 = vertex.Point(Shapes);
            GridVector2 p2 = candidate.Point(Shapes);
            if (p1 == p2)
                return true;

            GridLineSegment ChordLine = new(p1, p2);

            if ((TestsToRun & SliceChordTestType.ChordIntersection) > 0)
            {
                //IEnumerable<ISliceChord> existingChords = chordTree.Intersects(ChordLine.BoundingBox.ToRTreeRect(0));
                if (chordTree.IntersectionGenerator(ChordLine.BoundingBox.ToRTreeRect(0)).Any(c => c.Line.Intersects(ChordLine, true)))
                {
                    results |= SliceChordTestType.ChordIntersection;
                    return false;
                }
            }

            if ((TestsToRun & SliceChordTestType.EdgeType) > 0)
            {
                EdgeType edgeType = EdgeTypeExtensions.GetEdgeType(vertex, candidate, Shapes, ChordLine.PointAlongLine(0.5));
                if (!edgeType.IsValid())
                {
                    results |= SliceChordTestType.EdgeType;
                    return false;
                }
            }

            bool AngleOrientation = true;
            bool T2 = true;
            bool T2Opp = true;
            bool T4 = true;
            bool T4Opp = true;

            if ((TestsToRun & SliceChordTestType.LineOrientation) > 0)
            {
                AngleOrientation = EdgeTypeExtensions.OrientationsAreMatched(vertex, candidate, Shapes);
                if (!AngleOrientation)
                {
                    results |= SliceChordTestType.LineOrientation;
                    return false;
                }
            }

            if ((TestsToRun & SliceChordTestType.Theorem2) > 0)
            {
                T2 = Theorem2(Shapes, vertex, candidate);
                if (!T2)
                {
                    results |= SliceChordTestType.Theorem2;
                    return false;
                }

            }

            //bool T2 = true;

            if ((TestsToRun & SliceChordTestType.Theorem4) > 0)
            {
                T4Opp = Theorem4(AdjacentLevelShapes, ChordLine);
                if (!T4Opp)
                {
                    results |= SliceChordTestType.Theorem4;
                    return false;
                }

                T4 = Theorem4(SameLevelShapes, ChordLine);
                if (!T4)
                {
                    results |= SliceChordTestType.Theorem4;
                    return false;
                }
            }

            return AngleOrientation && T2 && T2Opp && T4 && T4Opp;
            //return Theorem2(OppositeContours, candidate, p) && Theorem4(OppositeContours, ContourLine) && Theorem4(Contours, ContourLine);
        }

        public static bool IsSliceChordValid(MorphRenderMesh mesh, MorphMeshVertex vertex, IReadOnlyList<IShape2D> SameLevelShapes, IReadOnlyList<IShape2D> AdjacentLevelShapes,
                                                       MorphMeshVertex candidate, SliceChordRTree chordTree, SliceChordTestType TestsToRun, out SliceChordTestType failures)
        {
            failures = SliceChordTestType.None;
            if (candidate.FacesAreComplete)
                return false;

            return IsSliceChordValid(vertex.ShapeIndex, mesh.Shapes, SameLevelShapes, AdjacentLevelShapes, candidate.ShapeIndex, chordTree, TestsToRun, out failures);

            /*
            GridVector2 p1 = vertex.Position.XY();
            GridVector2 p2 = candidate.Position.XY();
            if (p1 == p2)
                return true;

            if (candidate.FacesAreComplete)
                return false; 

            GridLineSegment ChordLine = new GridLineSegment(p1, p2);

            if ((TestsToRun & SliceChordTestType.ChordIntersection) > 0)
            {
                List<ISliceChord> existingChords = chordTree.Intersects(ChordLine.BoundingBox.ToRTreeRect(0));
                if (existingChords.Any(c => c.Line.Intersects(ChordLine, true)))
                    return false;
            }

            if ((TestsToRun & SliceChordTestType.EdgeType) > 0)
            {
                EdgeType edgeType = EdgeTypeExtensions.GetEdgeType(vertex.PolyIndex.Value, candidate.PolyIndex.Value, mesh.Polygons, ChordLine.PointAlongLine(0.5));
                if (!edgeType.IsValid())
                    return false;
            }

            bool AngleOrientation = true;
            bool T2 = true;
            bool T2Opp = true;
            bool T4 = true;
            bool T4Opp = true;

            if ((TestsToRun & SliceChordTestType.LineOrientation) > 0)
            {
                AngleOrientation = EdgeTypeExtensions.OrientationsAreMatched(vertex.PolyIndex.Value, candidate.PolyIndex.Value, mesh.Polygons);
                if (!AngleOrientation)
                    return false;
            }

            if ((TestsToRun & SliceChordTestType.Theorem2) > 0)
            {
                T2 = Theorem2(mesh.Polygons, vertex.PolyIndex.Value, candidate.PolyIndex.Value);
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
            */
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
        public static List<SliceChord> FindAllSliceChords(PolygonIndex vertex, PolygonIndex[] OppositeVerticies, GridPolygon[] Polygons, IReadOnlyList<GridPolygon> SameLevelPolys, IReadOnlyList<GridPolygon> AdjacentLevelPolys,
                                                              SliceChordRTree chordTree, SliceChordTestType TestsToRun)
        {
            GridVector2 p = vertex.Point(Polygons);

            List<SliceChord> listValid = [];

            foreach (PolygonIndex opposite in OppositeVerticies)
            {
                if (IsSliceChordValid(vertex, Polygons, SameLevelPolys, AdjacentLevelPolys, opposite, chordTree, TestsToRun, out SliceChordTestType failures))
                {
                    SliceChord sc = new(vertex, opposite, Polygons);
                    listValid.Add(sc);
                }
            }

            return listValid;
        }

        /// <summary>
        /// Locate the best slice chord partner for a given vertex
        /// </summary>
        /// <param name="vertex">Vertex we are testing</param>
        /// <param name="Polygons">Polygon array verticies refer to</param>
        /// <param name="SameLevelPolys">Polygons in the array at the same Z level as the vertex</param>
        /// <param name="AdjacentLevelShapes">Polygons in the array at a different Z level as the vertex</param>
        /// <param name="oppositeVertexTreeWithUniqueValues">Lookup data structure for verticies on different Z levels</param>
        /// <param name="chordTree">Lookup data structure for existing slice chords</param>
        /// <returns></returns>
        private static IShapeIndex FindOptimalTilingForVertexByDistance(IShapeIndex vertex, IShape2D[] Polygons, IReadOnlyList<IShape2D> SameLevelPolys, IReadOnlyList<IShape2D> AdjacentLevelShapes,
                                                              QuadTreeWithUniqueValues<IShapeIndex> oppositeVertexTreeWithUniqueValues, SliceChordRTree chordTree, SliceChordTestType TestsToRun)
        {
            GridVector2 p = vertex.Point(Polygons);
            if (oppositeVertexTreeWithUniqueValues.TryFindNearest(p, out var NearestPoint, out double distance) == false)
                return default;


            if (IsSliceChordValid(vertex, Polygons, SameLevelPolys, AdjacentLevelShapes, NearestPoint, chordTree, TestsToRun, out SliceChordTestType failures))
            {
                return NearestPoint;
            }

            //OK, the closest point is not a match.  Expand the search.
            int iNextTest = 1;
            int BatchSize = 1;
            int BatchMultiple = 10;
            List<DistanceToPoint<IShapeIndex>> NearestList = null;

            while (true)
            {
                if (iNextTest >= oppositeVertexTreeWithUniqueValues.Count)
                    return new PolygonIndex?();

                if ((NearestList is null || iNextTest >= NearestList.Count))
                {
                    BatchSize *= BatchMultiple;
                    NearestList = oppositeVertexTreeWithUniqueValues.FindNearestPoints(p, BatchSize);

                    if (NearestList.Count < BatchSize && iNextTest >= NearestList.Count)
                    {
                        return new PolygonIndex?();
                    }
                }

                if (iNextTest < NearestList.Count)
                {
                    IShapeIndex testPoint = NearestList[iNextTest].Value;

                    if (IsSliceChordValid(vertex, Polygons, SameLevelPolys, AdjacentLevelShapes, testPoint, chordTree, TestsToRun, out failures))
                        return testPoint;
                }

                iNextTest++;
            }
        }

        /// <summary>
        /// Locate the best slice chord partner for a given vertex
        /// </summary>
        /// <param name="vertex">Vertex we are testing</param>
        /// <param name="Polygons">Polygon array verticies refer to</param>
        /// <param name="SameLevelShapes">Polygons in the array at the same Z level as the vertex</param>
        /// <param name="AdjacentLevelShapes">Polygons in the array at a different Z level as the vertex</param>
        /// <param name="oppositeVertexTreeWithUniqueValues">Lookup data structure for verticies on different Z levels</param>
        /// <param name="chordTree">Lookup data structure for existing slice chords</param>
        /// <returns></returns>
        private static MorphMeshVertex FindOptimalTilingForVertexByDistance(this MorphRenderMesh mesh, MorphMeshVertex vertex, IReadOnlyList<IShape2D> SameLevelShapes, IReadOnlyList<IShape2D> AdjacentLevelShapes,
                                                              QuadTreeWithUniqueValues<MorphMeshVertex> oppositeVertexTreeWithUniqueValues, SliceChordRTree chordTree, SliceChordTestType TestsToRun)
        {
            GridVector2 p = vertex.Position.XY();
            if (false == oppositeVertexTreeWithUniqueValues.TryFindNearest(p, out var NearestPoint, out double distance))
                return null;

            BajajGeneratorMesh bajajMesh = mesh as BajajGeneratorMesh;
            SliceChordOriginTestResultsCache KnownCandidateFailures = bajajMesh?.SliceChordCandidateCache.GetFailuresForOrigin(vertex.Index);
            SliceChordTestType failures;

            if (NearestPoint.FacesAreComplete == false) //An optimization from profiling. 
            {
                if (IsSliceChordValid(mesh, vertex, SameLevelShapes, AdjacentLevelShapes, NearestPoint, chordTree, TestsToRun, out failures))
                {
                    return NearestPoint;
                }
                else
                {
                    KnownCandidateFailures?.RecordFailure(NearestPoint.Index, failures);
                }
            }

            //OK, the closest point is not a match.  Expand the search.
            int iNextTest = 1;
            int BatchSize = 1;
            const int BatchMultiple = 10;
            List<DistanceToPoint<MorphMeshVertex>> NearestList = null;

            while (true)
            {
                if (iNextTest >= oppositeVertexTreeWithUniqueValues.Count)
                    return null;

                if ((NearestList is null || iNextTest >= NearestList.Count))
                {
                    NearestList = oppositeVertexTreeWithUniqueValues.FindNearestPoints(p, BatchSize);

                    if (NearestList.Count < BatchSize && iNextTest >= NearestList.Count)
                    {
                        return null;
                    }

                    BatchSize *= BatchMultiple;
                }

                if (iNextTest < NearestList.Count)
                {
                    MorphMeshVertex testPoint = NearestList[iNextTest].Value;

                    if (testPoint.FacesAreComplete == false) //An optimization from profiling. 
                    {
                        if (KnownCandidateFailures != null)
                        {
                            if (KnownCandidateFailures.GetFailures(testPoint.Index, TestsToRun) != SliceChordTestType.None) //Check if another pass checked any of these test conditions and already failed
                            {
                                iNextTest++;
                                continue;
                            }
                        }

                        if (IsSliceChordValid(mesh, vertex, SameLevelShapes, AdjacentLevelShapes, testPoint, chordTree, TestsToRun, out failures))
                            return testPoint;
                        else
                        {
                            KnownCandidateFailures?.RecordFailure(NearestPoint.Index, failures); //Record the failure for any future passes
                        }
                    }
                }

                iNextTest++;
            }
        }

        /// <summary>
        /// Return a SortedList<int, List<GridPolygon>> using Z level as the key and lists all polygons for that Z level.
        /// </summary>
        /// <param name="polys"></param>
        /// <param name="PolyZ"></param>
        /// <returns></returns>
        private static SortedList<int, List<IShape2D>> ShapeByLevel(IShape2D[] polys, double[] ShapeZ)
        {
            SortedList<int, List<IShape2D>> levels = [];

            List<int> ZLevels = [.. ShapeZ.Distinct().Select(z => (int)z)];

            foreach (int Z in ZLevels)
            {
                List<IShape2D> level = [.. polys.Where((p, i) => ShapeZ[i] == Z)];
                levels.Add(Z, level);
            }

            return levels;
        }

        private static SortedList<int, List<IShape2D>> ShapeByLevel(this MorphRenderMesh mesh)
        {
            //TODO:  MorphRenderMesh should simply organize the Polygons as a hash table keyed on Z with a list of polygons for each Z value
            SortedList<int, List<IShape2D>> levels = [];

            List<int> ZLevels = [.. mesh.ShapeZ.Distinct().Select(z => (int)z)];

            foreach (int Z in ZLevels)
            {
                List<IShape2D> level = [.. mesh.Shapes.Where((p, i) => mesh.ShapeZ[i] == Z)];
                levels.Add(Z, level);
            }

            return levels;
        }

        /*
        public static void CreateOptimalTilingVertexTable(GridPolygon[] polygons, bool[] IsPolyAbove, SliceChordTestType TestsToRun, out OTVTable OTVTable)
        { 
            SliceChordRTree chordTree = new SliceChordRTree();
            CreateOptimalTilingVertexTable(new PolySetVertexEnum(polygons), polygons, IsPolyAbove, TestsToRun, out OTVTable, ref chordTree);
        }

        public static void CreateOptimalTilingVertexTable(IEnumerable<PointIndex> VerticiesToMap, IEnumerable<PointIndex> CandidateVerticies, GridPolygon[] polygons, bool[] IsPolyAbove, SliceChordTestType TestsToRun, out OTVTable Table, ref SliceChordRTree chordTree)
        { 
            SliceTopologyQuadTrees<PointIndex> LevelTree = CreateQuadTreesForVerticies(CandidateVerticies, polygons, IsPolyAbove);

            ////////////////////////////////////////////////////
            CreateOptimalTilingVertexTable(VerticiesToMap, polygons, IsPolyAbove, LevelTree, TestsToRun, out Table, ref chordTree);
        }
        
        public static ConcurrentDictionary<PointIndex, List<SliceChord>> CreateFullOptimalTilingVertexTable(IEnumerable<PointIndex> VerticiesToMap, IEnumerable<PointIndex> MatchCandidates, GridPolygon[] polygons, bool[] PolyZ, SortedList<int, QuadTreeWithUniqueValues<PointIndex>> CandidateTreeByLevel, SliceChordTestType TestsToRun,
                                                         ref SliceChordRTree chordTree)
        {
            SortedList<int, List<GridPolygon>> levels = PolyByLevel(polygons, PolyZ);
            Debug.Assert(levels.Keys.Count == 2);

            ConcurrentDictionary<PointIndex, List<SliceChord>> OTVTable = new ConcurrentDictionary<PointIndex, List<SliceChord>>();

            SortedList<double, PointIndex[]> CandidatesByLevel = new SortedList<double, PointIndex[]>();

            foreach (var ZLevel in MatchCandidates.GroupBy(v => PolyZ[v.iPoly]))
            {
                CandidatesByLevel.Add(ZLevel.Key, MatchCandidates.ToArray());
            }

            foreach (var polygroup in VerticiesToMap.GroupBy(v => v.iPoly))
            {
                int iPoly = polygroup.Key;
                GridPolygon poly = polygons[iPoly];
                int Z = (int)PolyZ[iPoly];
                int AdjacentZ = (int)PolyZ.Where(adjz => adjz != Z).First();

                //QuadTreeWithUniqueValues<PointIndex> treeWithUniqueValues = CandidateTreeByLevel[AdjacentZ];

                List<GridPolygon> SameLevelPolys = levels[Z];
                List<GridPolygon> AdjacentLevelPolys = levels[AdjacentZ];

                foreach (PointIndex i in polygroup)
                {
                    GridVector2 p1 = i.Point(poly);
                    List<SliceChord> listChords = FindAllSliceChords(i, CandidatesByLevel[AdjacentZ], polygons, SameLevelPolys, AdjacentLevelPolys, chordTree, TestsToRun);
                    if (listChords.Count > 0)
                    {
                        OTVTable.TryAdd(i, listChords);
                    }
                }
            }

            return OTVTable;
        }
        */
        /// <summary>
        /// Find the optimal tiling vertex for the passed verticies
        /// </summary>
        /// <param name="VerticiesToMap"></param>
        /// <param name="polygons"></param>
        /// <param name="PolyZ"></param>
        /// <param name="OTVTable"></param>
        public static void CreateOptimalTilingVertexTable(IEnumerable<IShapeIndex> VerticiesToMap, IShape2D[] shapes, bool[] IsUpperShape, SliceChordTestType TestsToRun, out OTVTable OTVTable, ref SliceChordRTree chordTree)
        {
            SliceTopologyQuadTrees<IShapeIndex> LevelTree = CreateQuadTreesForShapes(shapes, IsUpperShape);

            ////////////////////////////////////////////////////
            CreateOptimalTilingVertexTable(VerticiesToMap, shapes, IsUpperShape, LevelTree, TestsToRun, out OTVTable, ref chordTree);
        }


        public static void CreateOptimalTilingVertexTable(IEnumerable<IShapeIndex> VerticiesToMap, IShape2D[] polygons, bool[] IsUpperShape, SliceTopologyQuadTrees<IShapeIndex> CandidateTreeByLevel, SliceChordTestType TestsToRun,
                                                          out OTVTable Table, ref SliceChordRTree chordTree)
        {
            Table = new OTVTable();

            List<IShape2D> UpperPolygons = [.. polygons.Where((poly, i) => IsUpperShape[i])];
            List<IShape2D> LowerPolygons = [.. polygons.Where((poly, i) => false == IsUpperShape[i])];

            foreach (var shapeGroup in VerticiesToMap.GroupBy(v => v.iShape))
            {
                int iPoly = shapeGroup.Key;
                IShape2D shape = polygons[iPoly];

                QuadTreeWithUniqueValues<IShapeIndex> oppositeTreeWithUniqueValues = CandidateTreeByLevel.GetOppositeSide(iPoly);

                bool IsUpper = IsUpperShape[iPoly];
                List<IShape2D> sameLevelShapes = IsUpper ? UpperPolygons : LowerPolygons;
                List<IShape2D> adjacentLevelShapes = IsUpper ? LowerPolygons : UpperPolygons;

                foreach (IShapeIndex i in shapeGroup)
                {
                    GridVector2 p1 = i.Point(shape);
                    IShapeIndex NearestOnOtherLevel = FindOptimalTilingForVertexByDistance(i, polygons, sameLevelShapes, adjacentLevelShapes, oppositeTreeWithUniqueValues, chordTree, TestsToRun);
                    if (NearestOnOtherLevel is not null)
                    {
                        Table.TryAdd(i, NearestOnOtherLevel);
                    }
                }
            }
        }



        /// <summary>
        /// Find the optimal tiling vertex for the passed verticies
        /// </summary>
        /// <param name="VerticiesToMap"></param>
        /// <param name="polygons"></param>
        /// <param name="PolyZ"></param>
        /// <param name="OTVTable"></param>
        public static void CreateOptimalTilingVertexTable(this BajajGeneratorMesh mesh, IEnumerable<MorphMeshVertex> VerticiesToMap, SliceChordTestType TestsToRun, out ConcurrentDictionary<MorphMeshVertex, MorphMeshVertex> OTVTable, ref SliceChordRTree chordTree)
        {
            var LevelTree = mesh.CreateQuadTreesForContours();

            ////////////////////////////////////////////////////
            CreateOptimalTilingVertexTable(mesh, VerticiesToMap, LevelTree, TestsToRun, out OTVTable, ref chordTree);
        }

        public static void CreateOptimalTilingVertexTable(this BajajGeneratorMesh mesh, IEnumerable<MorphMeshVertex> VerticiesToMap, SliceTopologyQuadTrees<MorphMeshVertex> CandidateTreeByLevel, SliceChordTestType TestsToRun,
                                                          out ConcurrentDictionary<MorphMeshVertex, MorphMeshVertex> OTVTable, ref SliceChordRTree chordTree)
        {
            OTVTable = new ConcurrentDictionary<MorphMeshVertex, MorphMeshVertex>();

            foreach (var polygroup in VerticiesToMap.GroupBy(v => v.ShapeIndex.iShape))
            {
                int iPoly = polygroup.Key;
                IShape2D shape = mesh.Shapes[iPoly];

                QuadTreeWithUniqueValues<MorphMeshVertex> treeWithUniqueValues = CandidateTreeByLevel.GetOppositeSide(iPoly);

                bool IsUpperShape = mesh.UpperShapeIndicies.Contains(iPoly);
                IShape2D[] SameLevelShapes = IsUpperShape ? mesh.UpperShapes : mesh.LowerShapes;
                IShape2D[] AdjacentLevelShapes = IsUpperShape ? mesh.LowerShapes : mesh.UpperShapes;

                foreach (MorphMeshVertex v in polygroup.Where(v => v.FacesAreComplete == false))
                {
                    IShapeIndex i = v.ShapeIndex;
                    GridVector2 p1 = v.Position.XY();
                    MorphMeshVertex NearestOnOtherLevel = mesh.FindOptimalTilingForVertexByDistance(v, SameLevelShapes, AdjacentLevelShapes, treeWithUniqueValues, chordTree, TestsToRun);
                    if (NearestOnOtherLevel != null)
                    {
                        OTVTable.TryAdd(v, NearestOnOtherLevel);
                    }
                }
            }
        }
        /*
        public static SortedList<int, QuadTreeWithUniqueValues<MorphMeshVertex>> CreateQuadTreesForContours(this MorphRenderMesh mesh)
        {
            SortedList<int, QuadTreeWithUniqueValues<MorphMeshVertex>> LevelTree = new SortedList<int, QuadTreeWithUniqueValues<MorphMeshVertex>>();

            //Build a quad treeWithUniqueValues of all points at a given level
            foreach (double Z in mesh.PolyZ.Distinct())
            {
                GridPolygon[] ShapesOnLevel = mesh.Polygons.Where((p, i) => mesh.PolyZ[i] == Z).ToArray();
                GridRectangle bbox = ShapesOnLevel.BoundingBox();
                bbox.Scale(1.05);
                LevelTree.Add((int)Z, new QuadTreeWithUniqueValues<MorphMeshVertex>(bbox));
            }

            var VertsByZLevel = mesh.MorphVerticies.Where(v => v.Type == VertexOrigin.CONTOUR).GroupBy(v => Math.Round(v.Position.Z));
            foreach(var ZLevel in VertsByZLevel)
            {
                double Z = (int)ZLevel.Key;
                QuadTreeWithUniqueValues<MorphMeshVertex> treeWithUniqueValues = LevelTree[(int)Z];
                foreach(var vertex in ZLevel)
                {
                    treeWithUniqueValues.Add(vertex.Position.XY(), vertex);
                }
            }

            return LevelTree;
        }
        */

        public static SliceTopologyQuadTrees<MorphMeshVertex> CreateQuadTreesForContours(this BajajGeneratorMesh mesh)
        {
            QuadTreeWithUniqueValues<MorphMeshVertex> Above = BuildQuadTreeForPolyGroup(mesh, mesh.UpperShapeIndicies);
            QuadTreeWithUniqueValues<MorphMeshVertex> Below = BuildQuadTreeForPolyGroup(mesh, mesh.LowerShapeIndicies);

            return new SliceTopologyQuadTrees<MorphMeshVertex>(Above, Below, mesh.UpperShapeIndicies, mesh.LowerShapeIndicies);
        }

        private static QuadTreeWithUniqueValues<MorphMeshVertex> BuildQuadTreeForPolyGroup(BajajGeneratorMesh mesh, IReadOnlyList<int> polyset)
        {
            if (polyset.Count == 0)
            {
                return new QuadTreeWithUniqueValues<MorphMeshVertex>();
            }

            var ShapesOnLevel = polyset.Select(iPoly => mesh.Shapes[iPoly]);
            GridRectangle bbox = ShapesOnLevel.BoundingBox();
            bbox = GridRectangle.Scale(bbox, 1.05);
            QuadTreeWithUniqueValues<MorphMeshVertex> quadTreeWithUniqueValues = new(bbox);

            var Verts = mesh.MorphVerticies.Where(v => v.Type == VertexOrigin.CONTOUR && v.ShapeIndex is not null && polyset.Contains(v.ShapeIndex.iShape));
            foreach (var vertex in Verts)
            {
                quadTreeWithUniqueValues.TryAdd(vertex.Position.XY(), vertex);
            }

            return quadTreeWithUniqueValues;
        }


        /// <summary>
        /// Build a QuadTreeWithUniqueValues for each Z level containing all points in the polygons on that level
        /// </summary>
        /// <param name="polygons"></param>
        /// <param name="PolyZ"></param>
        /// <returns></returns>
        public static SliceTopologyQuadTrees<IShapeIndex> CreateQuadTreesForShapes(in IReadOnlyList<IShape2D> shapes, bool[] IsUpperShape)
        {
            var shapedata = shapes.Select((shape, i) => new { shape = shape, index = i }).ToArray();
            var upper_lower_groups = shapedata.GroupBy(data => IsUpperShape[data.index]);

            var UpperPolyData = upper_lower_groups.First(group => group.Key == true).Select(group => group);
            var LowerPolyData = upper_lower_groups.First(group => group.Key == false).Select(group => group);

            ImmutableArray<int> UpperPolyIndicies = [.. UpperPolyData.Select(data => data.index)];
            ImmutableArray<int> LowerPolyIndicies = [.. LowerPolyData.Select(data => data.index)];

            QuadTreeWithUniqueValues<IShapeIndex> Above = BuildQuadTreeForPolyGroup([.. UpperPolyData.Select(data => data.shape)],
                                                                                    UpperPolyIndicies);

            QuadTreeWithUniqueValues<IShapeIndex> Below = BuildQuadTreeForPolyGroup([.. LowerPolyData.Select(data => data.shape)],
                                                                                    LowerPolyIndicies);

            return new SliceTopologyQuadTrees<IShapeIndex>(Above, Below, UpperPolyIndicies, LowerPolyIndicies);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ShapesOnLevel"></param>
        /// <param name="iPolyLookup">Index of polygon we should use for PointIndex creation</param>
        /// <returns></returns>
        private static QuadTreeWithUniqueValues<IShapeIndex> BuildQuadTreeForPolyGroup(IShape2D[] ShapesOnLevel, IReadOnlyList<int> iPolyLookup)
        {
            GridRectangle bbox = ShapesOnLevel.BoundingBox();
            bbox = GridRectangle.Scale(bbox, 1.05);
            QuadTreeWithUniqueValues<IShapeIndex> quadTreeWithUniqueValues = new(bbox);

            for (int i = 0; i < ShapesOnLevel.Length; i++)
            {
                int iShape = iPolyLookup[i];
                IShape2D shape = ShapesOnLevel[i];

                if (shape is GridPolygon poly)
                {
                    foreach (PolygonIndex pIndex in new PolygonVertexEnum(poly, iShape))
                    {
                        GridVector2 p1 = pIndex.Point(poly);
                        quadTreeWithUniqueValues.Add(p1, pIndex);
                    }
                }
                else if (shape is GridPolyline line)
                {
                    foreach (PolylineIndex pIndex in new PolylineVertexEnum(line, iShape))
                    {
                        GridVector2 p1 = pIndex.Point(line);
                        quadTreeWithUniqueValues.Add(p1, pIndex);
                    }
                }
            }

            return quadTreeWithUniqueValues;
        }

        /*
        /// <summary>
        /// Build a QuadTreeWithUniqueValues for each Z level containing all points in the polygons on that level
        /// </summary>
        /// <param name="polygons"></param>
        /// <param name="PolyZ"></param>
        /// <returns></returns>
        public static SortedList<int, QuadTreeWithUniqueValues<PointIndex>> CreateQuadTreesForShapes(IReadOnlyList<GridPolygon> polygons, double[] PolyZ)
        {
            SortedList<int, QuadTreeWithUniqueValues<PointIndex>> LevelTree = new SortedList<int, QuadTreeWithUniqueValues<PointIndex>>();

            //Build a quad treeWithUniqueValues of all points at a given level
            foreach (double Z in PolyZ.Distinct())
            {
                LevelTree.Add((int)Z, new QuadTreeWithUniqueValues<PointIndex>(polygons.Where((p,i) => PolyZ[i] == Z).ToArray().BoundingBox()));
            }

            for (int iPoly = 0; iPoly < polygons.Count; iPoly++)
            {
                GridPolygon poly = polygons[iPoly];
                int Z = (int)PolyZ[iPoly];
                if (PolyZ.Contains(Z) == false)
                    continue; 

                QuadTreeWithUniqueValues<PointIndex> treeWithUniqueValues = LevelTree[Z];
                foreach (PointIndex i in new PolygonVertexEnum(poly, iPoly))
                {
                    GridVector2 p1 = i.Point(poly);
                    treeWithUniqueValues.Add(p1, i);
                }
            }

            return LevelTree;
        }
        */

        /// <summary>
        /// 
        /// </summary>
        /// <param name="PolysOnLevel"></param>
        /// <param name="iPolyLookup">Index of polygon we should use for PointIndex creation</param>
        /// <returns></returns>
        private static QuadTreeWithUniqueValues<PolygonIndex> BuildQuadTreeForPolyGroup(IEnumerable<PolygonIndex> Candidates, IReadOnlyList<GridPolygon> PointIndexablePolygons, GridPolygon[] PolysOnLevel)
        {
            GridRectangle bbox = PolysOnLevel.BoundingBox();
            bbox = GridRectangle.Scale(bbox, 1.05);
            QuadTreeWithUniqueValues<PolygonIndex> quadTreeWithUniqueValues = new(bbox);

            foreach (var VertGroup in Candidates.GroupBy(p => p.iPoly))
            {
                int iPoly = VertGroup.Key;
                GridPolygon poly = PointIndexablePolygons[iPoly];

                foreach (PolygonIndex i in VertGroup)
                {
                    GridVector2 p1 = i.Point(poly);
                    quadTreeWithUniqueValues.Add(p1, i);
                }
            }

            return quadTreeWithUniqueValues;
        }
    }
}
