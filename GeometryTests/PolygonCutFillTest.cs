using FsCheck;
using Geometry;
using Geometry.Meshing;
using GeometryTests.FSCheck;
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
        /// A renderer positions the mesh by the centroid and draws positions relative to it, so the centroid must be
        /// the polygon's and the fill must cover the polygon: its triangle areas add up to the polygon's area.
        /// </summary>
        [TestMethod]
        public void FillExposesPolygonCentroidAndCoversPolygon()
        {
            Polygon ring = Circle(64, 100).Translate(new Vector2(5000, -3000));
            PolygonCutFill fill = PolygonCutFill.Create(ring);

            Assert.AreEqual(ring.Centroid.X, fill.Centroid.X, 1e-9);
            Assert.AreEqual(ring.Centroid.Y, fill.Centroid.Y, 1e-9);

            double triangleArea = 0;
            for (int i = 0; i < fill.TriangleIndices.Count; i += 3)
            {
                Vector2 a = fill.Positions[fill.TriangleIndices[i]] - fill.Centroid;
                Vector2 b = fill.Positions[fill.TriangleIndices[i + 1]] - fill.Centroid;
                Vector2 c = fill.Positions[fill.TriangleIndices[i + 2]] - fill.Centroid;
                triangleArea += Math.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) / 2.0;
            }

            Assert.AreEqual(ring.Area, triangleArea, ring.Area * 1e-9);
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

        /// <summary>
        /// Regression: the left piece of a square cut at x = 50, as the pen retrace walks it. The last vertex before the
        /// closing point, (0,0), is a corner. The reduction used to drop it whatever the tolerance, turning the
        /// rectangle into a triangle of half the area.
        /// </summary>
        [TestMethod]
        public void ReduceExteriorRingKeepsTheCornerBeforeTheClosingVertex()
        {
            Polygon piece = new([new(50, 0), new(50, 50), new(50, 100), new(0, 100), new(0, 0), new(50, 0)]);

            Polygon reduced = piece.ReduceExteriorRing(4);

            CollectionAssert.AreEqual(new[] { new Vector2(50, 0), new Vector2(50, 100), new Vector2(0, 100), new Vector2(0, 0), new Vector2(50, 0) }, reduced.ExteriorRing);
            Assert.AreEqual(5000, reduced.Area, 1e-9);
        }

        private sealed class RingCase
        {
            public Polygon Polygon;
            public double Tolerance;

            public override string ToString() =>
                $"tolerance {Tolerance:R}, ring {string.Join(" ", Polygon.ExteriorRing.Select(p => $"({p.X:R},{p.Y:R})"))}";
        }

        /// <summary>
        /// Star-shaped ring of 4-40 vertices (angles strictly increasing, radius 20-100% of the scale) around a centre up
        /// to 500,000 pixels from the origin, at scales from 10 to 100,000 pixels, with a tolerance of 0.1-5% of the scale.
        /// </summary>
        private static Arbitrary<RingCase> ArbStarRing() =>
            Arb.From(
                from count in Gen.Choose(4, 40)
                from scaleExponent in Gen.Choose(1, 5)
                from cx in Gen.Choose(-500000, 500000)
                from cy in Gen.Choose(-500000, 500000)
                from jitters in Gen.ArrayOf(count, Gen.Choose(0, 400))
                from radii in Gen.ArrayOf(count, Gen.Choose(200, 1000))
                from tolerancePermille in Gen.Choose(1, 50)
                let scale = Math.Pow(10, scaleExponent)
                select new RingCase
                {
                    Polygon = StarRing(count, scale, new Vector2(cx, cy), jitters, radii),
                    Tolerance = scale * tolerancePermille / 1000.0,
                });

        private static Polygon StarRing(int count, double scale, Vector2 centre, int[] jitters, int[] radii)
        {
            double step = 2 * Math.PI / count;
            Vector2[] ring = new Vector2[count + 1];
            for (int i = 0; i < count; i++)
            {
                double angle = (i + (jitters[i] / 1000.0)) * step;
                double radius = scale * radii[i] / 1000.0;
                ring[i] = new Vector2(centre.X + (radius * Math.Cos(angle)), centre.Y + (radius * Math.Sin(angle)));
            }

            ring[count] = ring[0];
            return new Polygon(ring);
        }

        private static double DistanceToRing(Vector2[] ring, Vector2 p)
        {
            double best = double.MaxValue;
            for (int i = 0; i < ring.Length - 1; i++)
                best = Math.Min(best, new LineSegment(ring[i], ring[i + 1]).DistanceToPoint(p));

            return best;
        }

        /// <summary>
        /// The chord bound holds all the way round: every vertex of the source ring lies within the tolerance of the
        /// reduced ring, including the vertices next to the closing point.
        /// </summary>
        [TestMethod]
        public void ReduceExteriorRingStaysWithinToleranceOfEveryVertex() =>
            CoreCheck.Run(
                Prop.ForAll(ArbStarRing(), c =>
                {
                    Vector2[] reduced = c.Polygon.ReduceExteriorRing(c.Tolerance).ExteriorRing;

                    // Relative slack for the distance arithmetic at coordinates up to ~6e5.
                    double slack = c.Tolerance * 1e-9;
                    return c.Polygon.ExteriorRing.All(p => DistanceToRing(reduced, p) <= c.Tolerance + slack);
                }),
                nameof(ReduceExteriorRingStaysWithinToleranceOfEveryVertex));
    }
}
