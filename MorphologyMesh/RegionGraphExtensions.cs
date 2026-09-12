using Geometry;
using Geometry.Meshing;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;

//using TriangleNet.Meshing;

namespace MorphologyMesh
{

    public static class RegionGraphExtensions
    {
        /// <summary>
        /// XY scale of an open-end cap relative to the section contour, about the shape centroid or circle center.
        /// Polylines scale each vertex toward the vertex-average centroid; circles use the same factor on the radius.
        /// </summary>
        private const double EndCapScale = 0.5;

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
            int closedRegions = 0;
            int skippedRegions = 0;
            while (true)
            {
                var regionNode = graph.Nodes.Values.FirstOrDefault(n => n.Edges.Count == 0 && n.Key.Type == RegionType.UNTILED);
                if (regionNode is null)
                    break;

                try
                {
                    if (TryClosingUntiledRegion(mesh, regionNode.Key, rTree, OnProgress))
                        closedRegions++;
                    else
                        skippedRegions++;
                }
                catch (System.NotImplementedException)
                {
                    skippedRegions++;
                }
                catch (System.Exception e)
                {
                    //An unexpected failure closing this region.  Count it as skipped and continue so we still
                    //remove the node below (preventing an infinite re-pick of the same region) and produce a
                    //partial mesh instead of aborting the whole pass.
                    Trace.WriteLine($"Unexpected exception closing untiled region {regionNode.Key} in mesh {mesh}:\n{e}");
                    skippedRegions++;
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

            if (closedRegions + skippedRegions > 0)
                Trace.WriteLine($"MergeAndCloseRegionsPass on mesh {mesh}: closed {closedRegions} of {closedRegions + skippedRegions} untiled regions ({skippedRegions} skipped).");

            //A skipped region leaves an open hole in the mesh.  Flag the mesh so callers do not treat it as a
            //fully successful reconstruction.
            if (skippedRegions > 0)
                mesh.RecordGenerationError($"{skippedRegions} of {closedRegions + skippedRegions} untiled region(s) could not be closed");

            rTree ??= mesh.CreateChordTree(graph.ZLevels);

            List<OTVTable> OTVTables = [];

            /*
             *TODO: My original vision here was that some logic would pair off interior holes and invaginations even if they didn't overlap.  This project was hard enough so that effort was abandoned. 
             * 
            while (true)
            {
                var regionNode = graph.Nodes.Values.FirstOrDefault(n => n.Edges.Count == 1);
                if (regionNode is null)
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
                if (regionNode is null)
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
            rTree ??= mesh.CreateChordTree([.. regions.SelectMany(r => r.ZLevel).Distinct()]);

            List<OTVTable> listOTVTables = [];
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
            //TODO: This appears to only select verts without faces... shouldn't we look for any vert without a chord?
            List<int> vertsWithoutFaces = [.. region.Vertices.Where(v => mesh[v].Edges.SelectMany(e => mesh[e].Faces).Count() == 0)];

            //Candidates are selected and then added under one suite of tests.  These used to differ: the table was
            //built with Theorem2 and Theorem4 but added with LineOrientation, so the region could be judged
            //closeable against criteria that were never applied when the chords were actually created, and the
            //chords could then fail to be added, leaving the region open.
            const SliceChordTestType RegionChordTests = SliceChordTestType.Correspondance
                                                      | SliceChordTestType.ChordIntersection
                                                      | SliceChordTestType.Theorem2
                                                      | SliceChordTestType.Theorem4
                                                      | SliceChordTestType.LineOrientation
                                                      | SliceChordTestType.ShapeLink
                                                      | SliceChordTestType.ForkPartition;

            BajajMeshGenerator.CreateOptimalTilingVertexTable(vertsWithoutFaces.Select(v => mesh[v].ShapeIndex),
                                                              mesh.Shapes, mesh.IsUpperShape,
                                                              RegionChordTests,
                                                              out OTVTable OTVTable, ref rTree,
                                                              mesh.ShapeLinkPredicate, mesh.ForkPartition);

            //If we can't map every vertex in the region it needs to be mapped to another region before being capped off
            if (OTVTable.Count < vertsWithoutFaces.Count)
            {
                //Temporary, add faces in the same plane since we couldn't map the entire region.
                //mesh.AddFaces(r.Faces.Select(f => (IFace)f).ToArray());
                return OTVTable;
            }

            int added = BajajMeshGenerator.TryAddOTVTable(mesh, OTVTable, rTree, RegionChordTests, SliceChordPriority.Orientation);
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
            List<int> vertsWithoutFaces = region.Vertices.Where(v => mesh[v].Edges.SelectMany(e => mesh[e].Faces).Count() == 0).ToList();

            Vector2 center = region.Polygon.Centroid;
            double CenterZ = mesh.PolyZ.Average(); //Put it halfway between the sections

            int NewVertexIndex = mesh.AddVertex(new MorphMeshVertex(new PointIndex?(), center.ToVector3(CenterZ)));

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
        /// <returns>True if the region was closed (or required no work); false if it was skipped because the triangulation failed on degenerate geometry.</returns>
        private static bool TryClosingUntiledRegion(BajajGeneratorMesh mesh, MorphMeshRegion region, SliceChordRTree rTree, TriangulationMesh<IVertex2D<int>>.ProgressUpdate OnProgress = null)
        {
            if (region.Vertices.Length == 3 || region.Vertices.Length == 4)
                return TryClosingSmallRegion(mesh, region);

            Polygon regionPolygon;
            try
            {
                regionPolygon = region.Polygon;
            }
            catch (System.ArgumentException)
            {
                //A corresponding pair at two non-adjacent perimeter positions pinches the region into a figure-8 in
                //XY, which is not a polygon.  RegionPerimeterToFaces already splits such perimeters at the shared XY
                //and tiles each loop (RPC1 145474/145475, region 25-31-26-28-...).
                return TryClosingPinchedRegion(mesh, region);
            }
            Vector2 regionPolygonCenter = regionPolygon.Centroid;
            Polygon centeredRegionPolygon = regionPolygon.Translate(-regionPolygonCenter);

            //centeredRegionPolygon.IsConvex();

            var MedialAxis = MedialAxisFinder.ApproximateMedialAxis(centeredRegionPolygon);
            MedialAxisVertex[] NewVerts = [.. MedialAxis.Nodes.Values
                .Where(v => centeredRegionPolygon.GetRelation(v.Key) == ShapeRelation.Contained)];

            //TODO: Split any edges with an existing face into two parts so we can better merge the medial axis with the existing shape

            if (NewVerts.Length == 0)
            {
                //The medial axis approximation produced no usable interior points (none, or all fell outside
                //the region polygon). Skip rather than Debug.Assert/FailFast — one bad region must not kill the process.
                Trace.WriteLine($"Skipping untiled region {region} in mesh {mesh}: medial axis produced no interior points inside the face.");
                mesh.RecordGenerationError($"region {region}: medial axis produced no interior points");
                return false;
            }

            //Fallback Z (flat mid-plane) used only when the region perimeter carries no Z information.
            double fallbackZ = mesh.SliceCenterZ;

            //Interpolate each medial-axis vertex's Z from the region perimeter (Edwards 2011) instead of
            //flattening the whole skeleton to the mid-plane.  Perimeter positions are in absolute coordinates,
            //while the medial-axis vertices live in the centered space used for triangulation, so compare them
            //in the same (centered) frame.
            MorphMeshVertex[] perimeter = region.RegionPerimeter;
            Vector2[] perimeterCenteredXY = [.. perimeter.Select(v => v.Position.XY() - regionPolygonCenter)];
            double[] perimeterZ = [.. perimeter.Select(v => v.Position.Z)];

            //Build the medial-axis verticies but DO NOT add them to the mesh yet.  Pre-assign the indicies they
            //will receive so the triangulation can map its output back to these verticies, then commit them to
            //the mesh only if triangulation succeeds.  This prevents orphan verticies when triangulation fails
            //on degenerate input.
            int predictedStartIndex = mesh.Vertices.Count;
            var MedialAxisMeshVerts = NewVerts.Select((mv, k) =>
            {
                double vertZ = InterpolateZFromPerimeter(mv.Key, perimeterCenteredXY, perimeterZ, fallbackZ);
                MorphMeshVertex vtx = new(new MedialAxisIndex(MedialAxis, mv), (mv.Key + regionPolygonCenter).ToVector3(vertZ));
                vtx.SetIndex(predictedStartIndex + k);
                return vtx;
            }).ToArray();

            /*
            foreach(var edge in MedialAxis.Edges)
            {
                int iMeshVertA = VertexLookup[edge.Key.SourceNodeKey];
                int iMeshVertB = VertexLookup[edge.Key.TargetNodeKey];

                mesh.AddEdge(new MorphMeshEdge(EdgeType.MEDIALAXIS, iMeshVertA, iMeshVertB));
            }*/

            /*

            Vector2[] regionVertPositions = region.VertPositions.Select(v => v.XY()).ToArray();
            for(int i = 0; i < region.Vertices.Length; i++)
            {
                VertexLookup.Add(regionVertPositions[i], region.Vertices[i]);
            }
            */

            //Clean degenerate input (coincident / colinear perimeter points and duplicate interior points) before
            //triangulating.  Without this the divide-and-conquer Delaunay generator throws on degenerate geometry.
            var (cleanedPerimeter, cleanedInterior) = Geometry.Meshing.MeshExtensions.CleanRegionTriangulationInput(
                [.. region.RegionPerimeter.Cast<IVertex2D>()],
                [.. MedialAxisMeshVerts.Cast<IVertex2D>()]);

            if (cleanedPerimeter.Length < 3)
            {
                //No mesh verticies have been committed yet, so there is nothing to roll back.
                Trace.WriteLine($"Skipping untiled region {region}: fewer than 3 unique perimeter points after cleaning.");
                mesh.RecordGenerationError($"region {region}: fewer than 3 unique perimeter points");
                return false;
            }

            TriangulationMesh<IVertex2D<int>> polyMesh;
            try
            {
                polyMesh = Geometry.Meshing.MeshExtensions.Triangulate(cleanedPerimeter, cleanedInterior, OnProgress);
            }
            catch (System.Exception e) when (e is GeometryMeshExceptionBase || e is System.ArgumentException)
            {
                //Degenerate triangulation input (near-duplicate or colinear points) that survived cleaning.
                //Log the offending region so it can be reproduced deterministically, then skip it rather than
                //aborting the entire region-closing pass for this mesh.  No mesh verticies were committed yet,
                //so the failed region leaves no orphan geometry behind.
                Trace.WriteLine($"Skipping untiled region {region} in mesh {mesh}: triangulation failed ({e.GetType().Name}: {e.Message})\n{DescribeTriangulationInput(cleanedPerimeter, cleanedInterior)}");
                mesh.RecordGenerationError($"region {region}: triangulation failed ({e.GetType().Name}: {e.Message})");
                return false;
            }

            //A perimeter walked out from a pinched corresponding pair can run along edges that already carry their
            //full complement of faces; tiling it would turn those into 3 and 4-face edges (RPC1 83509/83711, 82622/82684
            //after the split-loop regions started closing).  Leaving the region open is the smaller defect.
            if (TryFindEdgeAtFaceCapacity(mesh, polyMesh, out string fullEdge))
            {
                Trace.WriteLine($"Skipping untiled region {region} in mesh {mesh}: perimeter edge {fullEdge} already has its full complement of faces.");
                mesh.RecordGenerationError($"region {region}: perimeter edge {fullEdge} already at face capacity");
                return false;
            }

            //Triangulation succeeded, so commit the medial-axis verticies to the mesh.  Their pre-assigned
            //indicies match the indicies AddVerticies assigns because nothing else mutated the mesh in between.
            int iNewVerts = mesh.AddVerticies(MedialAxisMeshVerts);
            System.Diagnostics.Debug.Assert(iNewVerts == predictedStartIndex, "Medial axis vertex indicies must match the indicies predicted before triangulation");

            //var polyMesh = regionPolygon.Triangulate(iPoly: 0);
            //TriangleNet.Meshing.IMesh triangulation = regionPolygon.Triangulate(internalPoints: NewVerts.Select(v => v.Key).ToArray());

            foreach (var e in polyMesh.Edges.Values)
            {
                int iA = polyMesh[e.A].Data; //Find vertex in the input mesh
                int iB = polyMesh[e.B].Data; //Find vertex in the input mesh

                if (mesh.Contains(iA, iB) == false)
                {
                    EdgeType type = mesh.GetEdgeTypeWithOrientation(iA, iB);
                    MorphMeshEdge newEdge = new(type, iA, iB);
                    //Trace.WriteLine(string.Format("Add edge {0}", newEdge));
                    mesh.AddEdge(newEdge);
                    rTree.Add(mesh.ToSegment(newEdge).BoundingBox.ToRTreeRect(0), new MeshChord(mesh, iA, iB));
                }
            }

            //The triangulation tiles one planar patch, so its faces are wound consistently once each is made CCW in
            //XY.  Deciding the outward side face by face with FaceHasCCWWinding gave neighbouring faces of the same
            //patch opposite answers near the contour, and because the faces are anchored (NormalIsKnownCorrect) the
            //final reorientation could not repair them (RPC1 82671/82673, 82659/82660).  Vote once for the patch.
            List<int[]> patch = [];
            int reverseVotes = 0;
            foreach (var polyFace in polyMesh.Faces)
            {
                int[] triIndicies = [.. polyFace.iVerts];
                if (triIndicies.Length == 3)
                {
                    Vector2 a = polyMesh[triIndicies[0]].Position;
                    Vector2 b = polyMesh[triIndicies[1]].Position;
                    Vector2 c = polyMesh[triIndicies[2]].Position;
                    if (a.Winding(b, c) == RotationDirection.Clockwise)
                        System.Array.Reverse(triIndicies);
                }

                int[] meshFaceVerts = [.. triIndicies.Select(i => polyMesh[i].Data)];
                patch.Add(meshFaceVerts);

                if (mesh.FaceHasCCWWinding(new MorphMeshFace(meshFaceVerts)))
                    reverseVotes++;
            }

            //Patch faces are anchored, so a patch that disagrees with an anchored neighbour (an earlier patch or a
            //cap) along a shared edge can never be repaired afterwards (RPC1 364005/364006/364007, where a second
            //closing pass tiled up to a first-pass patch).  When such a neighbour exists it decides the flip; the
            //XY heuristic only breaks the tie for a patch with no fixed neighbours.
            if (CountAnchoredNeighbourDirections(mesh, patch, out int sameDirection, out int oppositeDirection))
                reverseVotes = sameDirection > oppositeDirection ? patch.Count : 0;

            bool reversePatch = reverseVotes * 2 > patch.Count;
            foreach (int[] meshFaceVerts in patch)
            {
                MorphMeshFace newFace = reversePatch ? new MorphMeshFace(meshFaceVerts.Reverse()) : new MorphMeshFace(meshFaceVerts);
                newFace.NormalIsKnownCorrect = true;
                mesh.AddFace(newFace);
            }

            return true;
        }

        /// <summary>
        /// Closes a three or four vertex region with one or two triangles.  The ring comes from the ordered perimeter
        /// rather than <see cref="MorphMeshRegion.Vertices"/>, which lists the verticies in face enumeration order
        /// and so does not describe a quad's boundary, and a triangle that would overfill an existing edge is
        /// refused: a four vertex region walked out from a pinched corresponding pair sat on a CORRESPONDING edge
        /// that already had both its faces (RPC1 83509/83711).
        /// </summary>
        /// <summary>
        /// Tiles a region whose perimeter revisits one XY at a corresponding pair, by splitting it into loops at the
        /// shared point.  The whole patch is skipped if any face would overfill a perimeter edge.
        /// </summary>
        private static bool TryClosingPinchedRegion(BajajGeneratorMesh mesh, MorphMeshRegion region)
        {
            List<int> ring = [];
            foreach (MorphMeshVertex v in region.RegionPerimeter)
            {
                if (ring.Count == 0 || ring[^1] != v.Index)
                    ring.Add(v.Index);
            }
            while (ring.Count > 1 && ring[0] == ring[^1])
                ring.RemoveAt(ring.Count - 1);

            List<MorphMeshFace> patch = MorphRenderMesh.RegionPerimeterToFaces(mesh, ring);
            if (patch.Count == 0)
            {
                Trace.WriteLine($"Skipping pinched untiled region {region} in mesh {mesh}: perimeter could not be split into loops.");
                mesh.RecordGenerationError($"region {region}: pinched perimeter could not be tiled");
                return false;
            }

            //The loops are independent surfaces, so a face that would overfill an edge (typically the corresponding
            //edge at the pinch, which a sliver face already occupies) is dropped alone rather than with the patch.
            int added = 0;
            foreach (MorphMeshFace face in patch)
            {
                bool fits = true;
                foreach (IEdgeKey key in face.Edges)
                {
                    if (mesh.Contains(key.A, key.B) == false)
                        continue;

                    MorphMeshEdge edge = (MorphMeshEdge)mesh[key];
                    int capacity = edge.Type == EdgeType.CONTOUR ? 1 : 2;
                    if (edge.Faces.Count >= capacity)
                    {
                        Trace.WriteLine($"Pinched untiled region {region} in mesh {mesh}: face {face} dropped, edge {edge} already has its full complement of faces.");
                        fits = false;
                        break;
                    }
                }

                if (!fits)
                    continue;

                mesh.AddFace(face);
                added++;
            }

            //Dropped faces are not recorded as generation errors: the slots they wanted are already occupied, so the
            //surface there is complete (RPC1 366418/366419 is whole with two of three dropped) and a real gap shows
            //up in the manifold report anyway.
            return added > 0;
        }

        private static bool TryClosingSmallRegion(BajajGeneratorMesh mesh, MorphMeshRegion region)
        {
            List<int> ring = [];
            foreach (MorphMeshVertex v in region.RegionPerimeter)
            {
                if (ring.Contains(v.Index) == false)
                    ring.Add(v.Index);
            }

            if (ring.Count < 3)
            {
                Trace.WriteLine($"Skipping untiled region {region} in mesh {mesh}: perimeter has fewer than 3 verticies.");
                mesh.RecordGenerationError($"region {region}: perimeter has fewer than 3 verticies");
                return false;
            }

            List<int[]> patch = [];
            if (ring.Count == 3)
            {
                patch.Add([.. ring]);
            }
            else
            {
                Vector3[] p = [.. ring.Select(i => mesh[i].Position)];
                if (Vector3.Distance(p[0], p[2]) < Vector3.Distance(p[1], p[3]))
                {
                    patch.Add([ring[0], ring[1], ring[2]]);
                    patch.Add([ring[0], ring[2], ring[3]]);
                }
                else
                {
                    patch.Add([ring[0], ring[1], ring[3]]);
                    patch.Add([ring[1], ring[2], ring[3]]);
                }
            }

            foreach (int[] faceVerts in patch)
            {
                for (int i = 0; i < faceVerts.Length; i++)
                {
                    int a = faceVerts[i];
                    int b = faceVerts[(i + 1) % faceVerts.Length];
                    if (mesh.Contains(a, b) == false)
                        continue;

                    MorphMeshEdge edge = (MorphMeshEdge)mesh[new EdgeKey(a, b)];
                    int capacity = edge.Type == EdgeType.CONTOUR ? 1 : 2;
                    if (edge.Faces.Count >= capacity)
                    {
                        Trace.WriteLine($"Skipping untiled region {region} in mesh {mesh}: perimeter edge {edge} already has its full complement of faces.");
                        mesh.RecordGenerationError($"region {region}: perimeter edge {edge} already at face capacity");
                        return false;
                    }
                }
            }

            foreach (int[] faceVerts in patch)
                mesh.AddFace(new MorphMeshFace(faceVerts));

            return true;
        }

        /// <summary>
        /// True when a triangle of the region triangulation would add a face to a mesh edge that already carries as
        /// many faces as a slice surface allows (one on a contour edge, two elsewhere).  Only edges between existing
        /// mesh verticies are considered; edges to the not-yet-committed medial axis verticies are new.
        /// </summary>
        private static bool TryFindEdgeAtFaceCapacity(BajajGeneratorMesh mesh, TriangulationMesh<IVertex2D<int>> polyMesh, out string fullEdge)
        {
            foreach (IFace polyFace in polyMesh.Faces)
            {
                foreach (IEdgeKey polyEdge in polyFace.Edges)
                {
                    int iA = polyMesh[polyEdge.A].Data;
                    int iB = polyMesh[polyEdge.B].Data;
                    if (iA >= mesh.Vertices.Count || iB >= mesh.Vertices.Count || mesh.Contains(iA, iB) == false)
                        continue;

                    MorphMeshEdge edge = (MorphMeshEdge)mesh[new EdgeKey(iA, iB)];
                    int capacity = edge.Type == EdgeType.CONTOUR ? 1 : 2;
                    if (edge.Faces.Count >= capacity)
                    {
                        fullEdge = edge.ToString();
                        return true;
                    }
                }
            }

            fullEdge = null;
            return false;
        }

        /// <summary>
        /// For every edge of the (CCW in XY) patch that already carries an anchored face, records whether that face
        /// walks the edge in the same direction as the patch face (inconsistent, so the patch must flip) or the
        /// opposite direction (consistent).  Returns false when no anchored neighbour touches the patch.
        /// </summary>
        private static bool CountAnchoredNeighbourDirections(BajajGeneratorMesh mesh, List<int[]> patch, out int sameDirection, out int oppositeDirection)
        {
            sameDirection = 0;
            oppositeDirection = 0;
            foreach (int[] faceVerts in patch)
            {
                for (int i = 0; i < faceVerts.Length; i++)
                {
                    int a = faceVerts[i];
                    int b = faceVerts[(i + 1) % faceVerts.Length];
                    if (mesh.Contains(a, b) == false)
                        continue;

                    foreach (IFace neighbour in mesh[new EdgeKey(a, b)].Faces)
                    {
                        if (neighbour is not MorphMeshFace morphFace || morphFace.NormalIsKnownCorrect == false)
                            continue;

                        if (TraversesForward(neighbour.iVerts, a, b))
                            sameDirection++;
                        else
                            oppositeDirection++;
                    }
                }
            }

            return sameDirection + oppositeDirection > 0;
        }

        private static bool TraversesForward(System.Collections.Immutable.ImmutableArray<int> iVerts, int a, int b)
        {
            for (int i = 0; i < iVerts.Length; i++)
            {
                if (iVerts[i] == a && iVerts[(i + 1) % iVerts.Length] == b)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Interpolates the Z value for an interior (medial-axis) point from the region perimeter using inverse
        /// distance weighting (Edwards 2011).  This gives the closing mesh a smoothly varying surface that
        /// follows the perimeter Z, rather than flattening every interior vertex to the slice mid-plane.
        /// </summary>
        /// <param name="point">The interior point, in the same (centered) frame as <paramref name="perimeterXY"/></param>
        /// <param name="perimeterXY">The region perimeter vertex positions (XY) in the centered frame</param>
        /// <param name="perimeterZ">The region perimeter vertex Z values, parallel to <paramref name="perimeterXY"/></param>
        /// <param name="fallbackZ">The Z to use when the perimeter is empty</param>
        /// <returns>The interpolated Z value</returns>
        private static double InterpolateZFromPerimeter(Vector2 point, Vector2[] perimeterXY, double[] perimeterZ, double fallbackZ)
        {
            if (perimeterXY.Length == 0)
                return fallbackZ;

            double weightSum = 0;
            double weightedZSum = 0;

            for (int i = 0; i < perimeterXY.Length; i++)
            {
                double distSq = Vector2.DistanceSquared(point, perimeterXY[i]);

                //Coincident with a perimeter vertex: snap to that vertex's Z exactly.
                if (distSq <= Global.EpsilonSquared)
                    return perimeterZ[i];

                double weight = 1.0 / distSq;
                weightSum += weight;
                weightedZSum += weight * perimeterZ[i];
            }

            return weightedZSum / weightSum;
        }

        /// <summary>
        /// Formats the perimeter and interior points fed to the region triangulation so a failing region can be
        /// reproduced deterministically from the log output.
        /// </summary>
        private static string DescribeTriangulationInput(IReadOnlyList<IVertex2D> perimeter, IReadOnlyList<IVertex2D> interior)
        {
            System.Text.StringBuilder sb = new();
            sb.AppendLine($"  Perimeter ({perimeter.Count} points):");
            foreach (IVertex2D v in perimeter)
                sb.AppendLine($"    I:{v.Index} P:({v.Position.X:F4}, {v.Position.Y:F4})");

            sb.AppendLine($"  Interior ({interior.Count} points):");
            foreach (IVertex2D v in interior)
                sb.AppendLine($"    I:{v.Index} P:({v.Position.X:F4}, {v.Position.Y:F4})");

            return sb.ToString();
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
            //A cap extends half a section beyond the contour it closes, so the annotation occupies its own section
            //rather than collapsing onto the contour plane.
            double halfThickness = mesh.SliceThickness / 2.0;

            //Normally the shapes to cap sit on the band being closed.  An isolated annotation is the exception: its
            //slice holds the contour on one band and nothing on the other, and the open side is the empty band, so
            //the cap has to be built from the populated band and extended toward the empty one.
            bool capBandIsUpper = CloseUpper;
            if (CloseUpper && mesh.UpperShapeIndicies.Count == 0)
                capBandIsUpper = false;
            else if (CloseUpper == false && mesh.LowerShapeIndicies.Count == 0)
                capBandIsUpper = true;

            for (int iPoly = 0; iPoly < mesh.Shapes.Length; iPoly++)
            {
                if (mesh.IsUpperShape[iPoly] != capBandIsUpper)
                    continue;

                if (mesh.Shapes[iPoly] is Polygon poly)
                {
                    if (TryCapCirclePolygon(mesh, poly, iPoly, CloseUpper, halfThickness))
                        continue;

                    Vector2 polyCenter = poly.Centroid;
                    Polygon centeredPolygon = poly.Translate(-polyCenter);

                    var MedialAxis = MedialAxisFinder.ApproximateMedialAxis(centeredPolygon);
                    MedialAxisVertex[] NewVerts = DeduplicateMedialAxisVerts(
                        [.. MedialAxis.Nodes.Values.Where(v => centeredPolygon.Covers(v.Key))],
                        (double)Global.Epsilon * 100.0);

                    //TODO: Split any edges with an existing face into two parts so we can better merge the medial axis with the existing shape

                    if (NewVerts.Length == 0)
                    {
                        //This polygon has no medial axis to cap with, but other polygons on this end still need capping.
                        continue;
                    }

                    //The cap is a dome, not a plateau.  Every medial axis vertex used to be placed at a single
                    //target Z, which produced a flat-topped surface joined to the contour by a vertical wall.
                    //Instead each vertex rises in proportion to how deep inside the contour it sits: the vertex
                    //with the greatest clearance from the boundary reaches the full half-section, and vertices
                    //near the boundary stay near the contour so the cap meets the ring smoothly.
                    double contourZ = mesh.ShapeZ[iPoly];
                    double peakOffset = CloseUpper ? halfThickness : -halfThickness;

                    double[] clearance = [.. NewVerts.Select(v => BoundaryClearance(centeredPolygon, v.Key))];
                    double maxClearance = clearance.Max();

                    //Build the cap verticies with the indicies they will receive, but hold them back until the
                    //triangulation succeeds.  Committing first left orphan verticies behind whenever the
                    //triangulation threw on this polygon.
                    int predictedStartIndex = mesh.Vertices.Count;
                    var MedialAxisMeshVerts = NewVerts.Select((mv, k) =>
                    {
                        double depthFraction = maxClearance > 0 ? clearance[k] / maxClearance : 0;
                        double vertZ = contourZ + (peakOffset * depthFraction);
                        MorphMeshVertex vtx = new(new MedialAxisIndex(MedialAxis, mv), (mv.Key + polyCenter).ToVector3(vertZ));
                        vtx.SetIndex(predictedStartIndex + k);
                        return vtx;
                    }).ToArray();

                    PolygonVertexEnum polyVertEnum = new(poly, iPoly);
                    List<MorphMeshVertex> PolygonMeshVerticies = [.. polyVertEnum.Select(pi => mesh[pi])];
                    PolygonMeshVerticies.AddRange(MedialAxisMeshVerts);

                    TriangulationMesh<IVertex2D<MorphMeshVertex>> capTriangulation;
                    try
                    {
                        capTriangulation = TriangulateCapWithMedialAxis([.. PolygonMeshVerticies.Select(v => new Vertex2D<MorphMeshVertex>(v.Position.XY(), v))],
                                                                        poly,
                                                                        iPoly,
                                                                        OnProgress: null);
                    }
                    catch (System.Exception e) when (e is GeometryMeshExceptionBase || e is System.ArgumentException)
                    {
                        //Capping one polygon must not abandon the rest of the mesh.  This end of this polygon stays
                        //open, which the manifold report will show as a hole, but the tiled surface is still usable.
                        Trace.WriteLine($"Could not cap shape {iPoly} of mesh {mesh}: triangulation failed ({e.GetType().Name}: {e.Message})");
                        mesh.RecordGenerationError($"cap of shape {iPoly}: triangulation failed ({e.GetType().Name}: {e.Message})");
                        continue;
                    }

                    int iNewVerts = mesh.AddVerticies(MedialAxisMeshVerts);
                    System.Diagnostics.Debug.Assert(iNewVerts == predictedStartIndex, "Cap vertex indicies must match the indicies predicted before triangulation");

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
                            MorphMeshEdge newEdge = new(type, iA, iB);
                            //Trace.WriteLine(string.Format("Add edge {0}", newEdge));
                            mesh.AddEdge(newEdge);
                            //rTree.Add(mesh.ToSegment(newEdge).BoundingBox.ToRTreeRect(0), new MeshChord(mesh, iA, iB));
                        }
                    }

                    foreach (var polyFace in capTriangulation.Faces)
                    {
                        var TriVerts = polyFace.iVerts.Select(i => capTriangulation[i]).ToArray();
                        var MeshFaceVerts = TriVerts.Select(tv => tv.Data.Index).ToArray();

                        Vector3 normal = mesh.Normal(MeshFaceVerts);
                        MorphMeshFace newFace = CloseUpper
                            ? normal.Z < 0 ? new MorphMeshFace(MeshFaceVerts) : new MorphMeshFace(MeshFaceVerts.Reverse())
                            : normal.Z > 0 ? new MorphMeshFace(MeshFaceVerts) : new MorphMeshFace(MeshFaceVerts.Reverse());


                        /*
                        MorphMeshVertex[] positions = mesh[MeshFaceVerts].ToArray();
                        RotationDirection winding = .Winding();
                        MorphMeshFace newFace = null;
                        if (CloseUpper)
                            newFace = winding == RotationDirection.Clockwise ? new MorphMeshFace(MeshFaceVerts) : new MorphMeshFace(MeshFaceVerts.Reverse());
                        else
                            newFace = winding == RotationDirection.Counterclockwise ? new MorphMeshFace(MeshFaceVerts) : new MorphMeshFace(MeshFaceVerts.Reverse());
                            */

                        newFace.NormalIsKnownCorrect = true;
                        mesh.AddFace(newFace);
                    }
                }
                else if (mesh.Shapes[iPoly] is Polyline line)
                {
                    CapPolylineEnd(mesh, line, iPoly, CloseUpper, halfThickness);
                }
                else
                {
                    //Only polygons and polylines can be capped.  A shape reaching here leaves an open end in the surface, so make
                    //that visible instead of silently producing a mesh with a hole where the cap should be.
                    Trace.WriteLine($"Cannot cap {mesh.Shapes[iPoly].GetType().Name} at shape index {iPoly} of mesh {mesh}.  This end of the mesh is left open.");
                }
            }
        }

        /// <summary>
        /// When the source annotation is a circle, cap with a concentric circle at 50% radius instead of a medial-axis dome.
        /// </summary>
        private static bool TryCapCirclePolygon(BajajGeneratorMesh mesh, Polygon poly, int iPoly, bool closeUpper, double halfThickness)
        {
            if (mesh.Topology.IsValid == false)
                return false;

            LocationType[] types = mesh.Topology.ShapeLocationTypes;
            Circle[] circles = mesh.Topology.ShapeCircles;
            if (types is null || iPoly >= types.Length || types[iPoly] != LocationType.CIRCLE)
                return false;

            Circle sourceCircle = circles is not null && iPoly < circles.Length && circles[iPoly].Radius > 0
                ? circles[iPoly]
                : InferCircleFromPolygon(poly);

            CapCircleEnd(mesh, poly, iPoly, closeUpper, halfThickness, sourceCircle);
            return true;
        }

        private static Circle InferCircleFromPolygon(Polygon poly)
        {
            Vector2 center = poly.Centroid;
            Vector2[] ring = poly.ExteriorRing;
            double radius = 0;
            for (int i = 0; i < ring.Length; i++)
                radius += Vector2.Distance(center, ring[i]);
            radius /= System.Math.Max(1, ring.Length);
            return new Circle(center, radius);
        }

        /// <summary>
        /// Loft a circle contour to a concentric inner ring at <see cref="EndCapScale"/> radius, offset ± half a section
        /// in Z, then close the inner ring with a fan to its centre.  Without the fan the cap is an open frustum and
        /// every inner-ring edge reports as a hole (RPC1 368453/368452: holes:20 on a two-circle structure).
        /// </summary>
        private static void CapCircleEnd(BajajGeneratorMesh mesh, Polygon poly, int iPoly, bool closeUpper, double halfThickness, Circle sourceCircle)
        {
            double contourZ = mesh.ShapeZ[iPoly];
            double peakOffset = closeUpper ? halfThickness : -halfThickness;
            Vector2 center = sourceCircle.Center;
            double capRadius = sourceCircle.Radius * EndCapScale;

            List<PolygonIndex> contour = [];
            foreach (PolygonIndex idx in new PolygonVertexEnum(poly, iPoly))
                contour.Add(idx);
            if (contour.Count < 3 || capRadius <= 0)
                return;

            int n = contour.Count;
            int[] inner = new int[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 outerXY = mesh[contour[i]].Position.XY();
                Vector2 dir = outerXY - center;
                Vector2 innerXY;
                if (dir.Magnitude > Global.Epsilon)
                    innerXY = center + (dir / dir.Magnitude) * capRadius;
                else
                {
                    double angle = (2.0 * System.Math.PI * i) / n;
                    innerXY = center + new Vector2(System.Math.Cos(angle), System.Math.Sin(angle)) * capRadius;
                }

                MorphMeshVertex capVert = new(default(MedialAxisIndex), innerXY.ToVector3(contourZ + peakOffset));
                inner[i] = mesh.AddVertex(capVert);
            }

            int pole = mesh.AddVertex(new MorphMeshVertex(default(MedialAxisIndex), center.ToVector3(contourZ + peakOffset)));

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                int a = mesh[contour[i]].Index;
                int b = mesh[contour[next]].Index;
                int aPrime = inner[i];
                int bPrime = inner[next];

                if (mesh.Contains(a, aPrime) == false)
                    mesh.AddEdge(new MorphMeshEdge(EdgeType.CONTOUR_TO_MEDIALAXIS, a, aPrime));
                if (mesh.Contains(b, bPrime) == false)
                    mesh.AddEdge(new MorphMeshEdge(EdgeType.CONTOUR_TO_MEDIALAXIS, b, bPrime));
                if (mesh.Contains(aPrime, bPrime) == false)
                    mesh.AddEdge(new MorphMeshEdge(EdgeType.MEDIALAXIS, aPrime, bPrime));
                if (mesh.Contains(aPrime, pole) == false)
                    mesh.AddEdge(new MorphMeshEdge(EdgeType.MEDIALAXIS, aPrime, pole));

                AddCappedTriangle(mesh, [a, b, bPrime], closeUpper);
                AddCappedTriangle(mesh, [a, bPrime, aPrime], closeUpper);
                AddCappedTriangle(mesh, [aPrime, bPrime, pole], closeUpper);
            }
        }

        /// <summary>
        /// Loft an open-end polyline to a copy scaled 50% about the vertex-average centroid and offset
        /// ± half a section in Z. Called from <see cref="CapMeshEnd"/> so synapses and other polyline
        /// structures occupy their terminal section instead of collapsing onto the contour plane.
        /// A same-XY vertical strip has no area looking down Z; the inset copy gives curved polylines a visible band.
        /// </summary>
        private static void CapPolylineEnd(BajajGeneratorMesh mesh, Polyline line, int iPoly, bool closeUpper, double halfThickness)
        {
            double contourZ = mesh.ShapeZ[iPoly];
            double peakOffset = closeUpper ? halfThickness : -halfThickness;

            List<PolylineIndex> contour = [];
            foreach (PolylineIndex idx in new PolylineVertexEnum(line, iPoly))
                contour.Add(idx);
            if (contour.Count < 2)
                return;

            Vector2 centroid = Vector2.Zero;
            for (int i = 0; i < contour.Count; i++)
                centroid += mesh[contour[i]].Position.XY();
            centroid *= 1.0 / contour.Count;

            int[] extruded = new int[contour.Count];
            for (int i = 0; i < contour.Count; i++)
            {
                Vector2 xy = mesh[contour[i]].Position.XY();
                Vector2 scaled = centroid + ((xy - centroid) * EndCapScale);
                MorphMeshVertex capVert = new(default(MedialAxisIndex), scaled.ToVector3(contourZ + peakOffset));
                extruded[i] = mesh.AddVertex(capVert);
            }

            //A straight polyline scaled toward its own centroid stays on the same line, so the strip between the
            //contour and the cap copy is vertical and every triangle's normal has Z of about zero.  Orienting each
            //triangle from the sign of its own Z then flips neighbours at random and the cap's shared edges come
            //out inconsistent (RPC1 364267 and every other isolated gap junction).  The strip is built in one
            //consistent winding and flipped as a whole from the summed normal instead.
            List<int[]> strip = [];
            for (int i = 0; i + 1 < contour.Count; i++)
            {
                int a = mesh[contour[i]].Index;
                int b = mesh[contour[i + 1]].Index;
                int aPrime = extruded[i];
                int bPrime = extruded[i + 1];

                if (mesh.Contains(a, aPrime) == false)
                    mesh.AddEdge(new MorphMeshEdge(EdgeType.CONTOUR_TO_MEDIALAXIS, a, aPrime));
                if (mesh.Contains(b, bPrime) == false)
                    mesh.AddEdge(new MorphMeshEdge(EdgeType.CONTOUR_TO_MEDIALAXIS, b, bPrime));
                if (mesh.Contains(aPrime, bPrime) == false)
                    mesh.AddEdge(new MorphMeshEdge(EdgeType.MEDIALAXIS, aPrime, bPrime));

                strip.Add([a, b, bPrime]);
                strip.Add([a, bPrime, aPrime]);
            }

            AddCappedStrip(mesh, strip, closeUpper);
        }

        /// <summary>
        /// Adds triangles that already share a consistent winding, reversing all of them together when the summed
        /// normal points the wrong way for the cap's end.
        /// </summary>
        private static void AddCappedStrip(BajajGeneratorMesh mesh, List<int[]> strip, bool closeUpper)
        {
            double sumZ = 0;
            foreach (int[] verts in strip)
                sumZ += mesh.Normal(verts).Z;

            bool reverse = closeUpper ? sumZ > 0 : sumZ < 0;
            foreach (int[] verts in strip)
            {
                MorphMeshFace face = reverse ? new MorphMeshFace(verts.Reverse()) : new MorphMeshFace(verts);
                face.NormalIsKnownCorrect = true;
                mesh.AddFace(face);
            }
        }

        private static void AddCappedTriangle(BajajGeneratorMesh mesh, int[] verts, bool closeUpper)
        {
            Vector3 normal = mesh.Normal(verts);
            MorphMeshFace face = closeUpper
                ? normal.Z < 0 ? new MorphMeshFace(verts) : new MorphMeshFace(verts.Reverse())
                : normal.Z > 0 ? new MorphMeshFace(verts) : new MorphMeshFace(verts.Reverse());
            face.NormalIsKnownCorrect = true;
            mesh.AddFace(face);
        }

        /// <summary>
        /// Distance from an interior point to the nearest polygon boundary, counting interior rings.  Polygon.Distance
        /// only measures the exterior ring, which would report a point beside a hole as deep inside the shape.
        /// </summary>
        private static double BoundaryClearance(Polygon poly, Vector2 point)
        {
            double clearance = poly.Distance(point);

            foreach (Polygon inner in poly.InteriorPolygons)
                clearance = System.Math.Min(clearance, inner.Distance(point));

            return clearance;
        }

        /// <summary>
        /// Removes near-duplicate medial axis vertices, keeping only one representative per cluster
        /// within <paramref name="threshold"/> distance. This prevents the Delaunay triangulator from
        /// receiving nearly-coincident interior points that produce degenerate zero-length edges or
        /// trigger EdgesIntersectTriangulationException during the merge phase.
        /// </summary>
        private static MedialAxisVertex[] DeduplicateMedialAxisVerts(MedialAxisVertex[] verts, double threshold)
        {
            double threshSq = threshold * threshold;
            List<MedialAxisVertex> result = new(verts.Length);
            foreach (MedialAxisVertex v in verts)
            {
                bool isDuplicate = result.Any(kept =>
                {
                    double dx = kept.Key.X - v.Key.X;
                    double dy = kept.Key.Y - v.Key.Y;
                    return dx * dx + dy * dy <= threshSq;
                });
                if (!isDuplicate)
                    result.Add(v);
            }
            return [.. result];
        }

        private static TriangulationMesh<IVertex2D<MorphMeshVertex>> TriangulateCapWithMedialAxis(IVertex2D<MorphMeshVertex>[] verts, Polygon poly, int iPoly, TriangulationMesh<IVertex2D<MorphMeshVertex>>.ProgressUpdate OnProgress = null)
        {
            TriangulationMesh<IVertex2D<MorphMeshVertex>> triangulation = GenericDelaunayMeshGenerator2D<IVertex2D<MorphMeshVertex>>.TriangulateToMesh(verts, OnProgress);

            PolygonVertexEnum polyVertEnum = new(poly, iPoly);

            Dictionary<PolygonIndex, int> polyIndexToTriangulationIndex = [];

            //Ensure polygon ring is constrained in the mesh
            foreach (IVertex2D<MorphMeshVertex> vert in verts)
            {
                if (vert.Data.ShapeIndex is PolygonIndex polyIndex)
                {
                    polyIndexToTriangulationIndex.Add(polyIndex, vert.Index);
                }
            }

            HashSet<IEdge> constrainedEdges = [];
            Dictionary<PolygonIndex, Edge> edgeFacesToCheck = [];

            foreach (int iPolyVert in polyIndexToTriangulationIndex.Values)
            {
                IVertex2D<MorphMeshVertex> A = triangulation[iPolyVert];
                MorphMeshVertex MMV_A = A.Data;

                IVertex2D<MorphMeshVertex> B = triangulation[polyIndexToTriangulationIndex[(PolygonIndex)MMV_A.ShapeIndex.Next]];
                MorphMeshVertex MMV_B = B.Data; // polyIndexToTriangulationIndex[A.PolyIndex.Value.Next]];
                PolygonIndex polyIndex = (PolygonIndex)MMV_A.ShapeIndex;

                ConstrainedEdge edge = new(A.Index, B.Index);
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
            var EdgesToCheck = triangulation.Edges.Keys.Where(k =>
            {
                if (constrainedEdges.Contains(k))
                    return false;

                IVertex2D<MorphMeshVertex> A = triangulation[k.A];
                MorphMeshVertex MMV_A = A.Data;

                IVertex2D<MorphMeshVertex> B = triangulation[k.B];
                MorphMeshVertex MMV_B = B.Data; // polyIndexToTriangulationIndex[A.PolyIndex.Value.Next]];

                if (MMV_A.ShapeIndex is not PolygonIndex i_a)
                    return false;
                if (MMV_B.ShapeIndex is not PolygonIndex i_b)
                    return false;

                if (i_a.AreOnSameRing(i_b))
                    return true;

                //PointIndex polyIndex = MMV_A.PolyIndex.Value;

                return false;
            }).ToArray();


            foreach (IEdgeKey key in EdgesToCheck)
            {
                LineSegment line = triangulation.ToLineSegment(key);

                if (ShapeRelation.None == poly.GetRelation(line.Bisect()))
                {
                    triangulation.RemoveEdge(key);

                    OnProgress?.Invoke(triangulation);
                }
            }

            //If there are three constrained edges that form an interior polygon that is a triangle the face wont be removed.  This results
            //in a constrained edge with two faces.  For this case remove the interior face
            foreach (var innerPolyGroup in edgeFacesToCheck.GroupBy(i => i.Key.InnerShapeIndex))
            {
                Polygon innerPolygon = poly.InteriorPolygons[innerPolyGroup.Key.Value];
                Vector2 Centroid = innerPolygon.Centroid;

                //Figure out the inner polygon vertex numbers in the mesh
                SortedSet<int> innerPolyTriangulationVertIndicies = [.. innerPolyGroup.SelectMany(g => new int[] { g.Value.A, g.Value.B })];
                IFace[] allFaces = [.. innerPolyGroup.SelectMany(g => g.Value.Faces).Distinct()];

                IFace[] InteriorFaces = [.. allFaces.Where(f => f.iVerts.All(iVert => innerPolyTriangulationVertIndicies.Contains(iVert)))];

                //Should only ever be one interior face for a 3 vert interior polygon, unless someone adds interior polygons to interior polygons later <shudder/>
                foreach (IFace f in InteriorFaces)
                {
                    triangulation.RemoveFace(f);

                    OnProgress?.Invoke(triangulation);
                }
            }

            return triangulation;
        }
    }
}
