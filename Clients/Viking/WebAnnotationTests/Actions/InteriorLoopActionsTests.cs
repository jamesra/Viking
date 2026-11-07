using AnnotationService.Types;
using Geometry;
using Geometry.Transforms;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation;
using WebAnnotation.UI;
using WebAnnotation.UI.Actions;
using WebAnnotationModel;

namespace WebAnnotationTests.Actions
{
    /// <summary>
    /// A closed pen loop inside a polygon (<see cref="Shared2DShapeActionsForPath.IdentifyPossibleInteriorActions(IShape2D, IShape2D, Path, Func{LocationObj}, Func{IVolumeToSectionTransform})"/>)
    /// is accepted against the outline as drawn (smoothed), but the replaced hole is added to the control polygon, which the
    /// smoothed curve bulges past. These run on pen mouse-up with no handler above them, so a loop that cannot become a
    /// hole, or a location that left the store, offers no choice instead of throwing.
    /// </summary>
    [TestClass]
    public class InteriorLoopActionsTests
    {
        /// <summary>
        /// The default of <c>NumClosedCurveInterpolationPointsForDisplay</c>, which the polygon view smooths with. Read as a
        /// literal because <c>Global</c>'s type initializer needs an open volume.
        /// </summary>
        private const uint DrawnInterpolationPoints = 3;

        /// <summary>A 200 px square control outline with corner points only, as a sparse pen outline is stored.</summary>
        private static readonly Vector2[] Outline = [new(0, 0), new(200, 0), new(200, 200), new(0, 200), new(0, 0)];

        /// <summary>A 40 px square hole from 80 to 120.</summary>
        private static readonly Vector2[] Hole = [new(80, 80), new(120, 80), new(120, 120), new(80, 120), new(80, 80)];

        /// <summary>An identity grid covering section and volume -150..450, so a loop just past the outline still maps.</summary>
        private static IVolumeToSectionTransform Identity() => IdentityGrid(-150, 450);

        /// <summary>An identity grid covering section and volume <paramref name="min"/>..<paramref name="max"/>.</summary>
        private static IVolumeToSectionTransform IdentityGrid(double min, double max)
        {
            MappingVector2[] points = new MappingVector2[9];
            double step = (max - min) / 2;
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Vector2 mapped = new(min + (x * step), min + (y * step));
                    points[x + (y * 3)] = new MappingVector2(mapped, mapped);
                }
            }

            return new VolumeToSectionTransform("grid", new GridTransform(points, new Rectangle(min, max, min, max), 3, 3, new TransformBasicInfo()));
        }

        private static Polygon ControlPolygon(Vector2[] outline)
        {
            Polygon polygon = new(outline);
            polygon.AddInteriorRing(Hole);
            return polygon;
        }

        private static LocationObj Location(Polygon mosaic)
        {
            SqlGeometry shape = mosaic.ToSqlGeometry();
            return new LocationObj(new Location
            {
                ID = 11,
                TypeCode = (short)LocationType.POLYGON,
                Section = 1,
                MosaicShapeWKB = shape.AsBinary(),
                VolumeShapeWKB = shape.AsBinary()
            });
        }

        /// <summary>A pen stroke around an ellipse that runs a quarter turn past its start, closing a loop.</summary>
        private static Path Loop(Vector2 center, double radiusX, double radiusY)
        {
            Path path = new();
            const int steps = 72;
            for (int i = 0; i <= steps + (steps / 4); i++)
            {
                double angle = 2 * Math.PI * i / steps;
                path.Push(new Vector2(center.X + (radiusX * Math.Cos(angle)), center.Y + (radiusY * Math.Sin(angle))));
            }

            return path;
        }

        private static List<IAction> Actions(Polygon control, LocationObj location, Path path, IVolumeToSectionTransform? transform = null) =>
            Shared2DShapeActionsForPath.IdentifyPossibleInteriorActions(
                control,
                control.Smooth(DrawnInterpolationPoints),
                path,
                () => location,
                () => transform ?? Identity());

        /// <summary>Around the hole, dipping below the bottom edge where the smoothed outline bulges out.</summary>
        private static Path LoopPastTheControlEdge() => Loop(new Vector2(100, 65), 40, 75);

        /// <summary>Around the hole and well inside the outline.</summary>
        private static Path LoopInside() => Loop(new Vector2(100, 100), 50, 50);

        /// <summary>
        /// The state the fix is for is reachable: the smoothed outline covers the loop, but the control polygon refuses it as a hole.
        /// </summary>
        [TestMethod]
        public void SmoothedOutlineCoversALoopTheControlPolygonRefuses()
        {
            Polygon control = ControlPolygon(Outline);
            Polygon smoothed = control.Smooth(DrawnInterpolationPoints);
            Path path = LoopPastTheControlEdge();
            Polygon loop = new(path.SimplifiedFirstLoop);

            Assert.IsTrue(path.HasSelfIntersection);
            Assert.IsTrue(new Polygon(smoothed.ExteriorRing).Covers(loop), "the drawn outline covers the loop");
            Assert.IsTrue(loop.Covers(new Polygon(Hole)), "the loop encloses the hole");

            Polygon copy = (Polygon)control.Clone();
            copy.RemoveInteriorRing(0);
            Assert.ThrowsException<ArgumentException>(() => copy.AddInteriorRing(loop));
        }

        [TestMethod]
        public void LoopCrossingTheControlOutlineOffersNoChoice()
        {
            Polygon control = ControlPolygon(Outline);
            List<IAction> actions = Actions(control, Location(control), LoopPastTheControlEdge());
            Assert.AreEqual(0, actions.Count);
        }

        /// <summary>The stored (mosaic) outline can differ from the mapped one; a hole it refuses is not offered either.</summary>
        [TestMethod]
        public void LoopTheStoredOutlineRefusesOffersNoChoice()
        {
            Polygon control = ControlPolygon(Outline);
            Polygon narrowerMosaic = ControlPolygon([new(0, 0), new(140, 0), new(140, 200), new(0, 200), new(0, 0)]);
            List<IAction> actions = Actions(control, Location(narrowerMosaic), LoopInside());
            Assert.AreEqual(0, actions.Count);
        }

        /// <summary>A pen choice whose points the section cannot map is not offered.</summary>
        [TestMethod]
        public void LoopOutsideTheMappedAreaOffersNoChoice()
        {
            Polygon control = ControlPolygon(Outline);
            List<IAction> actions = Actions(control, Location(control), LoopInside(), IdentityGrid(0, 120));
            Assert.AreEqual(0, actions.Count);
        }

        [TestMethod]
        public void LoopAroundTheHoleReplacesIt()
        {
            Polygon control = ControlPolygon(Outline);
            Path path = LoopInside();
            List<IAction> actions = Actions(control, Location(control), path);

            Assert.AreEqual(1, actions.Count);
            Change2DContourAction replace = (Change2DContourAction)actions[0];
            Assert.AreEqual(RetraceCommandAction.REPLACE_INTERIOR_RING, replace.RetraceType);

            Polygon loop = new(path.SimplifiedFirstLoop);
            foreach (Polygon replaced in new[] { replace.NewMosaicPolygon, replace.NewVolumePolygon })
            {
                Assert.AreEqual(1, replaced.InteriorPolygons.Count);
                Assert.AreEqual(loop.Area, replaced.InteriorPolygons[0].Area, loop.Area * 1e-9);
                Assert.AreEqual(new Polygon(control.ExteriorRing).Area, new Polygon(replaced.ExteriorRing).Area, 1e-6);
            }
        }

        /// <summary>The view can outlive its store entry (deleted or replaced by a poll).</summary>
        [TestMethod]
        public void LocationNoLongerInTheStoreOffersNoChoice()
        {
            Polygon control = ControlPolygon(Outline);
            Polygon smoothed = control.Smooth(DrawnInterpolationPoints);

            Assert.AreEqual(0, Shared2DShapeActionsForPath.IdentifyPossibleInteriorActions(control, smoothed, LoopInside(), () => null, Identity).Count);
            Assert.AreEqual(0, Shared2DShapeActionsForPath.IdentifyPossibleInteriorActions(control, smoothed, Loop(new Vector2(40, 160), 15, 15), () => null, Identity).Count, "cut-hole branch");
            Assert.AreEqual(0, Shared2DShapeActionsForPath.IdentifyPossibleReshapeAction((LocationObj?)null, smoothed, LoopInside()).Count);
        }
    }
}
