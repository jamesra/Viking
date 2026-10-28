using Geometry;
using Geometry.Transforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Viking.VolumeModel;
using WebAnnotation.UI.Actions;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// A pen cut is saved as control points (<see cref="ControlPointCut"/>): the stored points on the kept side come back
    /// exactly as stored, and part of the shape outside the section mapping makes the cut fail instead of throwing, so the
    /// command can skip it on pen-up.
    /// </summary>
    [TestClass]
    public class RetraceAndReplaceMappingTests
    {
        private const double Tolerance = 2.0;

        /// <summary>
        /// A 3x3 identity grid covering section and volume 0..200; points far outside it cannot be mapped.
        /// </summary>
        private static IVolumeToSectionTransform CreateBoundedTransform()
        {
            MappingVector2[] points = new MappingVector2[9];
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Vector2 mapped = new(x * 100, y * 100);
                    points[x + (y * 3)] = new MappingVector2(mapped, mapped);
                }
            }

            return new VolumeToSectionTransform("grid", new GridTransform(points, new Rectangle(0, 200, 0, 200), 3, 3, new TransformBasicInfo()));
        }

        private static Vector2[] Square(double min, double max) =>
            [new(min, min), new(max, min), new(max, max), new(min, max), new(min, min)];

        /// <summary>Sixteen control points on a circle of radius 60 around (100, 100), closed.</summary>
        private static Vector2[] Circle16()
        {
            Vector2[] ring = new Vector2[17];
            for (int i = 0; i < 16; i++)
            {
                double angle = i * Math.PI / 8;
                ring[i] = new Vector2(100 + 60 * Math.Cos(angle), 100 + 60 * Math.Sin(angle));
            }

            ring[16] = ring[0];
            return ring;
        }

        private static readonly PolygonIndex Exterior = new(0, 0, 16);

        /// <summary>A stroke that enters the circle near angle 0, runs 10 px inside the outline and leaves near angle 90 degrees.</summary>
        private static Vector2[] StrokeCuttingTheFirstQuarter() =>
            [.. Enumerable.Range(-2, 25).Select(i =>
            {
                double angle = i * Math.PI / 40;
                double radius = i is < 0 or > 20 ? 75 : 50;
                return new Vector2(100 + radius * Math.Cos(angle), 100 + radius * Math.Sin(angle));
            })];

        [TestMethod]
        public void UnmappableRingFails()
        {
            Polygon mosaic = new(Square(-1000, -900));

            Assert.IsFalse(ControlPointCut.TrySplice(mosaic, CreateBoundedTransform(), new PolygonIndex(0, 0, 4), [new(-1100, -950), new(-950, -950)], Tolerance, out _, out _));
        }

        /// <summary>
        /// The control points away from the cut are saved exactly as stored, and the kept piece has only a few new points.
        /// </summary>
        [TestMethod]
        public void KeptSideReusesTheStoredPointsExactly()
        {
            Vector2[] stored = Circle16();
            Polygon mosaic = new(stored);

            Assert.IsTrue(ControlPointCut.TrySplice(mosaic, CreateBoundedTransform(), Exterior, StrokeCuttingTheFirstQuarter(), Tolerance, out ControlPointCut.Piece first, out ControlPointCut.Piece second));

            ControlPointCut.Piece kept = first.Mosaic.Area > second.Mosaic.Area ? first : second;
            // The quarter from angle 0 to 90 degrees is cut away; points 5 to 15 (112.5 to 337.5 degrees) are untouched
            for (int i = 5; i <= 15; i++)
                Assert.IsTrue(kept.Mosaic.ExteriorRing.Contains(stored[i]), $"stored point {i} kept exactly");

            Assert.IsTrue(kept.Mosaic.ExteriorRing.Length < stored.Length + 20, "only the cut stretch adds points");
        }

        /// <summary>An exterior cut keeps a stored hole it still covers, exactly as stored.</summary>
        [TestMethod]
        public void ExteriorCutKeepsTheStoredHole()
        {
            Polygon mosaic = new(Circle16());
            Vector2[] hole = Square(85, 115);
            mosaic.AddInteriorRing(hole);

            Assert.IsTrue(ControlPointCut.TrySplice(mosaic, CreateBoundedTransform(), Exterior, StrokeCuttingTheFirstQuarter(), Tolerance, out ControlPointCut.Piece first, out ControlPointCut.Piece second));

            ControlPointCut.Piece kept = first.Mosaic.Area > second.Mosaic.Area ? first : second;
            Assert.AreEqual(1, kept.Mosaic.InteriorRings.Count);
            CollectionAssert.AreEquivalent(hole.Take(4).ToArray(), kept.Mosaic.InteriorRings[0].Take(4).ToArray());
            Assert.AreEqual(1, kept.Volume.InteriorRings.Count);
        }
    }
}
