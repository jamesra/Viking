using Geometry;
using MorphologyMesh;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TriangleNet;
using TriangleNet.Meshing;
using OTVTable = System.Collections.Concurrent.ConcurrentDictionary<Geometry.PointIndex, Geometry.PointIndex>;
using SliceChordRTree = RTree.RTree<MorphologyMesh.SliceChord>;

namespace MonogameTestbed
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
        public static List<OTVTable> MergeAndCloseRegionsPass(this MorphMeshRegionGraph graph, MorphRenderMesh mesh, SliceChordRTree rTree = null)
        {
            while (true)
            {
                var regionNode = graph.Nodes.Values.FirstOrDefault(n => n.Edges.Count == 0 && n.Key.Type == RegionType.UNTILED);
                if (regionNode == null)
                    break;

                TryClosingUntiledRegion(mesh, regionNode.Key, rTree);
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
                rTree = mesh.CreateChordTree();
            }

            List<OTVTable> OTVTables = new List<OTVTable>();

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

            //At this point we've merged all of the nodes with one edge.  THere may be triangles of connections but we'll punt on those for the moment.

            //Identify regions with no edges and attempt to close them

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

            return OTVTables;
        }


        public static List<OTVTable> CloseRegions(this MorphRenderMesh mesh, IList<MorphMeshRegion> regions, SliceChordRTree rTree = null)
        {
            //Build the lookup tree for slice-chords
            if (rTree == null)
            {
                rTree = mesh.CreateChordTree();
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

        public static OTVTable TryClosingRegion(MorphRenderMesh mesh, MorphMeshRegion region, SliceChordRTree rTree)
        {
            if (region.Type == RegionType.EXPOSED || region.Type == RegionType.INVAGINATION)
            {
                return TryClosingSolidRegion(mesh, region, rTree);
            }

            if (region.Type == RegionType.HOLE)
            {
                if (!region.IsExposed(mesh))
                {
                    TryClosingHole(mesh, region, rTree);
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
        private static OTVTable TryClosingSolidRegion(this MorphRenderMesh mesh, MorphMeshRegion region, SliceChordRTree rTree)
        {
            OTVTable OTVTable;
            List<int> vertsWithoutFaces = region.Verticies.Where(v => mesh[v].Edges.SelectMany(e => mesh[e].Faces).Count() == 0).ToList();

            BajajMeshGenerator.CreateOptimalTilingVertexTable(vertsWithoutFaces.Select(v => mesh[v].PolyIndex.Value), mesh.Polygons, mesh.PolyZ,
                                                              SliceChordTestType.Correspondance | SliceChordTestType.ChordIntersection | SliceChordTestType.Theorem2 | SliceChordTestType.Theorem4,
                                                              out OTVTable, ref rTree);

            //If we can't map every vertex in the region it needs to be mapped to another region before being capped off
            if (OTVTable.Count < vertsWithoutFaces.Count)
            {
                //Temporary, add faces in the same plane since we couldn't map the entire region.
                //mesh.AddFaces(r.Faces.Select(f => (IFace)f).ToArray());
                return OTVTable;
            }

            int added = BajajOTVAssignmentView.TryAddOTVTable(mesh, OTVTable, rTree, SliceChordTestType.ChordIntersection | SliceChordTestType.LineOrientation, SliceChordPriority.Orientation);
            if (added == OTVTable.Count)
            {
                return null;
            }

            return OTVTable;
        }

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
        }

        /// <summary>
        /// Adds verticies and mesh edges for the medial axis of the untiled region.  The untiled region should be contained inside a single polygonal annotation
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="region"></param>
        /// <param name="rTree"></param>
        private static void TryClosingUntiledRegion(MorphRenderMesh mesh, MorphMeshRegion region, SliceChordRTree rTree)
        {
            GridPolygon regionPolygon = region.Polygon;
            var MedialAxis = MedialAxisFinder.ApproximateMedialAxis(regionPolygon);
            GridVector2[] NewVerts = MedialAxis.Points;
            double MinZ = region.VertPositions.Min(v => v.Z);
            double MaxZ = region.VertPositions.Max(v => v.Z);
            int iNewVerts = mesh.AddVerticies(NewVerts.Select(p => new MorphMeshVertex(new PointIndex?(), p.ToGridVector3((MinZ + MaxZ) / 2.0))).ToArray());

            Dictionary<GridVector2, int> VertexLookup = new Dictionary<GridVector2, int>(NewVerts.Length);
            for(int i = 0; i < NewVerts.Length; i++)
            {
                VertexLookup.Add(NewVerts[i], iNewVerts + i);
            }
            
            foreach(var edge in MedialAxis.Edges)
            {
                int iMeshVertA = VertexLookup[edge.Key.SourceNodeKey];
                int iMeshVertB = VertexLookup[edge.Key.TargetNodeKey];

                mesh.AddEdge(new MorphMeshEdge(EdgeType.MEDIALAXIS, iMeshVertA, iMeshVertB));
            }

            GridVector2[] regionVertPositions = region.VertPositions.Select(v => v.XY()).ToArray();
            for(int i = 0; i < region.Verticies.Length; i++)
            {
                VertexLookup.Add(regionVertPositions[i], region.Verticies[i]);
            }

            IMesh triangulation = regionPolygon.Triangulate(internalPoints: NewVerts);

            foreach(var e in triangulation.ToLines())
            {
                int iA = VertexLookup[e.A];
                int iB = VertexLookup[e.B];

                if (mesh.Contains(iA, iB) == false)
                {
                    mesh.AddEdge(new MorphMeshEdge(EdgeType.SURFACE, iA, iB));
                }
            }

            foreach(var t in triangulation.Triangles)
            {
                int iA = VertexLookup[t.GetVertex(0).ToGridVector2()];
                int iB = VertexLookup[t.GetVertex(1).ToGridVector2()];
                int iC = VertexLookup[t.GetVertex(2).ToGridVector2()];

                mesh.AddFace(iA, iB, iC);
            }
        }
    }
}
