using AnnotationService.Types;
using SqlGeometryUtils;
using Viking.AnnotationServiceTypes.Interfaces;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using WebAnnotationModel;

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

        private static LocationObj NewPolygonLocation()
        {
            SqlGeometry shape = Square(10);
            Location data = new()
            {
                ID = 1,
                TypeCode = (short)LocationType.POLYGON,
                Section = 1,
                MosaicShapeWKB = shape.AsBinary(),
                VolumeShapeWKB = shape.AsBinary()
            };
            return new LocationObj(data);
        }

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

            location.MosaicShape = Square(40);

            CollectionAssert.AreEqual(new[] { nameof(LocationObj.MosaicShape) }, geometryChanges);
            Assert.AreNotEqual(oldRadius, radiusWhenShapeChanged, "The views built for the change read the old radius.");
            Assert.AreEqual(location.Radius, radiusWhenShapeChanged, 1e-9);
        }

        [TestMethod]
        public void StoreUpdateNotifiesOnlyChangedNamedProperties()
        {
            LocationObj location = NewPolygonLocation();
            Location current = location.GetData();
            current.Terminal = false;
            current.AttributesXml = null;

            SqlGeometry newShape = Square(40);
            Location newdata = new()
            {
                ID = location.ID,
                TypeCode = current.TypeCode,
                Section = current.Section,
                MosaicShapeWKB = newShape.AsBinary(),
                VolumeShapeWKB = newShape.AsBinary(),
                Width = current.Width,
                Terminal = true,
                OffEdge = current.OffEdge,
                Closed = current.Closed,
                ParentID = current.ParentID,
                AttributesXml = "<Attributes><Attribute Name=\"Tag\" Value=\"1\" /></Attributes>",
                Position = current.Position,
                VolumePosition = current.VolumePosition,
                Radius = current.Radius,
            };

            List<string> changed = [];
            location.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            location.Update(newdata);

            CollectionAssert.AreEquivalent(
                new[] { nameof(LocationObj.MosaicShape), nameof(LocationObj.Terminal), nameof(LocationObj.Attributes) },
                changed);
            Assert.IsFalse(changed.Contains(""));
            Assert.IsFalse(changed.Contains(nameof(LocationObj.Radius)));
            Assert.IsFalse(changed.Contains(nameof(LocationObj.Width)));
        }

        [TestMethod]
        public void UnchangedStoreUpdateStaysQuiet()
        {
            LocationObj location = NewPolygonLocation();
            Location current = location.GetData();
            Location newdata = new()
            {
                ID = location.ID,
                TypeCode = current.TypeCode,
                Section = current.Section,
                MosaicShapeWKB = current.MosaicShapeWKB,
                VolumeShapeWKB = current.VolumeShapeWKB,
                Width = current.Width,
                Terminal = current.Terminal,
                OffEdge = current.OffEdge,
                Closed = current.Closed,
                ParentID = current.ParentID,
                AttributesXml = current.AttributesXml,
                Position = current.Position,
                VolumePosition = current.VolumePosition,
                Radius = current.Radius,
                Username = current.Username,
                LastModified = current.LastModified,
                Links = current.Links,
            };

            int count = 0;
            location.PropertyChanged += (_, _) => count++;
            location.Update(newdata);
            Assert.AreEqual(0, count);
        }
    }
}
