using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// <see cref="SegmentationViewportSession"/> runs over any <see cref="ISegmentationViewport"/> (Jotunn's scene host),
    /// not only Viking's section viewer.
    /// </summary>
    [TestClass]
    public class SegmentationViewportInterfaceTests
    {
        sealed class FakeViewport : ISegmentationViewport
        {
            public double CameraDownsample => 2;
            public int ViewportWidth => 100;
            public int ViewportHeight => 50;
            public Geometry.Vector2 ScreenToWorld(double x, double y) => new(x * 2, y * 2);
            public int SectionNumber => 7;
            public string VolumeName => "Test";
            public string VolumeTransform => "Grid";
            public string SectionTransform => "Grid";
            public string Channel => "TEM";
            public bool IsGrayscale => true;

            public Task<RenderTarget2D> RenderSceneToTexture(VikingXNA.Scene tileScene, float centerX, float centerY, int sectionNumber,
                bool showOverlays, bool asyncTextureLoad, CancellationToken token) => Task.FromResult<RenderTarget2D>(null);
        }

        [TestMethod]
        public void ViewportBoundsComeFromTheInterface()
        {
            SegmentationViewportSession session = new(new FakeViewport());

            Geometry.Rectangle bounds = session.GetCurrentViewportBounds();

            Assert.AreEqual(new Geometry.Rectangle(new Geometry.Vector2(0, 0), new Geometry.Vector2(200, 100)), bounds);
        }

        [TestMethod]
        public void CirclePromptIsFourClicksInsideTheCircle()
        {
            var circle = WebAnnotationTests.Actions.ChangeTypeActionTests.NewLocation(
                Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE, "CURVEPOLYGON(CIRCULARSTRING(40 50, 50 40, 60 50, 50 60, 40 50))");

            Assert.IsTrue(WebAnnotation.Tools.SegmentTool.TryCreateCirclePrompt(
                circle, WebAnnotationTests.Actions.ChangeTypeActionTests.CreateBoundedTransform(), out var prompt));

            Assert.AreEqual(4, prompt.Points.Count);
            foreach (Geometry.Vector2 point in prompt.Points)
                Assert.IsTrue(Geometry.Vector2.Distance(point, new Geometry.Vector2(50, 50)) < 10);
        }

        [TestMethod]
        public async Task SegmentingWithoutAServiceReportsNoClient()
        {
            SegmentationViewportSession session = new(new FakeViewport());

            var response = await session.SegmentAsync([new Geometry.Vector2(10, 10)], [], CancellationToken.None);

            Assert.IsNull(response);
            Assert.AreEqual(SegmentationSkipKind.NoClient, session.ConsumeLastSkip(out _));
        }
    }
}
