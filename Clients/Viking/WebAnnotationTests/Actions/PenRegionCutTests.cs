using Geometry;
using Geometry.Transforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation.UI;
using WebAnnotation.UI.Actions;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests.Actions
{
    /// <summary>
    /// A pen stroke that crosses an outline more than twice encloses a region (<see cref="PenRegionCut"/>): cutting a bridge out
    /// of a cell splits it into the pieces on either side, and a stroke from the outline into a hole opens the hole.
    /// </summary>
    [TestClass]
    public class PenRegionCutTests
    {
        private const double Tolerance = 2.0;

        /// <summary>An identity grid covering section and volume 0..300.</summary>
        private static IVolumeToSectionTransform Identity()
        {
            MappingVector2[] points = new MappingVector2[9];
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Vector2 mapped = new(x * 150, y * 150);
                    points[x + (y * 3)] = new MappingVector2(mapped, mapped);
                }
            }

            return new VolumeToSectionTransform("grid", new GridTransform(points, new Rectangle(0, 300, 0, 300), 3, 3, new TransformBasicInfo()));
        }

        private static LocationObj PolygonLocation(string wkt) => TestModel.Location(7, 0, LocationType.POLYGON, wkt);

        /// <summary>
        /// A closed ring through the corners with a point every 10 px along each edge, as WKT. With only its corners a square is
        /// drawn as a round blob, since the points are curve control points.
        /// </summary>
        private static string Ring(params Vector2[] corners)
        {
            List<Vector2> points = Stroke(10, [.. corners, corners[0]]);
            return "(" + string.Join(", ", points.Select(p => System.FormattableString.Invariant($"{p.X} {p.Y}"))) + ")";
        }

        private static List<Vector2> Stroke(params Vector2[] corners) => Stroke(2, corners);

        /// <summary>Points every <paramref name="spacing"/> px along the corners given.</summary>
        private static List<Vector2> Stroke(double spacing, Vector2[] corners)
        {
            List<Vector2> points = [corners[0]];
            for (int i = 1; i < corners.Length; i++)
            {
                int steps = (int)(Vector2.Distance(corners[i - 1], corners[i]) / spacing);
                for (int s = 1; s <= steps; s++)
                {
                    double t = (double)s / steps;
                    points.Add(new Vector2(corners[i - 1].X + t * (corners[i].X - corners[i - 1].X), corners[i - 1].Y + t * (corners[i].Y - corners[i - 1].Y)));
                }
            }

            return points;
        }

        /// <summary>
        /// Two boxes joined by a bridge. A U-shaped stroke from above the bridge, down past it and back up, cuts the middle of
        /// the bridge out: the boxes become two choices, and each keeps its stored points exactly.
        /// </summary>
        [TestMethod]
        public void CuttingABridgeOutLeavesThePiecesOnEitherSide()
        {
            LocationObj location = PolygonLocation("POLYGON(" + Ring(new(0, 0), new(100, 0), new(100, 40), new(150, 40), new(150, 0), new(250, 0), new(250, 100), new(150, 100), new(150, 60), new(100, 60), new(100, 100), new(0, 100)) + ")");
            List<Vector2> stroke = Stroke(new(115, 85), new(115, 15), new(135, 15), new(135, 85));

            List<Change2DContourAction> choices = PenRegionCut.GetActions(location, Identity(), stroke, Tolerance);

            Assert.AreEqual(2, choices.Count);
            Assert.IsTrue(choices.All(c => c.RetraceType == RetraceCommandAction.SHRINK_EXTERIOR_RING));
            Change2DContourAction left = choices.Single(c => c.NewMosaicPolygon.ExteriorRing.Contains(new Vector2(0, 0)));
            Change2DContourAction right = choices.Single(c => c.NewMosaicPolygon.ExteriorRing.Contains(new Vector2(250, 0)));
            CollectionAssert.IsSubsetOf(new[] { new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 100), new Vector2(0, 100) }, left.NewMosaicPolygon.ExteriorRing);
            CollectionAssert.IsSubsetOf(new[] { new Vector2(150, 0), new Vector2(250, 0), new Vector2(250, 100), new Vector2(150, 100) }, right.NewMosaicPolygon.ExteriorRing);
            Assert.IsFalse(choices.SelectMany(c => c.NewMosaicPolygon.ExteriorRing).Any(p => p.X > 117 && p.X < 133), "nothing is left between the stroke's arms");
        }

        /// <summary>A stroke from outside the cell into its hole and back cuts a slot that opens the hole to the outside.</summary>
        [TestMethod]
        public void CuttingFromTheOutlineIntoAHoleOpensTheHole()
        {
            LocationObj location = PolygonLocation("POLYGON(" + Ring(new(0, 0), new(200, 0), new(200, 200), new(0, 200)) + ", " + Ring(new(80, 80), new(120, 80), new(120, 120), new(80, 120)) + ")");
            List<Vector2> stroke = Stroke(new(-20, 95), new(100, 95), new(100, 105), new(-20, 105));

            List<Change2DContourAction> choices = PenRegionCut.GetActions(location, Identity(), stroke, Tolerance);

            Assert.AreEqual(1, choices.Count);
            Assert.AreEqual(0, choices[0].NewMosaicPolygon.InteriorRings.Count, "the hole is now part of the outline");
            CollectionAssert.IsSubsetOf(new[] { new Vector2(200, 0), new Vector2(200, 200) }, choices[0].NewMosaicPolygon.ExteriorRing);
        }

        [TestMethod]
        public void AStrokeEndingOnTheOtherSideOffersNothing()
        {
            LocationObj location = PolygonLocation("POLYGON(" + Ring(new(0, 0), new(100, 0), new(100, 100), new(0, 100)) + ")");
            List<Vector2> stroke = Stroke(new(-10, 50), new(50, 50), new(50, -10), new(70, -10), new(70, 50));

            Assert.AreEqual(0, PenRegionCut.GetActions(location, Identity(), stroke, Tolerance).Count);
        }
    }
}
