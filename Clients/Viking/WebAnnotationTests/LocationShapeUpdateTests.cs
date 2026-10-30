using Viking.AnnotationServiceTypes.Interfaces;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Viking.VolumeModel;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using WebAnnotationTests.Actions;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins <see cref="LocationShapeUpdate"/>: argument guards, the conversion to CURVEPOLYGON,
    /// and interior-ring handling. Only the no-save paths are exercised; the save path needs the WCF store.
    /// </summary>
    [TestClass]
    public class LocationShapeUpdateTests
    {
        /// <summary>
        /// Simplification reads the pen threshold from WebAnnotation's Global, whose type initializer needs a volume name.
        /// </summary>
        [TestInitialize]
        public void EnsureGlobalCanInitialize() => SegmentationServiceSessionTests.EnsureVolumeCachePathAvailable();

        private static Vector2[] Square(double min, double max) =>
            [new(min, min), new(max, min), new(max, max), new(min, max), new(min, min)];

        private static LocationObj PolylineLocation() =>
            ChangeTypeActionTests.NewLocation(LocationType.POLYLINE, "LINESTRING(10 10, 50 50, 90 10)");

        [TestMethod]
        public void AssignThrowsOnNullLocation()
        {
            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                LocationShapeUpdate.AssignSimplifiedVolumePolygon(null, new Polygon(Square(20, 180)), ChangeTypeActionTests.CreateBoundedTransform(), 1));
            Assert.AreEqual("modelObj", ex.ParamName);
        }

        [TestMethod]
        public void AssignThrowsOnNullPolygon()
        {
            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                LocationShapeUpdate.AssignSimplifiedVolumePolygon(PolylineLocation(), null, ChangeTypeActionTests.CreateBoundedTransform(), 1));
            Assert.AreEqual("volumePolygon", ex.ParamName);
        }

        [TestMethod]
        public void AssignThrowsOnNullTransformAndLeavesLocationUnchanged()
        {
            LocationObj location = PolylineLocation();
            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                LocationShapeUpdate.AssignSimplifiedVolumePolygon(location, new Polygon(Square(20, 180)), null, 1));
            Assert.AreEqual("transform", ex.ParamName);
            Assert.AreEqual(LocationType.POLYLINE, location.TypeCode);
        }

        [TestMethod]
        public void ApplyReturnsFalseForNullLocation() =>
            Assert.IsFalse(LocationShapeUpdate.ApplyVolumePolygon(null, new Polygon(Square(20, 180)), null));

        [TestMethod]
        public void ApplyReturnsFalseForNullPolygonWithoutChangingLocation()
        {
            LocationObj location = PolylineLocation();
            Assert.IsFalse(LocationShapeUpdate.ApplyVolumePolygon(location, null, null));
            Assert.AreEqual(LocationType.POLYLINE, location.TypeCode);
        }

        [TestMethod]
        public void ApplyReturnsFalseForNullParentWithoutChangingLocation()
        {
            LocationObj location = PolylineLocation();
            Assert.IsFalse(LocationShapeUpdate.ApplyVolumePolygon(location, new Polygon(Square(20, 180)), null));
            Assert.AreEqual(LocationType.POLYLINE, location.TypeCode);
        }

        [TestMethod]
        public void AssignConvertsLocationToCurvePolygonWithMosaicAndVolumeShapes()
        {
            LocationObj location = PolylineLocation();

            LocationShapeUpdate.AssignSimplifiedVolumePolygon(location, new Polygon(Square(20, 180)), ChangeTypeActionTests.CreateBoundedTransform(), 1);

            Assert.AreEqual(LocationType.CURVEPOLYGON, location.TypeCode);
            Assert.IsNotNull(location.MosaicShape);
            Assert.IsNotNull(location.VolumeShape);
            Assert.AreEqual(0, location.MosaicShape.STNumInteriorRing().Value);
            Assert.AreEqual(0, location.VolumeShape.STNumInteriorRing().Value);
        }

        [TestMethod]
        public void AssignMapsExteriorThroughTransformIntoMosaicBounds()
        {
            LocationObj location = PolylineLocation();

            LocationShapeUpdate.AssignSimplifiedVolumePolygon(location, new Polygon(Square(20, 180)), ChangeTypeActionTests.CreateBoundedTransform(), 1);

            // The grid transform is the identity, so the mosaic envelope is the simplified square itself.
            Microsoft.SqlServer.Types.SqlGeometry envelope = location.MosaicShape.STEnvelope();
            Assert.AreEqual(20, envelope.STPointN(1).STX.Value, 1e-6);
            Assert.AreEqual(20, envelope.STPointN(1).STY.Value, 1e-6);
            Assert.AreEqual(180, envelope.STPointN(3).STX.Value, 1e-6);
            Assert.AreEqual(180, envelope.STPointN(3).STY.Value, 1e-6);
        }

        [TestMethod]
        public void AssignCarriesInteriorRingIntoBothShapes()
        {
            LocationObj location = PolylineLocation();
            Polygon withHole = new(Square(20, 180));
            withHole.AddInteriorRing(Square(80, 120));

            LocationShapeUpdate.AssignSimplifiedVolumePolygon(location, withHole, ChangeTypeActionTests.CreateBoundedTransform(), 1);

            Assert.AreEqual(LocationType.CURVEPOLYGON, location.TypeCode);
            Assert.AreEqual(1, location.MosaicShape.STNumInteriorRing().Value);
            Assert.AreEqual(1, location.VolumeShape.STNumInteriorRing().Value);
        }

        [TestMethod]
        public void AssignSimplifiesVertexDenseExteriorBeforeSaving()
        {
            // 160 collinear vertices per edge of a 20..180 square: simplification must drop nearly all of them.
            List<Vector2> dense = new();
            for (int i = 0; i < 160; i++) dense.Add(new Vector2(20 + i, 20));
            for (int i = 0; i < 160; i++) dense.Add(new Vector2(180, 20 + i));
            for (int i = 0; i < 160; i++) dense.Add(new Vector2(180 - i, 180));
            for (int i = 0; i < 160; i++) dense.Add(new Vector2(20, 180 - i));
            dense.Add(new Vector2(20, 20));
            LocationObj location = PolylineLocation();
            IVolumeToSectionTransform transform = ChangeTypeActionTests.CreateBoundedTransform();

            LocationShapeUpdate.AssignSimplifiedVolumePolygon(location, new Polygon(dense.ToArray()), transform, 1);

            Assert.IsTrue(location.MosaicShape.STNumPoints().Value < dense.Count / 10, "mosaic points: " + location.MosaicShape.STNumPoints().Value);
        }

        [TestMethod]
        public void AssignIgnoresDownsample()
        {
            LocationObj slow = PolylineLocation();
            LocationObj fast = PolylineLocation();
            IVolumeToSectionTransform transform = ChangeTypeActionTests.CreateBoundedTransform();

            LocationShapeUpdate.AssignSimplifiedVolumePolygon(slow, new Polygon(Square(20, 180)), transform, 1);
            LocationShapeUpdate.AssignSimplifiedVolumePolygon(fast, new Polygon(Square(20, 180)), transform, 64);

            Assert.AreEqual(slow.MosaicShape.ToString(), fast.MosaicShape.ToString());
            Assert.AreEqual(slow.VolumeShape.ToString(), fast.VolumeShape.ToString());
        }

        /// <summary>
        /// Segmentation accept with an edge the section cannot map (the grid ends at 200): the result is refused with the
        /// reason and nothing is saved, the type included. The old accept changed the type first and mapped with the
        /// unchecked VolumeToSection.
        /// </summary>
        [TestMethod]
        public void AssignRefusesAPolygonWithAPointThatCannotBeMappedAndChangesNothing()
        {
            LocationObj location = PolylineLocation();
            string mosaicBefore = location.MosaicShape.ToString();
            string volumeBefore = location.VolumeShape.ToString();

            ArgumentException refused = Assert.ThrowsException<ArgumentException>(() =>
                LocationShapeUpdate.AssignSimplifiedVolumePolygon(location, new Polygon(Square(120, 260)), ChangeTypeActionTests.CreateBoundedTransform(), 1));

            StringAssert.Contains(refused.Message, "outside the region this section's transform maps");
            Assert.AreEqual(LocationType.POLYLINE, location.TypeCode);
            Assert.AreEqual(mosaicBefore, location.MosaicShape.ToString());
            Assert.AreEqual(volumeBefore, location.VolumeShape.ToString());
        }

        /// <summary>
        /// Segmentation accept under a non-linear warp: the saved mosaic control polygon is drawn by the forward owner as the
        /// simplified volume polygon that was shown, and the volume shape is the forward owner's.
        /// </summary>
        [TestMethod]
        public void AnAcceptedPolygonIsDrawnAsTheSimplifiedResult()
        {
            IVolumeToSectionTransform transform = new WebAnnotationTests.Tools.WarpTransform();
            LocationObj location = PolylineLocation();
            Polygon result = new([new(2000, 2000), new(2400, 2050), new(2450, 2400), new(2100, 2380), new(2000, 2000)]);
            Polygon shown = WebAnnotation.UI.AutoPolygonize.AutoPolygonizeSelection.SimplifyForSave(result);

            LocationShapeUpdate.AssignSimplifiedVolumePolygon(location, result, transform, 1);

            LocationVolumeGeometry drawn = LocationVolumeGeometry.For(location, transform);
            Assert.AreEqual(shown.ExteriorRing.Length, drawn.ControlPoints.Count);
            for (int i = 0; i < shown.ExteriorRing.Length; i++)
                Assert.AreEqual(0, Vector2.Distance(shown.ExteriorRing[i], drawn.ControlPoints[i]), 1e-6, $"point {i}");
            Assert.IsTrue(location.VolumeShape.STEquals(drawn.Shape).IsTrue);
        }
    }
}
