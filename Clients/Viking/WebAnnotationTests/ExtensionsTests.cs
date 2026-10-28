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
    /// Pins the pure helpers in <c>WebAnnotation/Extensions.cs</c>: hotkey modifier text, modifier
    /// key checks (flag tests: Ctrl+Shift counts as both, Alt is ignored), the user-settings
    /// annotation-type-name mapping, and nearest-location-link selection.
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

        /// <summary>
        /// Every Shift/Ctrl/Alt combination: Shift and Ctrl are read as flags and Alt never changes
        /// the answer, so holding Alt, or Ctrl and Shift together, does not cancel a modifier.
        /// </summary>
        [TestMethod]
        public void ModifierChecksAreFlagTestsAndIgnoreAlt()
        {
            foreach (bool shift in new[] { false, true })
            {
                foreach (bool ctrl in new[] { false, true })
                {
                    foreach (bool alt in new[] { false, true })
                    {
                        Keys keys = (shift ? Keys.Shift : Keys.None) |
                                    (ctrl ? Keys.Control : Keys.None) |
                                    (alt ? Keys.Alt : Keys.None);

                        Assert.AreEqual(shift, keys.ShiftPressed(), $"ShiftPressed({keys})");
                        Assert.AreEqual(ctrl, keys.CtrlPressed(), $"CtrlPressed({keys})");
                        Assert.AreEqual(shift || ctrl, keys.ShiftOrCtrlPressed(), $"ShiftOrCtrlPressed({keys})");
                    }
                }
            }
        }

        [TestMethod]
        [DataRow(Keys.Shift | Keys.Control, true, true, DisplayName = "Ctrl+Shift is both")]
        [DataRow(Keys.Shift | Keys.Alt, true, false, DisplayName = "Shift+Alt is Shift")]
        [DataRow(Keys.Control | Keys.Alt, false, true, DisplayName = "Ctrl+Alt is Ctrl")]
        [DataRow(Keys.Alt, false, false, DisplayName = "Alt alone is neither")]
        public void CombinedModifiers(Keys keys, bool shift, bool ctrl)
        {
            Assert.AreEqual(shift, keys.ShiftPressed());
            Assert.AreEqual(ctrl, keys.CtrlPressed());
            Assert.AreEqual(shift || ctrl, keys.ShiftOrCtrlPressed());
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
