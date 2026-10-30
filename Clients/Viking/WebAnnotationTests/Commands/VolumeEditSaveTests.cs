using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation;
using WebAnnotation.UI.Commands;
using WebAnnotation.ViewModel;
using WebAnnotationModel.Objects;
using WebAnnotationTests.Tools;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Every command that edits an annotation on screen saves a mosaic shape whose forward mapping is the geometry the user
    /// saw (<c>.cursor/rules/annotation-volume-geometry.mdc</c>): the edit is mapped back by the inverse owner and the
    /// forward owner draws it again. Checked under a pure scale, an affine rotate/scale/shear and a non-linear warp.
    /// </summary>
    [TestClass]
    public class VolumeEditSaveTests
    {
        private const double Tolerance = 1e-6;

        internal static readonly (string Name, IVolumeToSectionTransform Transform)[] Transforms =
        [
            ("scale 2", new ScaleTransform(2, new Vector2(300, -100))),
            ("rotate, scale and shear", AffineTransform.RotateScaleShear()),
            ("non-linear warp", new WarpTransform()),
        ];

        [TestInitialize]
        public void OpenAVolumeForGlobal() => SegmentationServiceSessionTests.EnsureVolumeCachePathAvailable();

        internal static SqlGeometry Wkt(string wkt) => SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(wkt), 0);

        internal static void AssertSamePoints(IReadOnlyList<Vector2> expected, IReadOnlyList<Vector2> actual, string because)
        {
            Assert.AreEqual(expected.Count, actual.Count, because);
            for (int i = 0; i < expected.Count; i++)
                Assert.AreEqual(0, Vector2.Distance(expected[i], actual[i]), Tolerance * Math.Max(1, Math.Abs(expected[i].X)), $"{because}: point {i}");
        }

        /// <summary>
        /// Translate a line or curve: the command moves the control points the forward owner draws, and the save maps them back,
        /// so the mosaic moves by the inverse of the volume drag and the width drawn stays the width shown. The old save
        /// copied the volume width into the mosaic Width (24 instead of 12 at scale 2), so the line was drawn twice as wide.
        /// </summary>
        [TestMethod]
        public void TranslatingALineMovesItsMosaicByTheInverseOfTheDragAndKeepsTheDrawnWidth()
        {
            foreach (LocationType type in new[] { LocationType.POLYLINE, LocationType.OPENCURVE, LocationType.CLOSEDCURVE })
            {
                foreach (var (name, transform) in Transforms)
                {
                    LocationObj location = LocationVolumeGeometryTests.NewLocation(type, Wkt("LINESTRING (1800 2800, 1950 3050, 2100 2900, 2300 3150)"), width: 12);
                    LocationVolumeGeometry before = LocationVolumeGeometry.For(location, transform);
                    Vector2 drag = new(240, -130);
                    Vector2[] moved = [.. before.ControlPoints.Select(p => p + drag)];

                    Assert.IsTrue(LocationActions.SaveVolumeLineEdit(location, transform, moved, before.Width, out string failure), failure);

                    LocationVolumeGeometry after = LocationVolumeGeometry.For(location, transform);
                    AssertSamePoints(moved, after.ControlPoints, $"{type} {name}");
                    AssertSamePoints(transform.VolumeToSection(moved), LocationVolumeGeometry.ToSqlGeometry(location.MosaicShape).ToPoints(), $"{type} {name}: mosaic is the drag mapped back");
                    Assert.AreEqual(before.Width, after.Width, 1e-6 * before.Width, $"{type} {name}: drawn width");
                }
            }
        }

        [TestMethod]
        public void ALineEditWithAPointOutsideTheTransformIsRefusedAndNothingIsSaved()
        {
            IVolumeToSectionTransform transform = new ClippedTransform(new ScaleTransform(1, Vector2.Zero), new Rectangle(0, 5000, 0, 5000));
            LocationObj location = LocationVolumeGeometryTests.NewLocation(LocationType.OPENCURVE, Wkt("LINESTRING (1800 2800, 1950 3050, 2100 2900)"), width: 12);
            IShape2D before = location.MosaicShape;

            Assert.IsFalse(LocationActions.SaveVolumeLineEdit(location, transform, [new(1800, 2800), new(5200, 3050), new(2100, 2900)], 30, out string failure));

            StringAssert.Contains(failure, "1 of 3 points");
            Assert.AreSame(before, location.MosaicShape);
            Assert.AreEqual(12, location.Width);
        }

        /// <summary>
        /// Translate a polygon (mosaic-first): the mosaic polygon moves by the volume drag mapped back, so the point the user
        /// grabbed is drawn under the cursor wherever the warp is.
        /// </summary>
        [TestMethod]
        public void MovingAPolygonMovesTheGrabbedMosaicPointUnderTheCursor()
        {
            foreach (var (name, transform) in Transforms)
            {
                Vector2 grabbed = new(2500, 2600), cursor = new(3100, 2250);

                Assert.IsTrue(TranslateScaleCommandBase.TryMosaicDelta(transform, grabbed, cursor, out Vector2 mosaicDelta));

                Vector2 movedGrab = transform.SectionToVolume(transform.VolumeToSection(grabbed) + mosaicDelta);
                Assert.AreEqual(0, Vector2.Distance(cursor, movedGrab), 1e-6, name);
            }

            IVolumeToSectionTransform clipped = new ClippedTransform(new ScaleTransform(1, Vector2.Zero), new Rectangle(0, 5000, 0, 5000));
            Assert.IsFalse(TranslateScaleCommandBase.TryMosaicDelta(clipped, new Vector2(100, 100), new Vector2(6000, 100), out _), "a drag off the transform does not move the mosaic");
        }

        /// <summary>
        /// Change Shape > Circle: the circle is the old shape's mosaic centre and equal-area radius, drawn by the forward owner. The old
        /// path moved the old mosaic and the stored (stale) volume shapes to the volume centre instead, so the "circle" kept
        /// its polygon outline.
        /// </summary>
        [TestMethod]
        public void ChangeShapeToCircleBuildsTheCircleFromTheMosaicCentreAndRadius()
        {
            IVolumeToSectionTransform transform = AffineTransform.RotateScaleShear();
            LocationObj location = LocationVolumeGeometryTests.NewLocation(LocationType.POLYGON,
                Wkt("POLYGON ((1800 2800, 2300 2800, 2300 3200, 1800 3200, 1800 2800))"));
            location.VolumeShape = Wkt("POLYGON ((0 0, 50 0, 50 50, 0 50, 0 0))").ToShape2D();
            Vector2 mosaicCenter = location.Position;
            // The equal-area radius; dev's LocationObj.Radius of a polygon is the square root of its area.
            double mosaicRadius = Math.Sqrt(500.0 * 400.0 / Math.PI);

            Func<IVolumeTransformProvider?> savedProvider = LocationVolumeGeometryExtensions.Provider;
            LocationVolumeGeometryExtensions.Provider = () => new SingleTransformProvider(transform);
            try
            {
                ConvertProbe.For(location).Convert(LocationType.CIRCLE);
            }
            finally
            {
                LocationVolumeGeometryExtensions.Provider = savedProvider;
            }

            Assert.AreEqual(LocationType.CIRCLE, location.TypeCode);
            Assert.IsInstanceOfType(location.MosaicShape, typeof(Circle));
            Assert.AreEqual(0, Vector2.Distance(mosaicCenter, location.Position), 0.01);
            Assert.AreEqual(mosaicRadius, location.Radius, 0.01);
            Circle drawn = LocationVolumeGeometry.For(location, transform).Circle!.Value;
            Assert.AreEqual(drawn.Radius, ((ICircle2D)location.VolumeShape).Radius, 0.01, "the volume shape is the forward owner's circle");
        }

        internal sealed class SingleTransformProvider(IVolumeToSectionTransform transform) : IVolumeTransformProvider
        {
            public IVolumeToSectionTransform GetSectionToVolumeTransform(int SectionNumber) => transform;
        }

        /// <summary>Calls the protected Change Shape handler on a view model built without a store.</summary>
        private sealed class ConvertProbe : Location_ViewModelBase
        {
            private ConvertProbe() : base(0)
            {
            }

            public static ConvertProbe For(LocationObj location)
            {
                ConvertProbe probe = (ConvertProbe)FormatterServices.GetUninitializedObject(typeof(ConvertProbe));
                typeof(Location_ViewModelBase).GetField(nameof(modelObj))!.SetValue(probe, location);
                return probe;
            }

            public void Convert(LocationType target) => ContextMenu_ConvertShape(new ToolStripMenuItem { Tag = target }, EventArgs.Empty);
        }
    }
}
