using Geometry;
using System.Collections.Generic;

namespace MorphologyMesh
{
    /// <summary>
    /// Decides which verticies of two open polylines on adjacent sections may be joined by a chord.
    ///
    /// When two polylines cross in XY a corresponding vertex is inserted on both at the crossing, and the ribbon
    /// between them becomes a twisted sheet: the part of one line before the crossing is tiled against the part of
    /// the other line before it (or after it, when the lines were traced in opposite directions), and the parts
    /// after the crossing are tiled together.  In XY projection those two spans overlap near the crossing, so a
    /// chord from one line's "before" to the other line's "after" looks perfectly reasonable to a Delaunay
    /// triangulation and to the polygon-oriented edge tests, yet it belongs to neither span.  Faces built on such a
    /// chord run through the twist, give the contour segments beside the crossing a second face, and leave the
    /// correct span with a hole (RPC1 gap junction 52432, locations 368197/368198 after process smoothing).
    ///
    /// Corresponding verticies are recognised as verticies with identical XY on both lines, which is how
    /// correspondence inserts them.  A chord that starts or ends on a corresponding vertex sits on the seam between
    /// spans and is allowed in either.
    /// </summary>
    public static class PolylineSpanPairing
    {
        /// <summary>
        /// True when a chord from <paramref name="a"/>'s vertex <paramref name="iA"/> to <paramref name="b"/>'s
        /// vertex <paramref name="iB"/> stays within a single span of the ribbon between the two polylines.  Lines
        /// that never cross have a single span, so every chord qualifies.
        /// </summary>
        public static bool ChordStaysInSpan(Polyline a, int iA, Polyline b, int iB)
        {
            Vector2[] ptsA = (Vector2[])a;
            Vector2[] ptsB = (Vector2[])b;

            List<(int iA, int iB)> crossings = FindCorrespondingVerticies(ptsA, ptsB);
            if (crossings.Count == 0)
                return true;

            //A chord endpoint on the seam belongs to both neighbouring spans.
            foreach ((int cA, int cB) in crossings)
            {
                if (iA == cA || iB == cB)
                    return true;
            }

            int spanA = SpanIndex(crossings, iA, c => c.iA);
            int spanB = SpanIndex(crossings, iB, c => c.iB);

            if (RunSameDirection(ptsA, ptsB, crossings))
                return spanA == spanB;

            return spanA == crossings.Count - spanB;
        }

        private static int SpanIndex(List<(int iA, int iB)> crossings, int iVertex, System.Func<(int iA, int iB), int> select)
        {
            int span = 0;
            foreach ((int iA, int iB) crossing in crossings)
            {
                if (select(crossing) < iVertex)
                    span++;
            }

            return span;
        }

        /// <summary>
        /// Pairs of indices whose points coincide, ordered along <paramref name="a"/>.
        /// </summary>
        private static List<(int iA, int iB)> FindCorrespondingVerticies(Vector2[] a, Vector2[] b)
        {
            List<(int, int)> result = [];
            for (int i = 0; i < a.Length; i++)
            {
                for (int j = 0; j < b.Length; j++)
                {
                    if (a[i] == b[j])
                    {
                        result.Add((i, j));
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// With two or more crossings the order they occur in on each line settles the direction.  With one, compare
        /// the XY tangents where the lines meet.
        /// </summary>
        private static bool RunSameDirection(Vector2[] a, Vector2[] b, List<(int iA, int iB)> crossings)
        {
            if (crossings.Count >= 2)
                return crossings[^1].iB > crossings[0].iB;

            (int iA, int iB) = crossings[0];
            return Vector2.Dot(Tangent(a, iA), Tangent(b, iB)) >= 0;
        }

        private static Vector2 Tangent(Vector2[] pts, int i)
        {
            int prev = i > 0 ? i - 1 : i;
            int next = i < pts.Length - 1 ? i + 1 : i;
            return pts[next] - pts[prev];
        }
    }
}
