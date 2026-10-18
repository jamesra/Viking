using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using rogue1.codepharm.net.XSD.WebAnnotationUserSettings.xsd;
using System.Collections.Generic;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.Interfaces;
using VikingXNAGraphics;
using WebAnnotation;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the pure helpers in <c>WebAnnotation/Extensions.cs</c>: hotkey modifier text, single-modifier
    /// key checks, the user-settings annotation-type-name mapping, and nearest-location-link selection.
    /// Combined modifier states (Ctrl+Shift, Alt) are deliberately not asserted; their intended
    /// behavior is an open decision.
    /// </summary>
    [TestClass]
    public class ExtensionsTests
    {
        [TestMethod]
        [DataRow(false, false, "")]
        [DataRow(true, false, "CTRL + ")]
        [DataRow(false, true, "SHIFT + ")]
        [DataRow(true, true, "CTRL + SHIFT + ")]
        public void BuildModifierStringListsCtrlBeforeShift(bool ctrl, bool shift, string expected)
        {
            Hotkey hotkey = new() { Ctrl = ctrl, Shift = shift };
            Assert.AreEqual(expected, hotkey.BuildModifierString());
        }

        [TestMethod]
        public void PlainShiftIsShiftNotCtrl()
        {
            Assert.IsTrue(Keys.Shift.ShiftPressed());
            Assert.IsFalse(Keys.Shift.CtrlPressed());
            Assert.IsTrue(Keys.Shift.ShiftOrCtrlPressed());
        }

        [TestMethod]
        public void PlainCtrlIsCtrlNotShift()
        {
            Assert.IsTrue(Keys.Control.CtrlPressed());
            Assert.IsFalse(Keys.Control.ShiftPressed());
            Assert.IsTrue(Keys.Control.ShiftOrCtrlPressed());
        }

        [TestMethod]
        public void NoModifierIsNeitherShiftNorCtrl()
        {
            Assert.IsFalse(Keys.None.ShiftPressed());
            Assert.IsFalse(Keys.None.CtrlPressed());
            Assert.IsFalse(Keys.None.ShiftOrCtrlPressed());
        }

        [TestMethod]
        [DataRow("Circle", LocationType.CIRCLE)]
        [DataRow("ClosedCurve", LocationType.CLOSEDCURVE)]
        [DataRow("OpenCurve", LocationType.OPENCURVE)]
        [DataRow("Polygon", LocationType.POLYGON)]
        [DataRow("Polyline", LocationType.POLYLINE)]
        [DataRow("Point", LocationType.POINT)]
        [DataRow("Ellipse", LocationType.ELLIPSE)]
        [DataRow("CurvePolygon", LocationType.CURVEPOLYGON)]
        public void KnownAnnotationTypeNamesMapToLocationTypes(string name, LocationType expected)
        {
            CreateStructureCommandAction create = new() { AnnotationType = name };
            ChangeLocationAnnotationTypeAction change = new() { AnnotationType = name };

            Assert.AreEqual(expected, create.GetLocationType());
            Assert.AreEqual(expected, change.GetLocationType());
        }

        [TestMethod]
        [DataRow("NotAType")]
        [DataRow("circle")]
        [DataRow("")]
        public void UnknownAnnotationTypeNameFallsBackToCircle(string name)
        {
            CreateStructureCommandAction create = new() { AnnotationType = name };
            ChangeLocationAnnotationTypeAction change = new() { AnnotationType = name };

            Assert.AreEqual(LocationType.CIRCLE, create.GetLocationType());
            Assert.AreEqual(LocationType.CIRCLE, change.GetLocationType());
        }

        [TestMethod]
        public void NearestLinkContainingPointPicksSmallestDistanceAtOrBelowOne()
        {
            HitTestResult far = Link(0.9);
            HitTestResult near = Link(0.2);
            HitTestResult edge = Link(1.0);

            Assert.AreSame(near, HitTestResultExtensions.NearestLocationLinkContainingPoint(new[] { far, near, edge }));
            Assert.AreSame(edge, HitTestResultExtensions.NearestLocationLinkContainingPoint(new[] { edge }));
        }

        [TestMethod]
        public void NearestLinkContainingPointIgnoresLinksOutsideRadiusAndNonLinks()
        {
            HitTestResult outside = Link(1.01);
            HitTestResult circle = new(new StubLocation(), 0, 0, 0.0);

            Assert.IsNull(HitTestResultExtensions.NearestLocationLinkContainingPoint(new[] { outside, circle }));
            Assert.IsNull(HitTestResultExtensions.NearestLocationLinkContainingPoint(new List<HitTestResult>()));
        }

        private static HitTestResult Link(double distance) => new(new StubLocationLink(), 0, 0, distance);

        private sealed class StubLocation : IHitTesting, IViewLocation
        {
            public Rectangle BoundingBox => default;

            public long ID => 1;

            public bool Contains(Vector2 Position) => true;
        }

        private sealed class StubLocationLink : IHitTesting, IViewLocationLink
        {
            public Rectangle BoundingBox => default;

            public LocationLinkKey Key => new(1, 2);

            public bool Contains(Vector2 Position) => true;
        }
    }
}
