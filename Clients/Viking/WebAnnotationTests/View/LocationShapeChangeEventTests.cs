using SqlGeometryUtils;
using Viking.AnnotationServiceTypes.Interfaces;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests.View
{
    /// <summary>
    /// A shape edit must announce one geometry change, because every geometry change makes a section rebuild the
    /// location's views, which smooths and triangulates the polygon.
    /// </summary>
    [TestClass]
    public class LocationShapeChangeEventTests
    {
        private static SqlGeometry Square(double side)
        {
            return SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(
                FormattableString(side)), 0);
        }

        private static string FormattableString(double side) =>
            System.FormattableString.Invariant($"POLYGON((0 0, {side} 0, {side} {side}, 0 {side}, 0 0))");

        private static LocationObj NewPolygonLocation() =>
            TestModel.Location(1, 0, LocationType.POLYGON, FormattableString(10));

        [TestMethod]
        public void RadiusIsNotAGeometryProperty()
        {
            Assert.IsTrue(LocationObj.IsGeometryProperty(nameof(LocationObj.MosaicShape)));
            Assert.IsFalse(LocationObj.IsGeometryProperty(nameof(LocationObj.Radius)));
        }

        [TestMethod]
        public void ShapeEditRaisesOneGeometryChangeAndRadiusIsCurrentWhenItIsAnnounced()
        {
            LocationObj location = NewPolygonLocation();
            double oldRadius = location.Radius;

            List<string> geometryChanges = [];
            double radiusWhenShapeChanged = double.NaN;
            location.PropertyChanged += (sender, e) =>
            {
                if (LocationObj.IsGeometryProperty(e.PropertyName))
                    geometryChanges.Add(e.PropertyName);
                if (e.PropertyName == nameof(LocationObj.MosaicShape))
                    radiusWhenShapeChanged = location.Radius;
            };

            location.MosaicShape = Square(40).ToShape2D();

            CollectionAssert.AreEqual(new[] { nameof(LocationObj.MosaicShape) }, geometryChanges);
            Assert.AreNotEqual(oldRadius, radiusWhenShapeChanged, "The views built for the change read the old radius.");
            Assert.AreEqual(location.Radius, radiusWhenShapeChanged, 1e-9);
        }
    }
}
