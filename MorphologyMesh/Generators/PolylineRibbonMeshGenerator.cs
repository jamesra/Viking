using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MorphologyMesh
{
    /// <summary>
    /// Tiles a slice whose shapes are all open polylines (gap junctions, synaptic rafts, adherens ribbons) into a
    /// ruled ribbon between sections.
    ///
    /// This is deliberately not the polygon Bajaj path.  An open curve has no interior, so none of the polygon
    /// machinery applies: there are no untiled regions to pair, no medial axis to close them with, no
    /// inside/outside test for chord midpoints, and no outward orientation to enforce.  Running that code on a
    /// polyline slice produces nonsense at best and exceptions at worst, so a polygon anywhere in the slice must be
    /// routed to <see cref="BajajMeshGenerator.GenerateFaces"/> instead (SliceGraph already makes polylines on a
    /// polygon slice correspondence-only).
    ///
    /// What the ribbon path does share with polygons is the vertex/contour plumbing: constrained Delaunay in XY,
    /// corresponding-vertex faces where the two curves cross, optimal-tiling-vertex slice chords, and the face
    /// generation that fills quads between chords.  The polyline-aware pieces of those routines are selected by
    /// <see cref="PolylineIndex"/> at runtime.
    /// </summary>
    public static class PolylineRibbonMeshGenerator
    {
        /// <summary>
        /// Ribbon tiling needs more than one contour segment on an open curve.  Two-point OPENCURVEs (RPC1
        /// locations 381857/381868) leave isolated edges after tiling; a 2-point line is split at 1/3 and 2/3 so both
        /// sides get interior verticies that OTV can pair, then any remaining shortfall bisects the longest segment.
        /// Endpoints are never moved.
        /// </summary>
        public const int MinOpenPolylinePoints = 4;

        /// <summary>
        /// Prepare an open polyline (POLYLINE / OPENCURVE) for ribbon tiling: densify it to at least
        /// <see cref="MinOpenPolylinePoints"/> verticies.  Closed rings (first == last) and lines already long enough
        /// are returned unchanged.
        ///
        /// This runs once per morphology node when the shape cache is built, never per slice.  A node is shared by
        /// the slice above and the slice below it, and the composite welds those two meshes on (node, vertex index),
        /// so both slices have to be built from identical verticies.
        /// </summary>
        public static Polyline PrepareOpenPolyline(Polyline line, int minPoints = MinOpenPolylinePoints)
        {
            if (line is null || line.PointCount < 2 || line.PointCount >= minPoints)
                return line;

            List<Vector2> pts = new(minPoints);
            foreach (PolylineIndex idx in new PolylineVertexEnum(line, 0))
                pts.Add(new Vector2(line[idx]));

            if (pts[0] == pts[^1])
                return line;

            if (pts.Count == 2)
            {
                Vector2 a = pts[0];
                Vector2 b = pts[1];
                pts.Insert(1, Lerp(a, b, 1.0 / 3.0));
                pts.Insert(2, Lerp(a, b, 2.0 / 3.0));
            }

            while (pts.Count < minPoints)
            {
                int bestSeg = -1;
                double bestLenSq = 0;
                for (int s = 0; s < pts.Count - 1; s++)
                {
                    Vector2 d = pts[s + 1] - pts[s];
                    double lenSq = (d.X * d.X) + (d.Y * d.Y);
                    if (lenSq > bestLenSq)
                    {
                        bestLenSq = lenSq;
                        bestSeg = s;
                    }
                }

                if (bestSeg < 0)
                    break;

                pts.Insert(bestSeg + 1, Lerp(pts[bestSeg], pts[bestSeg + 1], 0.5));
            }

            return new Polyline([.. pts], line.AllowsSelfIntersection);
        }

        private static Vector2 Lerp(Vector2 a, Vector2 b, double t) =>
            new(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));

        /// <summary>
        /// Build the ribbon faces for <paramref name="mesh"/>.  The caller owns end caps, virtual-overlap restore,
        /// normals, and manifold validation via <see cref="BajajMeshGenerator.FinishSliceMesh"/>.
        /// </summary>
        /// <returns>Number of cross-band polyline pairs that were left with a single sliver triangle and had it removed.</returns>
        internal static int GenerateRibbonFaces(BajajGeneratorMesh mesh)
        {
            Debug.Assert(mesh.HasPolygonShapes == false, "Polyline ribbon generator received a polygon; route through the Bajaj polygon path.");

            BajajMeshGenerator.AddDelaunayEdges(mesh);

            mesh.RemoveInvalidEdges();

            BajajMeshGenerator.CompleteCorrespondingVertexFaces(mesh);

            BajajMeshGenerator.FirstPassSliceChordGeneration(mesh, mesh.ShapeZ);

            BajajMeshGenerator.FirstPassFaceGeneration(mesh);

            return CleanRibbonFaces(mesh);
        }

        /// <summary>
        /// The ribbon-only cleanup that follows face generation: unfold segments carrying a second sheet, then drop
        /// lone sliver triangles.  Public so the BajajTest harness can run the same steps stage by stage.
        /// </summary>
        /// <returns>Number of cross-band polyline pairs that were left with a single sliver triangle and had it removed.</returns>
        public static int CleanRibbonFaces(BajajGeneratorMesh mesh)
        {
            if (TryRebuildTwoPolylineRibbon(mesh))
                return 0;

            RemoveFoldedRibbonFaces(mesh);

            return EnforceTwoFaceMinimumForPolylinePairs(mesh);
        }

        /// <summary>
        /// A slice holding exactly one open polyline per section is a ruled strip and nothing else, so it is built
        /// directly by marching both lines in arc-length order instead of trusting the Delaunay/chord faces.  Those
        /// passes work from distances, which say nothing useful when the two lines are short compared with their
        /// separation (RPC1 365031/365032: 50 nm lines 14 µm apart), and they then pair the lines in opposite
        /// directions on either side, leaving a bow-tie that no per-segment cleanup can unfold.  Forks and mixed
        /// slices keep the heuristic cleanup below.
        /// </summary>
        private static bool TryRebuildTwoPolylineRibbon(BajajGeneratorMesh mesh)
        {
            if (mesh.Shapes.Length != 2
                || mesh.Shapes[0] is not Polyline lineA
                || mesh.Shapes[1] is not Polyline lineB
                || mesh.IsUpperShape[0] == mesh.IsUpperShape[1])
            {
                return false;
            }

            int[] a = ShapeVertexIndices(mesh, 0, lineA.PointCount);
            int[] b = ShapeVertexIndices(mesh, 1, lineB.PointCount);
            if (a is null || b is null)
                return false;

            bool reversed = Vector2.Distance(Start(lineA), Start(lineB)) + Vector2.Distance(End(lineA), End(lineB))
                          > Vector2.Distance(Start(lineA), End(lineB)) + Vector2.Distance(End(lineA), Start(lineB));
            double[] fa = ArcLengthFractions(lineA);
            double[] fb = ArcLengthFractions(lineB);
            if (reversed)
            {
                Array.Reverse(b);
                fb = fb.Select(f => 1.0 - f).Reverse().ToArray();
            }

            foreach (IFace face in mesh.Faces.ToArray())
                mesh.RemoveFace(face);

            foreach (MorphMeshEdge edge in mesh.MorphEdges.Where(e => e.Type != EdgeType.CONTOUR).ToArray())
                mesh.RemoveEdge(edge);

            int i = 0, j = 0;
            while (i < a.Length - 1 || j < b.Length - 1)
            {
                bool advanceA = j == b.Length - 1 || (i < a.Length - 1 && fa[i + 1] <= fb[j + 1]);
                if (advanceA)
                {
                    mesh.AddFace(new MorphMeshFace(a[i], a[i + 1], b[j]));
                    i++;
                }
                else
                {
                    mesh.AddFace(new MorphMeshFace(a[i], b[j + 1], b[j]));
                    j++;
                }
            }

            //The classifier would mark strip rungs INVALID wherever the two lines overlap in XY (the usual case for
            //the same junction traced on adjacent sections); a rung of a ruled strip is a surface edge by construction.
            foreach (MorphMeshEdge edge in mesh.MorphEdges.Where(e => e.Type == EdgeType.UNKNOWN))
                edge.Type = mesh[edge.A].Position.XY() == mesh[edge.B].Position.XY() ? EdgeType.CORRESPONDING : EdgeType.SURFACE;

            Trace.WriteLine($"Mesh {mesh}: rebuilt two-polyline ribbon as a ruled strip of {mesh.Faces.Count} faces{(reversed ? " (lines run in opposite directions)" : "")}.");
            return true;
        }

        private static int[] ShapeVertexIndices(BajajGeneratorMesh mesh, int shape, int pointCount)
        {
            int[] indices = new int[pointCount];
            Array.Fill(indices, -1);
            foreach (MorphMeshVertex vertex in mesh.MorphVerticies)
            {
                if (vertex.ShapeIndex is PolylineIndex index && index.ShapeIndex == shape && index.VertexIndex < pointCount)
                    indices[index.VertexIndex] = vertex.Index;
            }

            return indices.Any(v => v < 0) ? null : indices;
        }

        /// <summary>
        /// A ribbon is a single sheet, so within one slice each contour segment of a polyline can carry only one face
        /// toward any given neighbouring shape.  The Delaunay pass has no inside/outside to reject with on open
        /// curves, so it fills the whole convex hull of the two lines; when one line runs past the end of the other
        /// (RPC1 gap junction 52432, locations 368195/368197) the far endpoint fans across every segment of the other
        /// line from the wrong side.  That second sheet folds the ribbon over on itself: the contour segments end up
        /// with two faces here and three once the neighbouring slice adds its own, and the fan's outer edge shows as
        /// a slit in the assembled surface.  Keep the smallest-perimeter face per segment and neighbour, which is the
        /// one whose apex is the nearby part of the other line, and drop the rest.
        ///
        /// Grouped by neighbouring shape rather than simply "one face per segment" because a fork legitimately puts
        /// faces to two different branches on either side of the same trunk segment.
        /// </summary>
        private static void RemoveFoldedRibbonFaces(BajajGeneratorMesh mesh)
        {
            int removed = 0;
            foreach (MorphMeshEdge contour in mesh.MorphEdges.Where(e => e.Type == EdgeType.CONTOUR && e.Faces.Count > 1).ToArray())
            {
                if (mesh[contour.A].ShapeIndex is not PolylineIndex)
                    continue;

                Dictionary<int, List<IFace>> facesByNeighbour = [];
                foreach (IFace face in contour.Faces)
                {
                    int apexShape = -1;
                    foreach (int iVert in face.iVerts)
                    {
                        if (iVert == contour.A || iVert == contour.B)
                            continue;

                        IShapeIndex apex = mesh[iVert].ShapeIndex;
                        apexShape = apex?.ShapeIndex ?? -1;
                    }

                    if (facesByNeighbour.TryGetValue(apexShape, out List<IFace> faces) == false)
                        facesByNeighbour[apexShape] = faces = [];

                    faces.Add(face);
                }

                foreach (KeyValuePair<int, List<IFace>> neighbourFaces in facesByNeighbour)
                {
                    List<IFace> faces = neighbourFaces.Value;
                    if (faces.Count < 2)
                        continue;

                    IFace keep = ChooseRibbonFace(mesh, contour, neighbourFaces.Key, faces);
                    foreach (IFace face in faces)
                    {
                        if (ReferenceEquals(face, keep))
                            continue;

                        mesh.RemoveFace(face);
                        removed++;
                    }
                }
            }

            if (removed == 0)
                return;

            //The dropped faces leave their non-contour edges with nothing attached; those would otherwise be reported
            //as isolated edges and confuse later passes.
            foreach (MorphMeshEdge edge in mesh.MorphEdges.Where(e => e.Type != EdgeType.CONTOUR && e.Faces.Count == 0).ToArray())
                mesh.RemoveEdge(edge);

            Trace.WriteLine($"Mesh {mesh}: removed {removed} folded ribbon face(s) that put a second sheet on a polyline segment.");
        }

        /// <summary>
        /// Picks the face a contour segment keeps toward one neighbouring polyline.  A ruled ribbon maps arc-length
        /// along one line monotonically onto the other, so the right apex is the neighbour vertex whose arc-length
        /// fraction matches the segment's (after deciding whether the lines run the same way from their endpoint
        /// distances).  The smallest perimeter used to decide instead, but two short lines far apart (RPC1
        /// 365031/365032: 50 nm lines 14 µm apart) have one endpoint nearer to every segment of the other line, so
        /// the fan from that endpoint won every segment and the true ribbon triangles were the ones removed.
        /// Perimeter still breaks ties and covers apexes that are not on a polyline.
        /// </summary>
        private static IFace ChooseRibbonFace(BajajGeneratorMesh mesh, MorphMeshEdge contour, int neighbourShape, List<IFace> faces)
        {
            if (neighbourShape < 0
                || mesh[contour.A].ShapeIndex is not PolylineIndex a
                || mesh.Shapes[a.ShapeIndex] is not Polyline line
                || mesh.Shapes[neighbourShape] is not Polyline neighbour)
            {
                return faces.MinBy(f => Perimeter(mesh, f));
            }

            double[] lineFractions = ArcLengthFractions(line);
            double[] neighbourFractions = ArcLengthFractions(neighbour);
            bool reversed = Vector2.Distance(Start(line), Start(neighbour)) + Vector2.Distance(End(line), End(neighbour))
                          > Vector2.Distance(Start(line), End(neighbour)) + Vector2.Distance(End(line), Start(neighbour));

            int iB = ((PolylineIndex)mesh[contour.B].ShapeIndex).VertexIndex;
            double segmentFraction = (lineFractions[a.VertexIndex] + lineFractions[iB]) / 2.0;

            double Score(IFace face)
            {
                foreach (int iVert in face.iVerts)
                {
                    if (iVert == contour.A || iVert == contour.B)
                        continue;

                    if (mesh[iVert].ShapeIndex is PolylineIndex apex && apex.ShapeIndex == neighbourShape)
                    {
                        double s = neighbourFractions[apex.VertexIndex];
                        return System.Math.Abs((reversed ? 1.0 - s : s) - segmentFraction);
                    }
                }

                return double.MaxValue;
            }

            return faces.OrderBy(Score).ThenBy(f => Perimeter(mesh, f)).First();
        }

        private static Vector2 Start(Polyline line) => new(line.Points[0].X, line.Points[0].Y);

        private static Vector2 End(Polyline line) => new(line.Points[^1].X, line.Points[^1].Y);

        private static double[] ArcLengthFractions(Polyline line)
        {
            double[] fractions = new double[line.PointCount];
            double total = 0;
            for (int i = 1; i < line.PointCount; i++)
            {
                total += Vector2.Distance(new Vector2(line.Points[i - 1].X, line.Points[i - 1].Y), new Vector2(line.Points[i].X, line.Points[i].Y));
                fractions[i] = total;
            }

            if (total > 0)
            {
                for (int i = 0; i < fractions.Length; i++)
                    fractions[i] /= total;
            }

            return fractions;
        }

        private static double Perimeter(BajajGeneratorMesh mesh, IFace face)
        {
            double total = 0;
            int n = face.iVerts.Length;
            for (int i = 0; i < n; i++)
                total += Vector3.Distance(mesh[face.iVerts[i]].Position, mesh[face.iVerts[(i + 1) % n]].Position);

            return total;
        }

        /// <summary>
        /// Two polylines on different sections should share a full quad or nothing at all.  A lone triangle is a
        /// sliver: it puts a single face where the surface needs two, and it leaves the odd vertex out with a
        /// boundary edge that reads as a hole.  Remove those triangles so the result is a clean gap.
        ///
        /// Polyline-only on purpose.  <c>TryClosingUntiledRegion</c> legitimately closes a three-vertex region with
        /// one triangle, and that path only runs on the polygon branch, so a backstop that also swept polygon pairs
        /// would delete correct region-closing output and reopen the hole it had just filled.
        ///
        /// This removes rather than trying to complete the quad.  FirstPassFaceGeneration has already run by this
        /// point and its whole job is to add the second face wherever the existing chords admit one, so a pair still
        /// holding one face is a pair whose complementary triangle was rejected.  Adding it here anyway would mean
        /// re-adding a face the chord tests already refused, which trades a gap for crossing edges.
        /// </summary>
        /// <returns>How many cross-band polyline pairs were left with exactly one face.</returns>
        private static int EnforceTwoFaceMinimumForPolylinePairs(BajajGeneratorMesh mesh)
        {
            //Group faces by the cross-band polyline pair they join.  A face touching three shapes is not this
            //pass's business.
            Dictionary<(int Lower, int Upper), List<IFace>> facesByPair = [];

            foreach (IFace face in mesh.Faces)
            {
                if (TryGetCrossBandPolylinePair(mesh, face, out (int Lower, int Upper) pair) == false)
                    continue;

                if (facesByPair.TryGetValue(pair, out List<IFace> faces) == false)
                    facesByPair[pair] = faces = [];

                faces.Add(face);
            }

            int slivers = 0;
            foreach (KeyValuePair<(int Lower, int Upper), List<IFace>> kvp in facesByPair)
            {
                if (kvp.Value.Count != 1)
                    continue;

                slivers++;
                mesh.RemoveFace(kvp.Value[0]);
                Trace.WriteLine($"Mesh {mesh}: removed the single triangle joining polyline shapes {kvp.Key.Lower} and {kvp.Key.Upper}; a cross-section polyline pair needs two faces or none.");
            }

            return slivers;
        }

        /// <summary>
        /// True when every vertex of the face belongs to one of exactly two polyline shapes that sit on opposite
        /// bands, and both bands are represented.
        /// </summary>
        private static bool TryGetCrossBandPolylinePair(BajajGeneratorMesh mesh, IFace face, out (int Lower, int Upper) pair)
        {
            pair = default;

            int lower = -1;
            int upper = -1;

            foreach (int iVert in face.iVerts)
            {
                IShapeIndex index = mesh[iVert].ShapeIndex;
                if (index is null)
                    return false;

                int iShape = index.ShapeIndex;
                if (mesh.Shapes[iShape] is not Polyline)
                    return false;

                ref int side = ref mesh.IsUpperShape[iShape] ? ref upper : ref lower;
                if (side >= 0 && side != iShape)
                    return false;

                side = iShape;
            }

            if (lower < 0 || upper < 0)
                return false;

            pair = (lower, upper);
            return true;
        }
    }
}
