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
using WebAnnotationModel;
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
                    AssertSamePoints(transform.VolumeToSection(moved), location.MosaicShape.ToPoints(), $"{type} {name}: mosaic is the drag mapped back");
                    Assert.AreEqual(before.Width, after.Width, 1e-6 * before.Width, $"{type} {name}: drawn width");
                }
            }
        }

        [TestMethod]
        public void ALineEditWithAPointOutsideTheTransformIsRefusedAndNothingIsSaved()
        {
            IVolumeToSectionTransform transform = new ClippedTransform(new ScaleTransform(1, Vector2.Zero), new Rectangle(0, 5000, 0, 5000));
            LocationObj location = LocationVolumeGeometryTests.NewLocation(LocationType.OPENCURVE, Wkt("LINESTRING (1800 2800, 1950 3050, 2100 2900)"), width: 12);
            string before = location.MosaicShape.ToString();

            Assert.IsFalse(LocationActions.SaveVolumeLineEdit(location, transform, [new(1800, 2800), new(5200, 3050), new(2100, 2900)], 30, out string failure));

            StringAssert.Contains(failure, "1 of 3 points");
            Assert.AreEqual(before, location.MosaicShape.ToString());
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
        /// Change Shape > Circle: the circle is the old shape's mosaic centre and radius, drawn by the forward owner. The old
        /// path moved the old mosaic and the stored (stale) volume shapes to the volume centre instead, so the "circle" kept
        /// its polygon outline.
        /// </summary>
        [TestMethod]
        public void ChangeShapeToCircleBuildsTheCircleFromTheMosaicCentreAndRadius()
        {
            IVolumeToSectionTransform transform = AffineTransform.RotateScaleShear();
            LocationObj location = LocationVolumeGeometryTests.NewLocation(LocationType.POLYGON,
                Wkt("POLYGON ((1800 2800, 2300 2800, 2300 3200, 1800 3200, 1800 2800))"));
            location.VolumeShape = Wkt("POLYGON ((0 0, 50 0, 50 50, 0 50, 0 0))");
            Vector2 mosaicCenter = location.Position;
            double mosaicRadius = location.Radius;

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
            Assert.AreEqual(SupportedGeometryType.CURVEPOLYGON, location.MosaicShape.GeometryType());
            Assert.AreEqual(0, Vector2.Distance(mosaicCenter, location.Position), 0.01);
            Assert.AreEqual(mosaicRadius, location.Radius, 0.01);
            Circle drawn = LocationVolumeGeometry.For(location, transform).Circle!.Value;
            Assert.AreEqual(drawn.Radius, location.VolumeShape.ToCircle().Radius, 0.01, "the volume shape is the forward owner's circle");
        }

        /// <summary>
        /// Adjust, add or remove a control point of a line or curve: the edited volume control points are what the forward
        /// owner draws afterwards, and the mosaic width is left as it was.
        /// </summary>
        [TestMethod]
        public void AControlPointEditOfALineIsDrawnBackAsEditedAndKeepsItsWidth()
        {
            foreach (var (name, transform) in Transforms)
            {
                LocationObj location = LocationVolumeGeometryTests.NewLocation(LocationType.OPENCURVE, Wkt("LINESTRING (1800 2800, 1950 3050, 2100 2900, 2300 3150)"), width: 10);
                Vector2[] edited = [.. LocationVolumeGeometry.For(location, transform).ControlPoints];
                edited[2] += new Vector2(-60, 85);
                Vector2[] added = AddLineControlPointCommand.AddControlPoint(edited, (edited[0] + edited[1]) / 2 + new Vector2(5, 5), out _);

                Assert.IsTrue(LocationActions.SaveVolumeLineEdit(location, transform, added, null, out string failure), failure);

                AssertSamePoints(added, LocationVolumeGeometry.For(location, transform).ControlPoints, name);
                Assert.AreEqual(10, location.Width, $"{name}: an edit without a width keeps the mosaic width");
            }
        }

        /// <summary>
        /// Adjust, add or remove a polygon vertex: the polygon the user edited on the forward owner's control polygon is
        /// mapped back and drawn again as edited, holes included.
        /// </summary>
        [TestMethod]
        public void AVertexEditOfAPolygonIsDrawnBackAsEdited()
        {
            foreach (var (name, transform) in Transforms)
            {
                LocationObj location = LocationVolumeGeometryTests.Fixture(LocationType.CURVEPOLYGON);
                Polygon control = LocationVolumeGeometry.For(location, transform).ControlPolygon();
                Vector2[] ring = [.. control.ExteriorRing];
                ring[1] += new Vector2(40, -25);
                Polygon edited = new(ring);
                foreach (Vector2[] hole in control.InteriorRings)
                    edited.AddInteriorRing(hole);

                Assert.IsTrue(AnnotationCommandBase.TryApplyInSection(transform, edited, mosaic => location.SetShapeFromGeometryInSection(transform, mosaic.ToSqlGeometry()), name));

                Polygon drawn = LocationVolumeGeometry.For(location, transform).ControlPolygon();
                AssertSamePoints(edited.ExteriorRing, drawn.ExteriorRing, name);
                AssertSamePoints(edited.InteriorRings[0], drawn.InteriorRings[0], $"{name}: hole");
            }
        }

        [TestMethod]
        public void APolygonEditOffTheTransformIsRefusedWithTheReason()
        {
            IVolumeToSectionTransform transform = new ClippedTransform(new ScaleTransform(1, Vector2.Zero), new Rectangle(0, 2000, 0, 2000));
            Polygon edited = new([new(100, 100), new(2500, 100), new(300, 300), new(100, 100)]);
            string? reason = null;

            Assert.IsFalse(AnnotationCommandBase.TryApplyInSection(transform, edited, _ => Assert.Fail("applied"), nameof(VolumeEditSaveTests), r => reason = r));

            StringAssert.Contains(reason, "1 of 4 points");
        }

        /// <summary>
        /// A pen region cut under a warp offers pieces whose saved mosaic polygon draws, through the forward owner's mapping,
        /// exactly as the piece previewed: kept stored points map forward unchanged and the new pen points are the inverse
        /// owner's.
        /// </summary>
        [TestMethod]
        public void APenCutPieceIsSavedAsItIsPreviewed()
        {
            IVolumeToSectionTransform transform = new WarpTransform();
            Vector2[] corners = [new(2000, 2000), new(2100, 2000), new(2100, 2040), new(2150, 2040), new(2150, 2000), new(2250, 2000),
                                 new(2250, 2100), new(2150, 2100), new(2150, 2060), new(2100, 2060), new(2100, 2100), new(2000, 2100)];
            List<Vector2> ring = [];
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 a = corners[i], b = corners[(i + 1) % corners.Length];
                int steps = (int)(Vector2.Distance(a, b) / 10);
                for (int s = 0; s < steps; s++)
                    ring.Add(a + ((b - a) * ((double)s / steps)));
            }

            ring.Add(ring[0]);
            LocationObj location = LocationVolumeGeometryTests.NewLocation(LocationType.POLYGON, ring.ToArray().ToPolygon());
            Vector2[] mosaicStroke = [.. Enumerable.Range(0, 36).Select(i => new Vector2(2115, 2085 - (i * 2))),
                                      .. Enumerable.Range(0, 10).Select(i => new Vector2(2117 + (i * 2), 2015)),
                                      .. Enumerable.Range(0, 36).Select(i => new Vector2(2135, 2015 + (i * 2)))];
            List<Vector2> volumeStroke = [.. transform.SectionToVolume(mosaicStroke)];

            List<WebAnnotation.UI.Actions.Change2DContourAction> choices = WebAnnotation.UI.Actions.PenRegionCut.GetActions(location, transform, volumeStroke, 2.0);

            Assert.AreEqual(2, choices.Count);
            foreach (WebAnnotation.UI.Actions.Change2DContourAction choice in choices)
            {
                Polygon drawn = LocationVolumeGeometry.TryMapPolygon(choice.NewMosaicPolygon, transform)!;
                AssertSamePoints(choice.NewVolumePolygon.ExteriorRing, drawn.ExteriorRing, "piece");
            }
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
