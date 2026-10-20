using Geometry;
using GeometryTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;
using WebAnnotation;
using WebAnnotation.UI.Commands;
using WebAnnotation.View;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Fill-bucket hover uses volume/smoothed holes and hole views; click used to
    /// test only the mosaic ring or skip hole views that are not LocationCanvasView.
    /// </summary>
    [TestClass]
    public class PolygonHoleFillTests
    {
        private static Polygon BoxWithCenteredHole(double outerScale, double holeScale)
        {
            Polygon outer = Primitives.BoxPolygon(outerScale);
            outer.AddInteriorRing(Primitives.BoxPolygon(holeScale));
            return outer;
        }

        [TestMethod]
        public void MosaicOnlyRemoveMissesVolumeSpaceClick()
        {
            Polygon mosaic = BoxWithCenteredHole(10, 3);
            Vector2 volumeClick = mosaic.InteriorPolygons[0].Centroid + new Vector2(40, 0);

            Assert.IsFalse(mosaic.TryRemoveInteriorRing(volumeClick));
            Assert.AreEqual(1, mosaic.InteriorPolygons.Count);
        }

        [TestMethod]
        public void VolumeSpaceClickRemovesMatchingMosaicHole()
        {
            Polygon mosaic = BoxWithCenteredHole(10, 3);
            Polygon volume = mosaic.Translate(new Vector2(40, 0));
            Vector2 volumeClick = volume.InteriorPolygons[0].Centroid;
            Vector2 mosaicClick = volumeClick;

            Assert.IsTrue(PolygonHoleFill.TryRemoveHoleAtClick(mosaic, mosaicClick, volume, volumeClick, 0));
            Assert.AreEqual(0, mosaic.InteriorPolygons.Count);
        }

        [TestMethod]
        public void SmoothedVolumeClickRemovesControlPointHole()
        {
            Polygon mosaic = BoxWithCenteredHole(10, 3);
            Polygon volume = (Polygon)mosaic.Clone();
            Polygon smoothed = volume.Smooth(8);

            if (!TryFindPointCoveredOnlyBySmoothedHole(volume, smoothed, out Vector2 probe))
                Assert.Inconclusive("Catmull–Rom smoothing did not expand the hole on this fixture.");

            Assert.IsFalse(mosaic.TryRemoveInteriorRing(probe));
            Assert.IsTrue(PolygonHoleFill.TryRemoveHoleAtClick(mosaic, probe, volume, probe, 8));
            Assert.AreEqual(0, mosaic.InteriorPolygons.Count);
        }

        [TestMethod]
        public void HoleViewIsNotLocationCanvasViewButReportsRemoveHole()
        {
            Polygon hole = Primitives.BoxPolygon(2);
            LocationInteriorHoleView view = new(42, 0, hole, hole);

            Assert.IsFalse(view is LocationCanvasView);

            bool mouse = LocationActionDispatch.TryGetAction(
                view, Vector2.Zero, 1, Keys.Control, penContact: false, out LocationAction mouseAction, out long mouseId);
            bool pen = LocationActionDispatch.TryGetAction(
                view, Vector2.Zero, 1, Keys.Control, penContact: true, out LocationAction penAction, out long penId);

            Assert.IsTrue(mouse);
            Assert.IsTrue(pen);
            Assert.AreEqual(LocationAction.REMOVEHOLE, mouseAction);
            Assert.AreEqual(LocationAction.REMOVEHOLE, penAction);
            Assert.AreEqual(42, mouseId);
            Assert.AreEqual(42, penId);
        }

        /// <summary>
        /// Search just outside each unsmoothed hole edge for a point the display curve covers.
        /// </summary>
        private static bool TryFindPointCoveredOnlyBySmoothedHole(Polygon volume, Polygon smoothed, out Vector2 probe)
        {
            probe = default;
            if (volume.InteriorPolygons.Count == 0 || smoothed.InteriorPolygons.Count == 0)
                return false;

            Polygon unsmoothedHole = volume.InteriorPolygons[0];
            Polygon smoothedHole = smoothed.InteriorPolygons[0];
            Vector2[] ring = unsmoothedHole.ExteriorRing;

            for (int i = 0; i < ring.Length - 1; i++)
            {
                Vector2 a = ring[i];
                Vector2 b = ring[i + 1];
                Vector2 mid = (a + b) / 2.0;
                Vector2 outward = (mid - unsmoothedHole.Centroid);
                if (outward.Magnitude <= Tolerance.Epsilon)
                    continue;

                outward = outward.Normalize();
                Vector2 candidate = mid + (outward * 0.15);
                if (!unsmoothedHole.Covers(candidate) && smoothedHole.Covers(candidate))
                {
                    probe = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
