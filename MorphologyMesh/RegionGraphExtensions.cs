using Geometry;
using Geometry.Meshing;
using System.Collections.Generic;
using System.Linq;
//using TriangleNet.Meshing;

namespace MorphologyMesh
{

    public static class RegionGraphExtensions
    {
        /// <summary>
        /// Find nodes with only one edge, attempt to create chords between the nodes.  If we are successful remove the edge. 
        /// Then find nodes with zero edges, attempt to close those regions. Remove the nodes if successful
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="mesh"></param>
        /// <param name="rTree"></param>
        /// <returns>A list of the OTV tables generated when attempting to merge the regions.  Used for debugging</returns>
        public static List<OTVTable> MergeAndCloseRegionsPass(this MorphMeshRegionGraph graph, BajajGeneratorMesh mesh, SliceChordRTree rTree = null, TriangulationMesh<IVertex2D<int>>.ProgressUpdate OnProgress = null)
        {
            while (true)
            {
                var regionNode = graph.Nodes.Values.FirstOrDefault(n => n.Edges.Count == 0 && n.Key.Type == RegionType.UNTILED);
                if (regionNode == null)
                    break;

                try
                {
                    TryClosingUntiledRegion(mesh, regionNode.Key, rTree, OnProgress);
                }
                catch (System.NotImplementedException)
                {

                }

                /*
                OTVTable table = 
                if (table != null)
                {
                    OTVTables.Add(table);
                }
                */
                graph.RemoveNode(regionNode.Key);
            }

            if (rTree == null)
            {
                rTree = mesh.CreateChordTree(graph.ZLevels);
            }

            List<OTVTable> OTVTables = new List<OTVTable>();

            /*
             *TODO: My original vision here was that some logic would pair off interior holes and invaginations even if they didn't overlap.  This project was hard enough so that effort was abandoned. 
             * 
            while (true)
            {
                var regionNode = graph.Nodes.Values.FirstOrDefault(n => n.Edges.Count == 1);
                if (regionNode == null)
                    break;

                MorphMeshRegionGraphEdge edge = regionNode.Edges.First().Value.First();

                OTVTable otvTable = BajajOTVAssignmentView.IdentifyChordCandidatesForRegionPair(mesh, edge.SourceNodeKey, edge.TargetNodeKey, SliceChordTestType.ChordIntersection | SliceChordTestType.LineOrientation | SliceChordTestType.Theorem4, rTree);
                OTVTables.Add(otvTable);

                int ChordsAdded = BajajOTVAssignmentView.TryAddOTVTable(mesh, otvTable, rTree, SliceChordTestType.ChordIntersection | SliceChordTestType.LineOrientation | SliceChordTestType.Theorem4, SliceChordPriority.Orientation);

                if (ChordsAdded > 0)
                    ChordsAdded += BajajOTVAssignmentView.TryAddOTVTable(mesh, otvTable, rTree, SliceChordTestType.ChordIntersection | SliceChordTestType.LineOrientation, SliceChordPriority.Orientation);

                //Handling how to prune the graph in the various cases of all edges added, some edges added, and no edges added isn't fully worked out in my head yet.
                if (ChordsAdded == otvTable.Count)
                {
                    //Remove the edge and region node from the graph
                    graph.RemoveEdge(edge);
                    graph.RemoveNode(regionNode.Key);
                }
                else if (ChordsAdded == 0)
                {
                    graph.RemoveEdge(edge);
                }
                else
                {
                    //Some were added... I want to leave the edge but that's an endless loop
                    graph.RemoveEdge(edge);
                }
            }
            */
            //At this point we've merged all of the nodes with one edge.  THere may be triangles of connections but we'll punt on those for the moment.

            //Identify regions with no edges and attempt to close them
            /*
            while (true)
            {
                var regionNode = graph.Nodes.Values.FirstOrDefault(n => n.Edges.Count == 0);
                if (regionNode == null)
                    break;

                OTVTable table = TryClosingRegion(mesh, regionNode.Key, rTree);
                if (table != null)
                {
                    OTVTables.Add(table);
                }

                graph.RemoveNode(regionNode.Key);

            }
            */
            return OTVTables;
        }


        public static List<OTVTable> CloseRegions(this BajajGeneratorMesh mesh, IList<MorphMeshRegion> regions, SliceChordRTree rTree = null)
        {
            //Build the lookup tree for slice-chords
            if (rTree == null)
            {
                rTree = mesh.CreateChordTree(regions.SelectMany(r => r.ZLevel).Distinct().ToList());
            }

            List<OTVTable> listOTVTables = new List<OTVTable>();
            foreach (MorphMeshRegion unpaired in regions)
            {
                OTVTable table = TryClosingRegion(mesh, unpaired, rTree);
                if (table != null && table.Count > 0)
                    listOTVTables.Add(table);
            }

            return listOTVTables;
        }

        public static OTVTable TryClosingRegion(BajajGeneratorMesh mesh, MorphMeshRegion region, SliceChordRTree rTree)
        {
            if (region.Type == RegionType.EXPOSED || region.Type == RegionType.INVAGINATION)
            {
                return TryClosingSolidRegion(mesh, region, rTree);
            }

            if (region.Type == RegionType.HOLE)
            {
                if (!region.IsExposed(mesh))
                {
                    //TryClosingHole(mesh, region, rTree);
                    TryClosingUntiledRegion(mesh, region, rTree);
                    return new OTVTable();
                }
            }

            if (region.Type == RegionType.UNTILED)
            {
                //Generate the medial axis of the region and repeat the tiling
                TryClosingUntiledRegion(mesh, region, rTree);
            }

            return null;
        }

        /// <summary>
        /// Try to see if the region can be closed.  If a slice chord can be created for every vertex in the region then it is considered closeable. 
        /// This function creates the chords if it is closeable.  Otherwise the OTV table for the region is returned.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="region">Region we are trying to close</param>
        /// <param name="rTree">RTree of all existing chords</param>
        private static OTVTable TryClosingSolidRegion(this BajajGeneratorMesh mesh, MorphMeshRegion region, SliceChordRTree rTree)
        {
            OTVTable OTVTable;
            //TODO: This appears to only select verts without faces... shouldn't we look for any vert without a chord?
            List<int> vertsWithoutFaces = region.Verticies.Where(v => mesh[v].Edges.SelectMany(e => mesh[e].Faces).Count() == 0).ToList();

            BajajMeshGenerator.CreateOptimalTilingVertexTable(vertsWithoutFaces.Select(v => mesh[v].PolyIndex.Value),
                                                              mesh.Polygons, mesh.IsUpperPolygon,
                                                              SliceChordTestType.Correspondance | SliceChordTestType.ChordIntersection | SliceChordTestType.Theorem2 | SliceChordTestType.Theorem4,
                                                              out OTVTable, ref rTree);

            //If we can't map every vertex in the region it needs to be mapped to another region before being capped off
            if (OTVTable.Count < vertsWithoutFaces.Count)
            {
                //Temporary, add faces in the same plane since we couldn't map the entire region.
                //mesh.AddFaces(r.Faces.Select(f => (IFace)f).ToArray());
                return OTVTable;
            }

            int added = BajajMeshGenerator.TryAddOTVTable(mesh, OTVTable, rTree, SliceChordTestType.ChordIntersection | SliceChordTestType.LineOrientation, SliceChordPriority.Orientation);
            if (added == OTVTable.Count)
            {
                return null;
            }

            return OTVTable;
        }

        /*
        /// <summary>
        /// Try to see if the region can be closed.  If a slice chord can be created for every vertex in the region then it is considered closeable. 
        /// This function creates the chords if it is closeable.  Otherwise the OTV table for the region is returned.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="region">Region we are trying to close</param>
        /// <param name="rTree">RTree of all existing chords</param>
        private static void TryClosingHole(MorphRenderMesh mesh, MorphMeshRegion region, SliceChordRTree rTree)
        {
            List<int> vertsWithoutFaces = region.Verticies.Where(v => mesh[v].Edges.SelectMany(e => mesh[e].Faces).Count() == 0).ToList();

            GridVector2 center = region.Polygon.Centroid;
            double CenterZ = mesh.PolyZ.Average(); //Put it halfway between the sections

            int NewVertexIndex = mesh.AddVertex(new MorphMeshVertex(new PointIndex?(), center.ToGridVector3(CenterZ)));

            MorphMeshVertex[] Perimeter = region.RegionPerimeter;
            for (int iVert = 0; iVert < Perimeter.Length; iVert++)
            {

                MorphMeshVertex origin = Perimeter[iVert];
                ///Create the first edge, then create the next edge for the face as we advance around the perimeter
                if (iVert == 0)
                {
                    MorphMeshEdge edge = new MorphMeshEdge(EdgeType.ARTIFICIAL, origin.Index, NewVertexIndex);
                    mesh.AddEdge(edge);
                }

                if (iVert + 1 < Perimeter.Length)
                {

                    MorphMeshEdge edge = new MorphMeshEdge(EdgeType.ARTIFICIAL, Perimeter[iVert + 1].Index, NewVertexIndex);
                    mesh.AddEdge(edge);


                    //I should perhaps create a new edge type "Artificial" for the edges connected to the new verticies I add that aren't part of the polygon
                    MorphMeshFace face = new MorphMeshFace(origin.Index, Perimeter[iVert + 1].Index, NewVertexIndex);
                    mesh.AddFace(face);
                }
            }
        }*/

        /// <summary>
        /// Adds verticies and mesh edges for the medial axis of the untiled region.  The untiled region should be contained inside a single polygonal annotation
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="region"></param>
        /// <param name="rTree"></param>
        private static void TryClosingUntiledRegion(BajajGeneratorMesh mesh, MorphMeshRegion region, SliceChordRTree rTree, TriangulationMesh<IVertex2D<int>>.ProgressUpdate OnProgress = null)
        {
            if(region.Verticies.Length == 3)
            {
                MorphMeshFace face = new MorphMeshFace(region.Verticies);
                mesh.AddFace(face); 
                return; 
            }
            else if (region.Verticies.Length == 4)
            {
                MorphMeshFace face = new MorphMeshFace(region.Verticies); 
                //Split face will add the face too
                mesh.SplitFace(face);
                return;
            }

            GridPolygon regionPolygon = region.Polygon;
            GridVector2 regionPolygonCenter = regionPolygon.Centroid;
            GridPolygon centeredRegionPolygon = regionPolygon.Translate(-regionPolygonCenter);

            //centeredRegionPolygon.IsConvex();

            var MedialAxis = MedialAxisFinder.ApproximateMedialAxis(centeredRegionPolygon);
            MedialAxisVertex[] NewVerts = MedialAxis.Nodes.Values.ToArray();

            System.Diagnostics.Debug.Assert(NewVerts.All(v => centeredRegionPolygon.ContainsExt(v.Key) == OverlapType.CONTAINED), "Interior points must be inside Face");   

            //TODO: Split any edges with an existing face into two parts so we can better merge the medial axis with the existing shape

            if (NewVerts.Length == 0)
            {
                return;
            }

            //double MinZ = region.VertPositions.Min(v => v.Z);
            //double MaxZ = region.VertPositions.Max(v => v.Z);

            double MinZ = mesh.LowerPolyIndicies.Max(i => mesh.PolyZ[i]); //Pick the largest of the low-end Z values
            double MaxZ = mesh.UpperPolyIndicies.Min(i => mesh.PolyZ[i]); //Pick the smallest of the high-end Z values
            double targetZ = (MinZ + MaxZ) / 2.0;

            //TODO: Adjust the Z level of the output based on the type of region and verticies we are connecting to.
            var MedialAxisMeshVerts = NewVerts.Select(mv => new MorphMeshVertex(new MedialAxisIndex(MedialAxis, mv), (mv.Key + regionPolygonCenter).ToGridVector3(targetZ))).ToArray();
            int iNewVerts = mesh.AddVerticies(MedialAxisMeshVerts);

            /*
            foreach(var edge in MedialAxis.Edges)
            {
                int iMeshVertA = VertexLookup[edge.Key.SourceNodeKey];
                int iMeshVertB = VertexLookup[edge.Key.TargetNodeKey];

                mesh.AddEdge(new MorphMeshEdge(EdgeType.MEDIALAXIS, iMeshVertA, iMeshVertB));
            }*/

            /*

            GridVector2[] regionVertPositions = region.VertPositions.Select(v => v.XY()).ToArray();
            for(int i = 0; i < region.Verticies.Length; i++)
            {
                VertexLookup.Add(regionVertPositions[i], region.Verticies[i]);
            }
            */

            var polyMesh = Geometry.Meshing.MeshExtensions.Triangulate(region.RegionPerimeter.Cast<IVertex2D>().ToArray(),
                                                                       MedialAxisMeshVerts.Cast<IVertex2D>().ToArray(),
                                                                       OnProgress);

            //var polyMesh = regionPolygon.Triangulate(iPoly: 0);
            //TriangleNet.Meshing.IMesh triangulation = regionPolygon.Triangulate(internalPoints: NewVerts.Select(v => v.Key).ToArray());

            foreach (var e in polyMesh.Edges.Values)
            {
                int iA = polyMesh[e.A].Data; //Find vertex in the input mesh
                int iB = polyMesh[e.B].Data; //Find vertex in the input mesh

                if (mesh.Contains(iA, iB) == false)
                {
                    EdgeType type = mesh.GetEdgeTypeWithOrientation(iA, iB);
                    MorphMeshEdge newEdge = new MorphMeshEdge(type, iA, iB);
                    //Trace.WriteLine(string.Format("Add edge {0}", newEdge));
                    mesh.AddEdge(newEdge);
                    rTree.Add(mesh.ToSegment(newEdge).BoundingBox.ToRTreeRect(0), new MeshChord(mesh, iA, iB));
                }
            }

            foreach (var polyFace in polyMesh.Faces)
            {
                var MeshFaceVerts = polyFace.iVerts.Select(i => polyMesh[i].Data).ToArray();

                var newFace = new MorphMeshFace(MeshFaceVerts);

                if (mesh.FaceHasCCWWinding(newFace))
                    newFace = new MorphMeshFace(MeshFaceVerts.Reverse());

                newFace.NormalIsKnownCorrect = true;
                mesh.AddFace(newFace);
            }
        }

        /// <summary>
        /// Called on a bajaj mesh to cap either the upper or lower polygons using a method similar to closing an untiled region
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="region"></param>
        /// <param name="rTree"></param>
        /// <param name="OnProgress"></param>
        public static void CapMeshEnd(this BajajGeneratorMesh mesh, bool CloseUpper, TriangulationMesh<IVertex2D<int>>.ProgressUpdate OnProgress = null)
        {
            GridPolygon[] polysToClose = CloseUpper ? mesh.UpperPolygons : mesh.LowerPolygons;

            double MinZ = mesh.LowerPolyIndicies.Max(i => mesh.PolyZ[i]); //Pick the largest of the low-end Z values
            double MaxZ = mesh.UpperPolyIndicies.Min(i => mesh.PolyZ[i]); //Pick the smallest of the high-end Z values

            double HalfThickness = (MaxZ - MinZ) / 2.0;
            double targetZ = CloseUpper ? MaxZ + HalfThickness : MinZ - HalfThickness;

            for(int iPoly = 0; iPoly < mesh.Polygons.Length; iPoly++)
            {
                bool ClosePoly = CloseUpper ? mesh.IsUpperPolygon[iPoly] : !mesh.IsUpperPolygon[iPoly];
                if (ClosePoly == false)
                    continue;

                GridPolygon poly = mesh.Polygons[iPoly];
                {
                    GridVector2 polyCenter = poly.Centroid;
                    GridPolygon centeredPolygon = poly.Translate(-polyCenter);

                    var MedialAxis = MedialAxisFinder.ApproximateMedialAxis(centeredPolygon);
                    MedialAxisVertex[] NewVerts = MedialAxis.Nodes.Values.ToArray();
                    System.Diagnostics.Debug.Assert(NewVerts.All(v => centeredPolygon.Contains(v.Key)), "Interior points must be inside Face");

                    //TODO: Split any edges with an existing face into two parts so we can better merge the medial axis with the existing shape

                    if (NewVerts.Length == 0)
                    {
                        return;
                    }

                    //TODO: Adjust the Z level of the output based on the type of region and verticies we are connecting to.
                    var MedialAxisMeshVerts = NewVerts.Select(mv => new MorphMeshVertex(new MedialAxisIndex(MedialAxis, mv), (mv.Key + polyCenter).ToGridVector3(targetZ))).ToArray();
                    int iNewVerts = mesh.AddVerticies(MedialAxisMeshVerts);

                    PolygonVertexEnum polyVertEnum = new PolygonVertexEnum(poly, iPoly);
                    var PolygonMeshVerticies = polyVertEnum.Select(pi => mesh[pi]).ToList();
                    PolygonMeshVerticies.AddRange(MedialAxisMeshVerts);
                    
                    var capTriangulation = TriangulateCapWithMedialAxis(PolygonMeshVerticies.Select(v => new Vertex2D<MorphMeshVertex>(v.Position.XY(), v)).ToArray(), poly, iPoly,
                                                                               OnProgress: null);

                    //var polyMesh = regionPolygon.Triangulate(iPoly: 0);
                    //TriangleNet.Meshing.IMesh triangulation = regionPolygon.Triangulate(internalPoints: NewVerts.Select(v => v.Key).ToArray());

                    foreach (var e in capTriangulation.Edges.Values)
                    {
                        MorphMeshVertex A = capTriangulation[e.A].Data; //Find vertex in the input mesh
                        MorphMeshVertex B = capTriangulation[e.B].Data; //Find vertex in the input mesh

                        int iA = A.Index;
                        int iB = B.Index;

                        if (mesh.Contains(iA, iB) == false)
                        {
                            EdgeType type = mesh.GetEdgeTypeWithOrientation(iA, iB);
                            MorphMeshEdge newEdge = new MorphMeshEdge(type, iA, iB);
                            //Trace.WriteLine(string.Format("Add edge {0}", newEdge));
                            mesh.AddEdge(newEdge);
                            //rTree.Add(mesh.ToSegment(newEdge).BoundingBox.ToRTreeRect(0), new MeshChord(mesh, iA, iB));
                        }
                    }

                    foreach (var polyFace in capTriangulation.Faces)
                    {
                        var TriVerts = polyFace.iVerts.Select(i => capTriangulation[i]).ToArray();
                        var MeshFaceVerts = TriVerts.Select(tv => tv.Data.Index).ToArray();

                        GridVector3 normal = mesh.Normal(MeshFaceVerts);
                        MorphMeshFace newFace = null;

                        if (CloseUpper)
                        {
                            newFace = normal.Z < 0 ? new MorphMeshFace(MeshFaceVerts) : new MorphMeshFace(MeshFaceVerts.Reverse());
                        }
                        else {
                            newFace = normal.Z > 0 ? new MorphMeshFace(MeshFaceVerts) : new MorphMeshFace(MeshFaceVerts.Reverse());
                        }


                        /*
                        MorphMeshVertex[] positions = mesh[MeshFaceVerts].ToArray();
                        RotationDirection winding = .Winding();
                        MorphMeshFace newFace = null;
                        if (CloseUpper)
                            newFace = winding == RotationDirection.CLOCKWISE ? new MorphMeshFace(MeshFaceVerts) : new MorphMeshFace(MeshFaceVerts.Reverse());
                        else
                            newFace = winding == RotationDirection.COUNTERCLOCKWISE ? new MorphMeshFace(MeshFaceVerts) : new MorphMeshFace(MeshFaceVerts.Reverse());
                            */

                        newFace.NormalIsKnownCorrect = true;
                        mesh.AddFace(newFace);
                    }
                }
            }
        }

        private static TriangulationMesh<IVertex2D<MorphMeshVertex>> TriangulateCapWithMedialAxis(IVertex2D<MorphMeshVertex>[] verts, GridPolygon poly, int iPoly, TriangulationMesh<IVertex2D<MorphMeshVertex>>.ProgressUpdate OnProgress = null)
        {
            TriangulationMesh<IVertex2D<MorphMeshVertex>> triangulation = GenericDelaunayMeshGenerator2D<IVertex2D<MorphMeshVertex>>.TriangulateToMesh(verts, OnProgress);

            PolygonVertexEnum polyVertEnum = new PolygonVertexEnum(poly, iPoly);

            Dictionary<PointIndex, int> polyIndexToTriangulationIndex = new Dictionary<PointIndex, int>();

            //Ensure polygon ring is constrained in the mesh
            foreach (IVertex2D<MorphMeshVertex> vert in verts)
            {
                if (vert.Data.PolyIndex.HasValue == false)
                    continue;

                polyIndexToTriangulationIndex.Add(vert.Data.PolyIndex.Value, vert.Index);
            }

            HashSet<IEdge> constrainedEdges = new HashSet<IEdge>();
            Dictionary<PointIndex, Edge> edgeFacesToCheck = new Dictionary<PointIndex, Edge>();

            foreach (int iPolyVert in polyIndexToTriangulationIndex.Values)
            {
                IVertex2D<MorphMeshVertex> A = triangulation[iPolyVert];
                MorphMeshVertex MMV_A = A.Data;

                IVertex2D<MorphMeshVertex> B = triangulation[polyIndexToTriangulationIndex[MMV_A.PolyIndex.Value.Next]];
                MorphMeshVertex MMV_B = B.Data; // polyIndexToTriangulationIndex[A.PolyIndex.Value.Next]];
                PointIndex polyIndex = MMV_A.PolyIndex.Value;

                ConstrainedEdge edge = new ConstrainedEdge(A.Index, B.Index);
                triangulation.AddConstrainedEdge(edge, OnProgress);
                constrainedEdges.Add(edge);

                //If there are three constrained edges that form an interior polygon that is a triangle the face wont be removed.  This results
                //in a constrained edge with two faces.  For this case remove the interior face after all constrained edges are added
                if (polyIndex.IsInner && polyIndex.NumUniqueInRing == 3)
                {
                    edgeFacesToCheck.Add(polyIndex, edge);
                }
            }

            //Remove edges that are not contained in the polygon, that means we check that the midpoint of edges that connect points on the same ring which are not constrained edges are inside the polygon
            var EdgesToCheck = triangulation.Edges.Keys.Where(k => {
                if (constrainedEdges.Contains(k))
                    return false;

                IVertex2D<MorphMeshVertex> A = triangulation[k.A];
                MorphMeshVertex MMV_A = A.Data;

                IVertex2D<MorphMeshVertex> B = triangulation[k.B];
                MorphMeshVertex MMV_B = B.Data; // polyIndexToTriangulationIndex[A.PolyIndex.Value.Next]];

                if (false == (MMV_A.PolyIndex.HasValue && MMV_B.PolyIndex.HasValue))
                    return false;

                if (MMV_A.PolyIndex.Value.AreOnSameRing(MMV_B.PolyIndex.Value))
                    return true;

                //PointIndex polyIndex = MMV_A.PolyIndex.Value;

                return false;
                }).ToArray();


            foreach (EdgeKey key in EdgesToCheck)
            {
                GridLineSegment line = triangulation.ToGridLineSegment(key);

                if (OverlapType.NONE == poly.ContainsExt(line.Bisect()))
                {
                    triangulation.RemoveEdge(key);

                    if (OnProgress != null)
                    {
                        OnProgress(triangulation);
                    }
                }
            }

            //If there are three constrained edges that form an interior polygon that is a triangle the face wont be removed.  This results
            //in a constrained edge with two faces.  For this case remove the interior face
            foreach (var innerPolyGroup in edgeFacesToCheck.GroupBy(i => i.Key.iInnerPoly))
            {
                GridPolygon innerPolygon = poly.InteriorPolygons[innerPolyGroup.Key.Value];
                GridVector2 Centroid = innerPolygon.Centroid;

                //Figure out the inner polygon vertex numbers in the mesh
                SortedSet<int> innerPolyTriangulationVertIndicies = new SortedSet<int>(innerPolyGroup.SelectMany(g => new int[] { g.Value.A, g.Value.B }));
                IFace[] allFaces = innerPolyGroup.SelectMany(g => g.Value.Faces).Distinct().ToArray();

                IFace[] InteriorFaces = allFaces.Where(f => f.iVerts.All(iVert => innerPolyTriangulationVertIndicies.Contains(iVert))).ToArray();

                //Should only ever be one interior face for a 3 vert interior polygon, unless someone adds interior polygons to interior polygons later <shudder/>
                foreach (IFace f in InteriorFaces)
                {
                    triangulation.RemoveFace(f);

                    if (OnProgress != null)
                    {
                        OnProgress(triangulation);
                    }
                }
            }

            return triangulation;
        }
    }
}
