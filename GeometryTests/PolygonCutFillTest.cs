using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    [TestClass]
    public class PolygonCutFillTest
    {
        static Polygon Circle(int count, double radius)
        {
            Vector2[] ring = new Vector2[count + 1];
            for (int i = 0; i < count; i++)
            {
                double angle = i * Math.PI * 2.0 / count;
                ring[i] = new Vector2(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
            }

            ring[count] = ring[0];
            return new Polygon(ring);
        }

        static bool ContainsPoint(Polygon polygon, Vector2 point)
        {
            for (int i = 0; i < polygon.ExteriorRing.Length; i++)
            {
                Vector2 vertex = polygon.ExteriorRing[i];
                if (vertex.X == point.X && vertex.Y == point.Y)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A short chord must leave vertices on the far side of the ring bitwise unchanged.
        /// This is the contract the choice fill relies on after it stopped curve-fitting the whole cut.
        /// </summary>
        [TestMethod]
        public void WalkKeepsVerticesOutsideTheChord()
        {
            const double radius = 100;
            const double chordY = 80;
            Polygon ring = Circle(64, radius);
            Vector2[] path = [new Vector2(-radius * 2, chordY), new Vector2(radius * 2, chordY)];
            Polygon clockwise = Polygon.WalkPolygonCut(ring, RotationDirection.Clockwise, path);
            Polygon counter = Polygon.WalkPolygonCut(ring, RotationDirection.Counterclockwise, path);
            Polygon larger = clockwise.Area >= counter.Area ? clockwise : counter;
            Polygon smaller = ReferenceEquals(larger, clockwise) ? counter : clockwise;

            int kept = 0;
            int dropped = 0;
            for (int i = 0; i < ring.ExteriorRing.Length - 1; i++)
            {
                Vector2 vertex = ring.ExteriorRing[i];
                bool inLarger = ContainsPoint(larger, vertex);
                if (vertex.Y < chordY - 1)
                {
                    Assert.IsTrue(inLarger, $"Vertex {vertex} below the chord was rewritten");
                    kept++;
                }
                else if (vertex.Y > chordY + 1)
                {
                    Assert.IsFalse(inLarger, $"Vertex {vertex} above the chord stayed on the large piece");
                    Assert.IsTrue(ContainsPoint(smaller, vertex), $"Vertex {vertex} was dropped from both pieces");
                    dropped++;
                }
            }

            Assert.IsTrue(kept > 10, "Expected most of the ring to sit below the chord");
            Assert.IsTrue(dropped > 0, "Expected the cap above the chord to leave the large piece");
        }

        /// <summary>
        /// Triangles the chord does not cross keep the cached vertex indices, including a vertex far from the cut.
        /// </summary>
        [TestMethod]
        public void SeparateKeepsDistantTriangleIndices()
        {
            Polygon ring = Circle(32, 100);
            PolygonCutFill fill = PolygonCutFill.Create(ring);
            PolygonCutSeparation parts = fill.Separate([new Vector2(-200, 80), new Vector2(200, 80)]);

            Assert.IsTrue(parts.CavityTriangleIndices.Length >= 3, "The chord should cross at least one triangle");
            Assert.IsTrue(parts.KeptTriangleIndices.Length >= 3, "Distant triangles should remain");

            HashSet<(int A, int B, int C)> original = new();
            IReadOnlyList<int> all = fill.TriangleIndices;
            for (int i = 0; i + 2 < all.Count; i += 3)
                original.Add((all[i], all[i + 1], all[i + 2]));

            for (int i = 0; i + 2 < parts.KeptTriangleIndices.Length; i += 3)
            {
                (int A, int B, int C) tri = (parts.KeptTriangleIndices[i], parts.KeptTriangleIndices[i + 1], parts.KeptTriangleIndices[i + 2]);
                Assert.IsTrue(original.Contains(tri), "A kept triangle changed vertex indices");
            }

            int far = 0;
            double minY = double.MaxValue;
            for (int i = 0; i < fill.Positions.Count; i++)
            {
                if (fill.Positions[i].Y < minY)
                {
                    minY = fill.Positions[i].Y;
                    far = i;
                }
            }

            Assert.IsTrue(minY < 0, "The ring should extend below the chord");
            Assert.IsTrue(parts.KeptTriangleIndices.Contains(far), "The far vertex was dropped from the cached triangles");
        }

        /// <summary>
        /// A smoothed display ring is many samples of one curve. Chord reduction must drop most of them
        /// and keep only original coordinates, which is what a saved cut stores.
        /// </summary>
        [TestMethod]
        public void ReduceExteriorRingDropsSamplesAndKeepsOriginalVertices()
        {
            Polygon circle = Circle(128, 100);
            Polygon reduced = circle.ReduceExteriorRing(2.0);

            Assert.IsTrue(reduced.ExteriorRing.Length < circle.ExteriorRing.Length / 2,
                "Interpolated samples should not all survive");
            Assert.IsTrue(reduced.IsValid());

            foreach (Vector2 vertex in reduced.ExteriorRing)
                Assert.IsTrue(ContainsPoint(circle, vertex), "Reduction moved a vertex");
        }
    }
}
