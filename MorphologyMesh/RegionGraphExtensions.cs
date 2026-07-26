using Geometry;
using Geometry.Meshing;
using System.Collections.Generic;
using System.Diagnostics;
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
                mesh.GenerationHadErrors = true;

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
            List<int> vertsWithoutFaces = [.. region.Verticies.Where(v => mesh[v].Edges.SelectMany(e => mesh[e].Faces).Count() == 0)];

            //Candidates are selected and then added under one suite of tests.  These used to differ: the table was
            //built with Theorem2 and Theorem4 but added with LineOrientation, so the region could be judged
            //closeable against criteria that were never applied when the chords were actually created, and the
            //chords could then fail to be added, leaving the region open.
            const SliceChordTestType RegionChordTests = SliceChordTestType.Correspondance
                                                      | SliceChordTestType.ChordIntersection
                                                      | SliceChordTestType.Theorem2
                                                      | SliceChordTestType.Theorem4
                                                      | SliceChordTestType.LineOrientation;

            BajajMeshGenerator.CreateOptimalTilingVertexTable(vertsWithoutFaces.Select(v => mesh[v].ShapeIndex),
                                                              mesh.Shapes, mesh.IsUpperShape,
                                                              RegionChordTests,
                                                              out OTVTable OTVTable, ref rTree);

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
        /// <returns>True if the region was closed (or required no work); false if it was skipped because the triangulation failed on degenerate geometry.</returns>
        private static bool TryClosingUntiledRegion(BajajGeneratorMesh mesh, MorphMeshRegion region, SliceChordRTree rTree, TriangulationMesh<IVertex2D<int>>.ProgressUpdate OnProgress = null)
        {
            if (region.Verticies.Length == 3)
            {
                MorphMeshFace face = new(region.Verticies);
                mesh.AddFace(face);
                return true;
            }
            else if (region.Verticies.Length == 4)
            {
                MorphMeshFace face = new(region.Verticies);
                //Split face will add the face too
                mesh.SplitFace(face);
                return true;
            }

            GridPolygon regionPolygon = region.Polygon;
            GridVector2 regionPolygonCenter = regionPolygon.Centroid;
            GridPolygon centeredRegionPolygon = regionPolygon.Translate(-regionPolygonCenter);

            //centeredRegionPolygon.IsConvex();

            var MedialAxis = MedialAxisFinder.ApproximateMedialAxis(centeredRegionPolygon);
            MedialAxisVertex[] NewVerts = [.. MedialAxis.Nodes.Values];

            System.Diagnostics.Debug.Assert(NewVerts.All(v => centeredRegionPolygon.GetRelation(v.Key) == ShapeRelation.CONTAINED), "Interior points must be inside Face");

            //TODO: Split any edges with an existing face into two parts so we can better merge the medial axis with the existing shape

            if (NewVerts.Length == 0)
            {
                //The medial axis approximation produced no interior points, so this region cannot be tiled.
                //Report it as unclosed so the caller tracks the open hole rather than silently dropping it.
                Trace.WriteLine($"Skipping untiled region {region} in mesh {mesh}: medial axis produced no interior points.");
                return false;
            }

            //Fallback Z (flat mid-plane) used only when the region perimeter carries no Z information.
            double fallbackZ = mesh.SliceCenterZ;

            //Interpolate each medial-axis vertex's Z from the region perimeter (Edwards 2011) instead of
            //flattening the whole skeleton to the mid-plane.  Perimeter positions are in absolute coordinates,
            //while the medial-axis vertices live in the centered space used for triangulation, so compare them
            //in the same (centered) frame.
            MorphMeshVertex[] perimeter = region.RegionPerimeter;
            GridVector2[] perimeterCenteredXY = [.. perimeter.Select(v => v.Position.XY() - regionPolygonCenter)];
            double[] perimeterZ = [.. perimeter.Select(v => v.Position.Z)];

            //Build the medial-axis verticies but DO NOT add them to the mesh yet.  Pre-assign the indicies they
            //will receive so the triangulation can map its output back to these verticies, then commit them to
            //the mesh only if triangulation succeeds.  This prevents orphan verticies when triangulation fails
            //on degenerate input.
            int predictedStartIndex = mesh.Verticies.Count;
            var MedialAxisMeshVerts = NewVerts.Select((mv, k) =>
            {
                double vertZ = InterpolateZFromPerimeter(mv.Key, perimeterCenteredXY, perimeterZ, fallbackZ);
                MorphMeshVertex vtx = new(new MedialAxisIndex(MedialAxis, mv), (mv.Key + regionPolygonCenter).ToGridVector3(vertZ));
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

            GridVector2[] regionVertPositions = region.VertPositions.Select(v => v.XY()).ToArray();
            for(int i = 0; i < region.Verticies.Length; i++)
            {
                VertexLookup.Add(regionVertPositions[i], region.Verticies[i]);
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

            foreach (var polyFace in polyMesh.Faces)
            {
                var MeshFaceVerts = polyFace.iVerts.Select(i => polyMesh[i].Data).ToArray();

                MorphMeshFace newFace = new(MeshFaceVerts);

                if (mesh.FaceHasCCWWinding(newFace))
                    newFace = new MorphMeshFace(MeshFaceVerts.Reverse());

                newFace.NormalIsKnownCorrect = true;
                mesh.AddFace(newFace);
            }

            return true;
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
        private static double InterpolateZFromPerimeter(GridVector2 point, GridVector2[] perimeterXY, double[] perimeterZ, double fallbackZ)
        {
            if (perimeterXY.Length == 0)
                return fallbackZ;

            double weightSum = 0;
            double weightedZSum = 0;

            for (int i = 0; i < perimeterXY.Length; i++)
            {
                double distSq = GridVector2.DistanceSquared(point, perimeterXY[i]);

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

            for (int iPoly = 0; iPoly < mesh.Shapes.Length; iPoly++)
            {
                bool ClosePoly = CloseUpper ? mesh.IsUpperShape[iPoly] : !mesh.IsUpperShape[iPoly];
                if (ClosePoly == false)
                    continue;

                if (mesh.Shapes[iPoly] is GridPolygon poly)
                {
                    GridVector2 polyCenter = poly.Centroid;
                    GridPolygon centeredPolygon = poly.Translate(-polyCenter);

                    var MedialAxis = MedialAxisFinder.ApproximateMedialAxis(centeredPolygon);
                    MedialAxisVertex[] NewVerts = DeduplicateMedialAxisVerts([.. MedialAxis.Nodes.Values], (double)Global.Epsilon * 100.0);
                    System.Diagnostics.Debug.Assert(NewVerts.All(v => centeredPolygon.Contains(v.Key)), "Interior points must be inside Face");

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
                    int predictedStartIndex = mesh.Verticies.Count;
                    var MedialAxisMeshVerts = NewVerts.Select((mv, k) =>
                    {
                        double depthFraction = maxClearance > 0 ? clearance[k] / maxClearance : 0;
                        double vertZ = contourZ + (peakOffset * depthFraction);
                        MorphMeshVertex vtx = new(new MedialAxisIndex(MedialAxis, mv), (mv.Key + polyCenter).ToGridVector3(vertZ));
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
                        mesh.GenerationHadErrors = true;
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

                        GridVector3 normal = mesh.Normal(MeshFaceVerts);
                        MorphMeshFace newFace = CloseUpper
                            ? normal.Z < 0 ? new MorphMeshFace(MeshFaceVerts) : new MorphMeshFace(MeshFaceVerts.Reverse())
                            : normal.Z > 0 ? new MorphMeshFace(MeshFaceVerts) : new MorphMeshFace(MeshFaceVerts.Reverse());


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
                else
                {
                    //Only polygons can be capped.  A shape reaching here leaves an open end in the surface, so make
                    //that visible instead of silently producing a mesh with a hole where the cap should be.
                    Trace.WriteLine($"Cannot cap {mesh.Shapes[iPoly].GetType().Name} at shape index {iPoly} of mesh {mesh}.  This end of the mesh is left open.");
                }
            }
        }

        /// <summary>
        /// Distance from an interior point to the nearest polygon boundary, counting interior rings.  GridPolygon.Distance
        /// only measures the exterior ring, which would report a point beside a hole as deep inside the shape.
        /// </summary>
        private static double BoundaryClearance(GridPolygon poly, GridVector2 point)
        {
            double clearance = poly.Distance(point);

            foreach (GridPolygon inner in poly.InteriorPolygons)
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

        private static TriangulationMesh<IVertex2D<MorphMeshVertex>> TriangulateCapWithMedialAxis(IVertex2D<MorphMeshVertex>[] verts, GridPolygon poly, int iPoly, TriangulationMesh<IVertex2D<MorphMeshVertex>>.ProgressUpdate OnProgress = null)
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
                GridLineSegment line = triangulation.ToGridLineSegment(key);

                if (ShapeRelation.NONE == poly.GetRelation(line.Bisect()))
                {
                    triangulation.RemoveEdge(key);

                    OnProgress?.Invoke(triangulation);
                }
            }

            //If there are three constrained edges that form an interior polygon that is a triangle the face wont be removed.  This results
            //in a constrained edge with two faces.  For this case remove the interior face
            foreach (var innerPolyGroup in edgeFacesToCheck.GroupBy(i => i.Key.iInnerPoly))
            {
                GridPolygon innerPolygon = poly.InteriorPolygons[innerPolyGroup.Key.Value];
                GridVector2 Centroid = innerPolygon.Centroid;

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
