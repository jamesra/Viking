using System;
using System.Globalization;
using System.Windows.Forms;
using FsCheck;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI;
using Viking.UI.Forms;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins how the Capture Screen dialog (<see cref="ScreenshotForm"/>) shows the viewer's downsample. The viewer zooms in
    /// past 1:1 down to <see cref="ViewerMagnificationSteps.MinDownsample"/>, so the dialog's Downsample Factor box must accept
    /// those values; it used to start at 1, so loading the dialog threw and Capture Screen failed whenever the viewer was
    /// zoomed in past 100%.
    /// </summary>
    [TestClass]
    public class ScreenshotFormDownsampleTests
    {
        private const double RectWidth = 400;
        private const double RectHeight = 300;

        /// <summary>
        /// Runs the dialog's Load handler without showing it, as <see cref="Form.ShowDialog()"/> would.
        /// </summary>
        private sealed class LoadedScreenshotForm : ScreenshotForm
        {
            public LoadedScreenshotForm(double viewerDownsample)
                : base(new Rectangle(new Vector2(0, 0), RectWidth, RectHeight), viewerDownsample, 1)
            {
                OnLoad(EventArgs.Empty);
            }

            public decimal ShownDownsample => Find("numDownsample").Value;

            public string ShownDownsampleText => Find("numDownsample").Text;

            public decimal ShownWidth => Find("numWidth").Value;

            private NumericUpDown Find(string name) => (NumericUpDown)Controls.Find(name, true)[0];
        }

        /// <summary>
        /// Regression: one Zoom In click from 1:1 (and Home, which snaps to 0.5) left a downsample below 1, and Capture Screen
        /// threw ArgumentOutOfRangeException instead of opening.
        /// </summary>
        [TestMethod]
        public void ZoomedInPastOneToOne_LoadsAndShowsTheViewerDownsample()
        {
            foreach (double ds in new[] { 0.5, ViewerMagnificationSteps.ZoomIn(1.0), ViewerMagnificationSteps.MinDownsample })
            {
                using LoadedScreenshotForm form = new(ds);

                Assert.AreEqual((decimal)ds, form.ShownDownsample, $"downsample {ds}");
                Assert.AreEqual(((decimal)ds).ToString("F2", CultureInfo.CurrentCulture), form.ShownDownsampleText, $"text shown for {ds}");
                Assert.AreEqual(ds, form.Downsample, $"capture downsample {ds}");
                Assert.AreEqual(Math.Round(RectWidth / ds), (double)form.ShownWidth, 1e-9, $"image width at {ds}");
            }
        }

        /// <summary>
        /// For every downsample the viewer allows, from its minimum up to the box's maximum (log-uniform, so zoomed-in values
        /// get as many cases as zoomed-out ones), the dialog loads, shows that downsample and captures with it.
        /// </summary>
        [TestMethod]
        public void AnyViewerDownsample_LoadsAndIsShown()
        {
            const double max = 65536;
            Gen<double> downsamples = Gen.Choose(0, 1_000_000).Select(i =>
                ViewerMagnificationSteps.MinDownsample * Math.Pow(max / ViewerMagnificationSteps.MinDownsample, i / 1_000_000.0));

            Prop.ForAll(downsamples.ToArbitrary(), ds =>
            {
                using LoadedScreenshotForm form = new(ds);
                return form.ShownDownsample == (decimal)ds && form.Downsample == ds;
            }).QuickCheckThrowOnFailure();
        }

        /// <summary>
        /// A pinch zoom sets the camera directly and can pass the viewer's limits; the box then shows its nearest end instead
        /// of throwing, and the capture still uses the viewer's own downsample.
        /// </summary>
        [TestMethod]
        public void DownsampleOutsideTheBoxRange_ShowsTheNearestEnd()
        {
            using (LoadedScreenshotForm below = new(0.001))
            {
                Assert.AreEqual((decimal)ViewerMagnificationSteps.MinDownsample, below.ShownDownsample);
                Assert.AreEqual(0.001, below.Downsample);
            }

            using LoadedScreenshotForm above = new(100000);
            Assert.AreEqual(65536m, above.ShownDownsample);
            Assert.AreEqual(100000.0, above.Downsample);
        }
    }
}
