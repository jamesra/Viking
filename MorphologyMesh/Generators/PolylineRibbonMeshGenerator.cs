using Geometry;
using Geometry.Meshing;
using System.Collections.Generic;
using System.Diagnostics;

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

            return EnforceTwoFaceMinimumForPolylinePairs(mesh);
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
