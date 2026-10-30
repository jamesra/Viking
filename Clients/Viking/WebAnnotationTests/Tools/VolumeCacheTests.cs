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
using WebAnnotationModel.Objects;

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
            public IVolumeToSectionTransform GetDefaultSectionToVolumeTransform(int SectionNumber) => transform;
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
                location.SetShapeFromGeometryInSection(ActiveTransform, LocationVolumeGeometry.ToSqlGeometry(location.MosaicShape));

                AssertSamePoints(LocationVolumeGeometry.For(location, DefaultTransform).CacheShape, LocationVolumeGeometry.ToSqlGeometry(location.VolumeShape), $"{type}: default");
                Assert.IsFalse(LocationVolumeGeometry.ToSqlGeometry(location.VolumeShape).STEquals(LocationVolumeGeometry.For(location, ActiveTransform).CacheShape).IsTrue, $"{type}: not the active transform's");
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
            AssertSamePoints(SqlGeometryUtils.Extensions.ToCircle(cached.Center.X, cached.Center.Y, 0, cached.Radius), LocationVolumeGeometry.ToSqlGeometry(circle.VolumeShape), "cached under the default");
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

            IVolumeToSectionTransform grid = model.GetDefaultSectionToVolumeTransform(5);
            Assert.AreSame(model.GetSectionToVolumeTransform(5), grid, "the default is active: the same object");

            model.ActiveVolumeTransform = "Rigid";
            Assert.AreNotSame(grid, model.GetSectionToVolumeTransform(5), "another transform is active");
            Assert.AreSame(grid, model.GetDefaultSectionToVolumeTransform(5), "the default is unchanged");

            IVolumeToSectionTransform reference = model.GetDefaultSectionToVolumeTransform(1);
            Assert.AreEqual(new Vector2(12, 34), reference.SectionToVolume(new Vector2(12, 34)), "no entry: identity");

            volume.DefaultVolumeTransform = "Not loaded";
            Assert.AreEqual(new Vector2(12, 34), model.GetDefaultSectionToVolumeTransform(5).SectionToVolume(new Vector2(12, 34)), "no such group: identity");
        }
    }
}
