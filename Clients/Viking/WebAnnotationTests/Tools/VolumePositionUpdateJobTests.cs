using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using System;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests.Tools
{
    /// <summary>
    /// Update Volume Positions (<see cref="VolumePositionUpdateJob"/>), the refresh after a .stos change, writes exactly the
    /// cache a client save writes: <see cref="VolumeCache.Shape"/> under the volume's default transform.
    /// </summary>
    [TestClass]
    public class VolumePositionUpdateJobTests
    {
        /// <summary>The default transform; it shears, so a smoothed circle ring is visibly smaller than the circle.</summary>
        private static readonly IVolumeToSectionTransform DefaultTransform = AffineTransform.RotateScaleShear();

        /// <summary>The transform the user edits under in the viewer.</summary>
        private static readonly IVolumeToSectionTransform ActiveTransform = new ScaleTransform(1.25, new Vector2(500, -200));

        private static readonly LocationType[] Types =
        [
            LocationType.CIRCLE, LocationType.POINT, LocationType.POLYGON, LocationType.CURVEPOLYGON,
            LocationType.POLYLINE, LocationType.OPENCURVE, LocationType.CLOSEDCURVE,
        ];

        private Func<IDefaultVolumeTransformProvider?> savedDefaults = VolumeCache.DefaultTransforms;

        [TestInitialize]
        public void OpenAVolumeWithADefaultTransform()
        {
            savedDefaults = VolumeCache.DefaultTransforms;
            VolumeCache.DefaultTransforms = () => new VolumeCacheTests.OneDefault(DefaultTransform);
        }

        [TestCleanup]
        public void CloseTheVolume() => VolumeCache.DefaultTransforms = savedDefaults;

        private static void AssertSamePoints(SqlGeometry expected, SqlGeometry actual, string because)
        {
            Vector2[] e = expected.ToPoints(), a = actual.ToPoints();
            Assert.AreEqual(e.Length, a.Length, because);
            for (int i = 0; i < e.Length; i++)
                Assert.AreEqual(0, Vector2.Distance(e[i], a[i]), 1e-9 * Math.Max(1, Math.Abs(e[i].X)), $"{because}: point {i}");
        }

        /// <summary>
        /// For every type, circles included, the job's cache is the client save's. The old job wrote the smoothed shape, which
        /// made a circle the inscribed circle of its ring polygon, about 2% smaller than the circle the client saved.
        /// </summary>
        [TestMethod]
        public void TheJobWritesWhatTheClientSaveWritesForEveryType()
        {
            foreach (LocationType type in Types)
            {
                LocationObj saved = LocationVolumeGeometryTests.Fixture(type);
                saved.SetShapeFromGeometryInSection(ActiveTransform, LocationVolumeGeometry.ToSqlGeometry(saved.MosaicShape));

                LocationObj refreshed = LocationVolumeGeometryTests.Fixture(type);
                Assert.IsTrue(VolumePositionUpdateJob.TryUpdateVolumeShape(refreshed, DefaultTransform), $"{type}: updated");

                AssertSamePoints(LocationVolumeGeometry.ToSqlGeometry(saved.VolumeShape), LocationVolumeGeometry.ToSqlGeometry(refreshed.VolumeShape), $"{type}: the job writes what the save writes");
                Assert.IsFalse(VolumePositionUpdateJob.TryUpdateVolumeShape(refreshed, DefaultTransform), $"{type}: a second pass changes nothing");
            }
        }

        /// <summary>
        /// A location whose type does not match its mosaic shape is repaired through the owner, so its cache comes from the
        /// repaired shape.
        /// </summary>
        [TestMethod]
        public void ARepairedShapeIsSavedThroughTheOwner()
        {
            LocationObj location = LocationVolumeGeometryTests.NewLocation(LocationType.POLYGON,
                SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars("LINESTRING (1800 2800, 2300 2800, 2300 3200, 1800 3200)"), 0));

            Assert.IsTrue(VolumePositionUpdateJob.TryUpdateVolumeShape(location, DefaultTransform));

            Assert.AreEqual(SupportedGeometryType.POLYGON, LocationVolumeGeometry.ToSqlGeometry(location.MosaicShape).GeometryType());
            AssertSamePoints(LocationVolumeGeometry.For(location, DefaultTransform).CacheShape, LocationVolumeGeometry.ToSqlGeometry(location.VolumeShape), "cache of the repaired shape");
        }
    }
}
