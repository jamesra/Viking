using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.ViewModels;
using Viking.VolumeModel;
using WebAnnotationModel;

namespace WebAnnotationTests.Tools
{
    /// <summary>
    /// The stored volume cache is always written under the volume's default transform (<see cref="VolumeCache"/>), whatever
    /// transform is on screen, so server queries, OData and export see one space. See
    /// <c>.cursor/rules/annotation-volume-geometry.mdc</c>.
    /// </summary>
    [TestClass]
    public class VolumeCacheTests
    {
        private const double Tolerance = 1e-6;

        /// <summary>The volume's default transform in these tests.</summary>
        private static readonly IVolumeToSectionTransform DefaultTransform = new ScaleTransform(2, new Vector2(300, -100));

        /// <summary>A different transform the user has chosen to view the volume in.</summary>
        private static readonly IVolumeToSectionTransform ActiveTransform = AffineTransform.RotateScaleShear();

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
            VolumeCache.DefaultTransforms = () => new OneDefault(DefaultTransform);
        }

        [TestCleanup]
        public void CloseTheVolume() => VolumeCache.DefaultTransforms = savedDefaults;

        internal sealed class OneDefault(IVolumeToSectionTransform transform) : IDefaultVolumeTransformProvider
        {
            public IVolumeToSectionTransform? GetDefaultSectionToVolumeTransform(int SectionNumber) => transform;
        }

        private static void AssertSamePoints(SqlGeometry expected, SqlGeometry actual, string because)
        {
            Vector2[] e = expected.ToPoints(), a = actual.ToPoints();
            Assert.AreEqual(e.Length, a.Length, because);
            for (int i = 0; i < e.Length; i++)
                Assert.AreEqual(0, Vector2.Distance(e[i], a[i]), Tolerance * Math.Max(1, Math.Abs(e[i].X)), $"{because}: point {i}");
        }

        /// <summary>
        /// Saving a mosaic shape while another transform is on screen writes the cache the default transform gives. The old
        /// save wrote the cache under the transform on screen, so the stored volume positions moved whenever someone edited
        /// while viewing another alignment.
        /// </summary>
        [TestMethod]
        public void ASaveUnderAnotherActiveTransformWritesTheCacheUnderTheDefault()
        {
            foreach (LocationType type in Types)
            {
                LocationObj location = LocationVolumeGeometryTests.Fixture(type);
                location.SetShapeFromGeometryInSection(ActiveTransform, location.MosaicShape);

                AssertSamePoints(LocationVolumeGeometry.For(location, DefaultTransform).CacheShape, location.VolumeShape, $"{type}: default");
                Assert.IsFalse(location.VolumeShape.STEquals(LocationVolumeGeometry.For(location, ActiveTransform).CacheShape).IsTrue, $"{type}: not the active transform's");
            }
        }

        /// <summary>
        /// An edit made on screen is mapped back with the transform on screen (the user saw it there), and the cache is then
        /// derived under the default one.
        /// </summary>
        [TestMethod]
        public void AnEditMappedBackUnderTheActiveTransformCachesUnderTheDefault()
        {
            LocationObj circle = LocationVolumeGeometryTests.Fixture(LocationType.CIRCLE);
            Circle shown = LocationVolumeGeometry.For(circle, ActiveTransform).Circle!.Value;
            Circle moved = new(shown.Center + new Vector2(40, -25), shown.Radius * 1.1);

            Assert.IsTrue(circle.TryApply(LocationVolumeGeometry.ToMosaic(moved, ActiveTransform), ActiveTransform));

            Circle cached = LocationVolumeGeometry.For(circle, DefaultTransform).Circle!.Value;
            AssertSamePoints(SqlGeometryUtils.Extensions.ToCircle(cached.Center.X, cached.Center.Y, 0, cached.Radius), circle.VolumeShape, "cached under the default");
        }

        /// <summary>
        /// The cache of a circle is the owner's exact circle; the smoothed <see cref="LocationVolumeGeometry.Shape"/> is the
        /// inscribed circle of its polygon, about 2% smaller.
        /// </summary>
        [TestMethod]
        public void ACircleIsCachedAsTheOwnersExactCircle()
        {
            LocationObj circle = LocationVolumeGeometryTests.Fixture(LocationType.CIRCLE);
            LocationVolumeGeometry geometry = LocationVolumeGeometry.For(circle, DefaultTransform);

            Circle exact = geometry.Circle!.Value;
            AssertSamePoints(SqlGeometryUtils.Extensions.ToCircle(exact.Center.X, exact.Center.Y, 0, exact.Radius), geometry.CacheShape, "the owner's circle");
            Assert.IsTrue(geometry.Shape.ToCircle().Radius < exact.Radius * 0.99, "the smoothed shape is the smaller inscribed circle");
        }

        /// <summary>
        /// Simplify Polygon and removing a hole (polygon context menu) edit the mosaic shape and save it through the owner, so
        /// the stored volume shape is derived again under the default transform. They used to write <c>MosaicShape</c>
        /// directly, which left the cache describing the old polygon.
        /// </summary>
        [TestMethod]
        public void SimplifyPolygonAndRemoveHoleDeriveTheCacheAgain()
        {
            SegmentationServiceSessionTests.EnsureVolumeCachePathAvailable(); // Global's settings

            // A densely sampled round outline, which Simplify Polygon thins, around a square hole.
            Vector2[] ring = new Vector2[65];
            for (int i = 0; i < 64; i++)
                ring[i] = new Vector2(2050 + (250 * Math.Cos(i * Math.PI / 32)), 3000 + (250 * Math.Sin(i * Math.PI / 32)));
            ring[64] = ring[0];
            Vector2[] hole = [new(2000, 2950), new(2100, 2950), new(2100, 3050), new(2000, 3050), new(2000, 2950)];
            LocationObj location = LocationVolumeGeometryTests.NewLocation(LocationType.POLYGON, ring.ToPolygon([hole]));
            location.SetShapeFromGeometryInSection(ActiveTransform, location.MosaicShape);
            int exteriorBefore = location.MosaicShape.ToPolygon().ExteriorRing.Length;

            Assert.IsTrue(WebAnnotation.ViewModel.Location_ViewModelBase.TrySimplifyPolygon(location, null, ActiveTransform));
            Assert.IsTrue(location.MosaicShape.ToPolygon().ExteriorRing.Length < exteriorBefore, "the near-collinear vertex is removed");
            AssertSamePoints(LocationVolumeGeometry.For(location, DefaultTransform).CacheShape, location.VolumeShape, "after Simplify Polygon");

            Assert.IsTrue(WebAnnotation.ViewModel.Location_ViewModelBase.TryRemoveInnerPolygon(location, 0, ActiveTransform));
            Assert.IsFalse(location.MosaicShape.ToPolygon().HasInteriorRings, "the hole is removed");
            AssertSamePoints(LocationVolumeGeometry.For(location, DefaultTransform).CacheShape, location.VolumeShape, "after removing the hole");
        }

        /// <summary>
        /// <see cref="VolumeViewModel.GetDefaultSectionToVolumeTransform"/> is the one helper that gives the default transform:
        /// the default group's .stos while another group is active, the active transform's own object while the default is
        /// active, and identity for a section the default has no entry for (the reference section) or a volume without it.
        /// </summary>
        [TestMethod]
        public void TheVolumeViewModelGivesTheDefaultTransformWhateverIsActive()
        {
            Volume volume = (Volume)FormatterServices.GetUninitializedObject(typeof(Volume));
            volume.Sections = [];
            volume.Transforms = new SortedList<string, SortedList<int, ITransform>>
            {
                ["Grid"] = new() { [5] = new Geometry.Transforms.IdentityTransform() },
                ["Rigid"] = new() { [5] = new Geometry.Transforms.IdentityTransform() },
            };
            volume.DefaultVolumeTransform = "Grid";
            VolumeViewModel model = new(volume);

            IVolumeToSectionTransform grid = model.GetDefaultSectionToVolumeTransform(5)!;
            Assert.AreSame(model.GetSectionToVolumeTransform(5), grid, "the default is active: the same object");

            model.ActiveVolumeTransform = "Rigid";
            Assert.AreNotSame(grid, model.GetSectionToVolumeTransform(5), "another transform is active");
            Assert.AreSame(grid, model.GetDefaultSectionToVolumeTransform(5), "the default is unchanged");

            IVolumeToSectionTransform reference = model.GetDefaultSectionToVolumeTransform(1)!;
            Assert.AreEqual(new Vector2(12, 34), reference.SectionToVolume(new Vector2(12, 34)), "no entry: identity");

            volume.DefaultVolumeTransform = "Not loaded";
            Assert.IsNull(model.GetDefaultSectionToVolumeTransform(5), "a default that did not load while others did: none, not identity");

            volume.Transforms.Clear();
            Assert.AreEqual(new Vector2(12, 34), model.GetDefaultSectionToVolumeTransform(5)!.SectionToVolume(new Vector2(12, 34)), "a volume without transforms: identity");
        }
    }
}
