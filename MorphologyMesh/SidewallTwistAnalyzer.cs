using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace MorphologyMesh
{
    /// <summary>
    /// Counts XY-crossing Z-chords and Next+Previous fans on a generated slice mesh.
    /// Used by MorphologyMeshTest to diagnose helical / hourglass sidewalls without a DAE export.
    /// </summary>
    public readonly struct SidewallTwistReport
    {
        /// <summary>Z-crossing edges whose endpoints both lie on input contours.</summary>
        public int SliceChordCount { get; init; }

        /// <summary>Faces whose vertices do not all share one Z, i.e. the wall between slices.</summary>
        public int SidewallFaceCount { get; init; }

        /// <summary>Pairs of those chords whose XY projections cross except at a shared vertex.</summary>
        public int CrossingChordPairs { get; init; }

        /// <summary>
        /// Corresponding pairs that received both a Next and a Previous fan, the hourglass tiling
        /// CompleteCorrespondingVertexFaces can emit when FLIPPED_DIRECTION is treated as valid.
        /// </summary>
        public int FlippedDirectionFans { get; init; }

        /// <summary>Contour-to-contour chords whose endpoint tangents disagree by more than 90°.</summary>
        public int OrientationMismatchChords { get; init; }

        public override string ToString() =>
            $"SliceChords={SliceChordCount} SidewallFaces={SidewallFaceCount} CrossingPairs={CrossingChordPairs} FlippedFans={FlippedDirectionFans} OrientationMismatches={OrientationMismatchChords}";
    }

    /// <summary>
    /// Headless twist metric over BajajMeshGenerator.GenerateFaces output. Project each contour-to-contour
    /// Z-chord into XY and count proper intersections; also count corresponding dual-direction fans.
    /// </summary>
    public static class SidewallTwistAnalyzer
    {
        const double ZEpsilon = 1e-6;

        /// <summary>
        /// Builds a <see cref="SidewallTwistReport"/> for <paramref name="mesh"/> after face generation.
        /// Callers are MorphologyMeshTest cases (stacked squares, cached cell pairs) that assert CrossingChordPairs == 0.
        /// </summary>
        public static SidewallTwistReport Analyze(MorphRenderMesh mesh)
        {
            List<(int A, int B, Vector2 Axy, Vector2 Bxy)> chords = [];
            HashSet<(int, int)> seen = [];
            int orientationMismatches = 0;

            void TryAddChord(int a, int b)
            {
                MorphMeshVertex vA = mesh.GetVertex(a);
                MorphMeshVertex vB = mesh.GetVertex(b);
                if (Math.Abs(vA.Position.Z - vB.Position.Z) <= ZEpsilon)
                    return;

                int lo = Math.Min(a, b);
                int hi = Math.Max(a, b);
                if (!seen.Add((lo, hi)))
                    return;

                Vector2 axy = vA.Position.XY();
                Vector2 bxy = vB.Position.XY();
                chords.Add((a, b, axy, bxy));

                if (vA.ShapeIndex is PolygonIndex iA && vB.ShapeIndex is PolygonIndex iB
                    && !EdgeTypeExtensions.OrientationsAreMatched(iA, iB, mesh.Shapes))
                {
                    orientationMismatches++;
                }
            }

            foreach (MorphMeshEdge edge in mesh.MorphEdges)
                TryAddChord(edge.A, edge.B);

            int sidewallFaces = 0;
            foreach (IFace face in mesh.Faces)
            {
                bool mixedZ = false;
                ImmutableArray<int> iVerts = face.iVerts;
                for (int i = 0; i < iVerts.Length; i++)
                {
                    int j = (i + 1) % iVerts.Length;
                    MorphMeshVertex vI = mesh.GetVertex(iVerts[i]);
                    MorphMeshVertex vJ = mesh.GetVertex(iVerts[j]);
                    if (Math.Abs(vI.Position.Z - vJ.Position.Z) > ZEpsilon)
                        mixedZ = true;
                    TryAddChord(iVerts[i], iVerts[j]);
                }

                if (mixedZ)
                    sidewallFaces++;
            }

            int crossingPairs = CountCrossingPairs(chords);
            int flippedFans = CountFlippedDirectionFans(mesh);

            return new SidewallTwistReport
            {
                SliceChordCount = chords.Count,
                SidewallFaceCount = sidewallFaces,
                CrossingChordPairs = crossingPairs,
                FlippedDirectionFans = flippedFans,
                OrientationMismatchChords = orientationMismatches
            };
        }

        static int CountCrossingPairs(List<(int A, int B, Vector2 Axy, Vector2 Bxy)> chords)
        {
            int crossings = 0;
            for (int i = 0; i < chords.Count; i++)
            {
                var left = chords[i];
                if (left.Axy == left.Bxy)
                    continue;

                LineSegment leftSeg = new(left.Axy, left.Bxy);
                for (int j = i + 1; j < chords.Count; j++)
                {
                    var right = chords[j];
                    if (SharesVertex(left, right))
                        continue;
                    if (right.Axy == right.Bxy)
                        continue;

                    LineSegment rightSeg = new(right.Axy, right.Bxy);
                    if (leftSeg.Intersects(rightSeg, EndpointsOnRingDoNotIntersect: true))
                        crossings++;
                }
            }

            return crossings;
        }

        static bool SharesVertex(
            (int A, int B, Vector2 Axy, Vector2 Bxy) left,
            (int A, int B, Vector2 Axy, Vector2 Bxy) right) =>
            left.A == right.A || left.A == right.B || left.B == right.A || left.B == right.B;

        static int CountFlippedDirectionFans(MorphRenderMesh mesh)
        {
            int fans = 0;
            foreach (MorphMeshVertex v in mesh.MorphVerticies)
            {
                if (!v.Corresponding.HasValue)
                    continue;
                if (v.Index > v.Corresponding.Value)
                    continue;
                if (v.ShapeIndex is not PolygonIndex a)
                    continue;

                MorphMeshVertex partner = mesh.GetVertex(v.Corresponding.Value);
                if (partner.ShapeIndex is not PolygonIndex b)
                    continue;

                bool nextNext = mesh.Contains(a.Next, b.Next);
                bool nextPrev = mesh.Contains(a.Next, b.Previous);
                bool prevPrev = mesh.Contains(a.Previous, b.Previous);
                bool prevNext = mesh.Contains(a.Previous, b.Next);

                if ((nextNext && nextPrev) || (prevPrev && prevNext))
                    fans++;
            }

            return fans;
        }
    }
}
