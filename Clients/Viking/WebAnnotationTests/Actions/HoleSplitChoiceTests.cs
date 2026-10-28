using Geometry;
using Geometry.Transforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using System.Collections.Generic;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation.UI;
using WebAnnotation.UI.Actions;
using WebAnnotation.UI.Commands;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests.Actions
{
    /// <summary>
    /// A pen stroke that runs from the polygon through one of its holes and back splits the hole in two
    /// (<see cref="PolygonCut.BuildRetraceActions"/>). A click on either part of the hole fills that part; a click on the
    /// polygon outside the hole fills the smaller part. An outline retrace still keeps the piece that is clicked.
    /// </summary>
    [TestClass]
    public class HoleSplitChoiceTests
    {
        private const double Tolerance = 2.0;

        /// <summary>Same tolerance <see cref="PolygonCut.GetRetraceActionForPath"/> uses at downsample 1.</summary>
        private const double ReduceTolerance = 4;

        /// <summary>In the small part of the hole, left of the stroke at x = 123.</summary>
        private static readonly Vector2 InSmallPart = new(110, 150);

        /// <summary>In the large part of the hole, right of the stroke.</summary>
        private static readonly Vector2 InLargePart = new(160, 150);

        /// <summary>On the polygon itself, outside the hole.</summary>
        private static readonly Vector2 OnPolygon = new(60, 150);

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

        /// <summary>A 260 px square with a 100 px square hole from 100 to 200, with a control point every 10 px.</summary>
        private static LocationObj SquareWithHole()
        {
            string wkt = "POLYGON(" + Ring(new(20, 20), new(280, 20), new(280, 280), new(20, 280)) + ", " + Ring(new(100, 100), new(200, 100), new(200, 200), new(100, 200)) + ")";
            return TestModel.Location(7, 0, LocationType.POLYGON, wkt);
        }

        private static string Ring(params Vector2[] corners)
        {
            List<Vector2> points = Stroke(10, [.. corners, corners[0]]);
            return "(" + string.Join(", ", points.Select(p => System.FormattableString.Invariant($"{p.X} {p.Y}"))) + ")";
        }

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

        /// <summary>The choices for <paramref name="stroke"/> over the location as drawn, as the pen builds them.</summary>
        private static List<Change2DContourAction> Choices(LocationObj location, List<Vector2> stroke, bool enterIsFirstInteraction)
        {
            Polygon drawn = location.MosaicShape.ToPolygon().Smooth(WebAnnotation.Global.NumClosedCurveInterpolationPointsForDisplay);
            Vector2[] path = [.. stroke];
            PolygonIndex first = drawn.IntersectingSegmentIndices(path.ToLineSegments()).First().Value;
            return PolygonCut.BuildRetraceActions(location, Identity(), drawn, path, first, enterIsFirstInteraction, Tolerance, ReduceTolerance);
        }

        /// <summary>A stroke from the polygon down through the hole at x = 123 and back onto the polygon.</summary>
        private static List<Change2DContourAction> SplitTheHole() =>
            Choices(SquareWithHole(), Stroke(2, [new(123, 61), new(123, 239)]), enterIsFirstInteraction: false);

        /// <summary>The choice a click at <paramref name="point"/> picks, as the choice canvas hit-tests it, or null.</summary>
        private static Change2DContourAction Clicked(IEnumerable<Change2DContourAction> choices, Vector2 point) =>
            choices.FirstOrDefault(c => c.ClickRegions.Any(r => r.Covers((IPoint2D)point)));

        private static bool IsFilled(Change2DContourAction choice, Vector2 point) => choice.NewMosaicPolygon.Covers((IPoint2D)point);

        [TestMethod]
        public void ClickingTheSmallPartOfTheHoleFillsIt()
        {
            List<Change2DContourAction> choices = SplitTheHole();
            Assert.AreEqual(2, choices.Count);
            Assert.IsTrue(choices.All(c => c.RetraceType == RetraceCommandAction.SHRINK_INTERNAL_RING));

            Change2DContourAction picked = Clicked(choices, InSmallPart);

            Assert.IsNotNull(picked);
            Assert.IsTrue(IsFilled(picked, InSmallPart), "the clicked part becomes part of the polygon");
            Assert.IsFalse(IsFilled(picked, InLargePart), "the other part stays a hole");
            Assert.AreEqual(1, picked.NewMosaicPolygon.InteriorRings.Count);
        }

        /// <summary>
        /// A sliver narrower than the chord tolerance the choices are thinned with is still clickable, because the parts are
        /// hit-tested as walked.
        /// </summary>
        [DataTestMethod]
        [DataRow(103.0, 101.5)]
        [DataRow(107.0, 103.5)]
        public void ClickingAThinSliverOfTheHoleFillsIt(double strokeX, double clickX)
        {
            List<Change2DContourAction> choices = Choices(SquareWithHole(), Stroke(2, [new(strokeX, 61), new(strokeX, 239)]), enterIsFirstInteraction: false);
            Vector2 inSliver = new(clickX, 150);

            Change2DContourAction picked = Clicked(choices, inSliver);

            Assert.IsNotNull(picked);
            Assert.IsTrue(picked.FillsSmallerHolePart);
            Assert.IsTrue(IsFilled(picked, inSliver));
            Assert.IsFalse(IsFilled(picked, InLargePart));
        }

        [TestMethod]
        public void ClickingTheLargePartOfTheHoleFillsIt()
        {
            List<Change2DContourAction> choices = SplitTheHole();

            Change2DContourAction picked = Clicked(choices, InLargePart);

            Assert.IsNotNull(picked);
            Assert.IsTrue(IsFilled(picked, InLargePart), "the clicked part becomes part of the polygon");
            Assert.IsFalse(IsFilled(picked, InSmallPart), "the other part stays a hole");
        }

        [TestMethod]
        public void ClickingThePolygonFillsTheSmallerPart()
        {
            List<Change2DContourAction> choices = SplitTheHole();

            Change2DContourAction picked = Clicked(choices, OnPolygon);

            Assert.IsNotNull(picked);
            Assert.IsTrue(picked.FillsSmallerHolePart);
            Assert.IsTrue(IsFilled(picked, InSmallPart));
            Assert.IsFalse(IsFilled(picked, InLargePart));
            Assert.AreSame(picked, choices[0], "the choice that fills the smaller part is listed first");
        }

        /// <summary>Each choice previews, and is named by, the part of the hole it fills.</summary>
        [TestMethod]
        public void HoleChoicesShowAndNameThePartTheyFill()
        {
            List<Change2DContourAction> choices = SplitTheHole();
            Change2DContourAction fillsSmall = choices.Single(c => c.FillsSmallerHolePart);
            Change2DContourAction fillsLarge = choices.Single(c => !c.FillsSmallerHolePart);

            Assert.IsTrue(fillsSmall.FilledHolePart.Covers((IPoint2D)InSmallPart));
            Assert.IsFalse(fillsSmall.FilledHolePart.Covers((IPoint2D)InLargePart));
            Assert.IsTrue(fillsLarge.FilledHolePart.Covers((IPoint2D)InLargePart));
            Assert.IsNull(fillsLarge.ClickOutsideHole, "only the smaller part's choice takes clicks on the polygon");
            Assert.AreEqual("Fill smaller part of hole", ActionSelectionCanvasControl.LabelFor(fillsSmall));
            Assert.AreEqual("Fill larger part of hole", ActionSelectionCanvasControl.LabelFor(fillsLarge));
        }

        /// <summary>When one side of the cut is not a valid polygon its part is unknown; that choice keeps its whole new shape.</summary>
        [TestMethod]
        public void AnUnknownPartKeepsTheWholeShapeAsTheClickArea()
        {
            Change2DContourAction lone = SplitTheHole()[0];
            Change2DContourAction copy = new(lone.Location, lone.RetraceType, lone.NewMosaicPolygon, lone.NewVolumePolygon, transform: Identity());

            PolygonCut.AttachHoleFillParts(copy, null, null, new Polygon(Stroke(10, [new(120, 100), new(200, 100), new(200, 200), new(120, 200), new(120, 100)]).ToArray()), lone.NewVolumePolygon);

            Assert.IsNull(copy.FilledHolePart);
            Assert.IsFalse(copy.FillsSmallerHolePart);
            Assert.AreEqual(1, copy.ClickRegions.Count);
            Assert.AreSame(copy.NewSmoothedVolumePolygon, copy.ClickRegions[0]);
            Assert.AreEqual("Replace boundary", ActionSelectionCanvasControl.LabelFor(copy));
        }

        /// <summary>
        /// The retrace command (Change Boundary) fills the smaller part of a split hole, and Ctrl fills the larger one, by
        /// keeping the other part as the hole.
        /// </summary>
        [TestMethod]
        public void RetraceCommandFillsTheSmallerPartUnlessCtrl()
        {
            Polygon small = new(Stroke(10, [new(100, 100), new(120, 100), new(120, 200), new(100, 200), new(100, 100)]).ToArray());
            Polygon large = new(Stroke(10, [new(120, 100), new(200, 100), new(200, 200), new(120, 200), new(120, 100)]).ToArray());

            Assert.AreSame(large, RetraceAndReplacePathCommand.HolePartKept(small, large, fillLarger: false));
            Assert.AreSame(large, RetraceAndReplacePathCommand.HolePartKept(large, small, fillLarger: false));
            Assert.AreSame(small, RetraceAndReplacePathCommand.HolePartKept(small, large, fillLarger: true));
            Assert.AreSame(small, RetraceAndReplacePathCommand.HolePartKept(large, small, fillLarger: true));
        }

        /// <summary>
        /// A stroke across the outline keeps its behaviour: each piece is clicked on its own new shape, and the piece clicked
        /// is the one kept.
        /// </summary>
        [TestMethod]
        public void OutlineRetraceKeepsTheClickedPiece()
        {
            List<Change2DContourAction> choices = Choices(SquareWithHole(), Stroke(2, [new(63, 1), new(63, 299)]), enterIsFirstInteraction: true);
            Assert.AreEqual(2, choices.Count);
            Assert.IsTrue(choices.All(c => c.RetraceType == RetraceCommandAction.SHRINK_EXTERIOR_RING));
            Assert.IsTrue(choices.All(c => c.FilledHolePart is null && c.ClickRegions.Count == 1 && ReferenceEquals(c.ClickRegions[0], c.NewSmoothedVolumePolygon)));

            Vector2 leftOfStroke = new(40, 150);
            Vector2 rightOfStroke = new(240, 150);
            Change2DContourAction left = Clicked(choices, leftOfStroke);
            Change2DContourAction right = Clicked(choices, rightOfStroke);

            Assert.IsNotNull(left);
            Assert.IsNotNull(right);
            Assert.AreNotSame(left, right);
            Assert.IsTrue(left.NewMosaicPolygon.Covers((IPoint2D)leftOfStroke) && !left.NewMosaicPolygon.Covers((IPoint2D)rightOfStroke));
            Assert.IsTrue(right.NewMosaicPolygon.Covers((IPoint2D)rightOfStroke) && !right.NewMosaicPolygon.Covers((IPoint2D)leftOfStroke));
            Assert.AreEqual(1, right.NewMosaicPolygon.InteriorRings.Count, "the larger piece keeps the hole");
        }
    }
}
