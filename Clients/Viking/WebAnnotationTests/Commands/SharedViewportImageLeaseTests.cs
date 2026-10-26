using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Viewport capture sharing between auto-polygonize and segmentation commands.
    /// Bounds must stay within 1% and the stored tile level must be the single level every camera resolves to.
    /// </summary>
    [TestClass]
    public class SharedViewportImageLeaseTests
    {
        private static AutoPolygonizeUploadContext Context(double downsample, Rectangle bounds)
            => new(downsample, bounds, 64, 64);

        [TestMethod]
        public void CanReuse_SameView_IsTrue()
        {
            Rectangle bounds = new(0, 1000, 0, 800);
            AutoPolygonizeUploadContext context = Context(1, bounds);

            Assert.IsTrue(context.MatchesViewportForReuse(bounds, 2));
            Assert.IsTrue(context.MatchesViewportForReuse(new Rectangle(4, 1004, 0, 800), 1.5));
        }

        [TestMethod]
        public void CanReuse_ViewMovedPastOnePercent_IsFalse()
        {
            Rectangle bounds = new(0, 1000, 0, 800);
            AutoPolygonizeUploadContext context = Context(1, bounds);

            Assert.IsFalse(context.MatchesViewportForReuse(new Rectangle(20, 1020, 0, 800), 2));
        }

        [TestMethod]
        public void CanReuse_EveryCameraLevelResolvesToTheSingleTileLevel()
        {
            Rectangle bounds = new(0, 1000, 0, 800);

            Assert.IsTrue(Context(1, bounds).MatchesViewportForReuse(bounds, 1));
            Assert.IsTrue(Context(1, bounds).MatchesViewportForReuse(bounds, 1.2));
            Assert.IsTrue(Context(1, bounds).MatchesViewportForReuse(bounds, 4));
            Assert.IsFalse(Context(2, bounds).MatchesViewportForReuse(bounds, 2), "no camera submits level 2");
        }

        [TestMethod]
        public async Task GetOrUpload_SecondCallerJoinsInFlightCapture()
        {
            SharedViewportImageLease lease = new();
            Rectangle bounds = new(0, 1000, 0, 800);
            var started = new TaskCompletionSource<bool>();
            var release = new TaskCompletionSource<AutoPolygonizeUploadContext?>();
            int calls = 0;

            Task<AutoPolygonizeUploadContext?> Upload()
            {
                calls++;
                started.TrySetResult(true);
                return release.Task;
            }

            Task<AutoPolygonizeUploadContext?> first = lease.GetOrUploadAsync(bounds, 2, Upload);
            Task<AutoPolygonizeUploadContext?> second = lease.GetOrUploadAsync(bounds, 1.5, Upload);
            await started.Task;

            Assert.AreEqual(1, calls);
            release.SetResult(Context(1, bounds));

            AutoPolygonizeUploadContext? a = await first;
            AutoPolygonizeUploadContext? b = await second;
            Assert.AreEqual(bounds, a?.WorldBounds);
            Assert.AreEqual(bounds, b?.WorldBounds);

            int extraUploads = 0;
            AutoPolygonizeUploadContext? cached = await lease.GetOrUploadAsync(
                bounds,
                2,
                () =>
                {
                    extraUploads++;
                    return Task.FromResult<AutoPolygonizeUploadContext?>(null);
                });
            Assert.AreEqual(0, extraUploads);
            Assert.AreEqual(bounds, cached?.WorldBounds);
        }

        [TestMethod]
        public async Task AbandonInFlight_DoesNotReplaceALaterUpload()
        {
            SharedViewportImageLease lease = new();
            Rectangle bounds = new(0, 1000, 0, 800);
            Rectangle laterBounds = new(4, 1004, 0, 800);
            var gate = new TaskCompletionSource<bool>();

            Task<AutoPolygonizeUploadContext?> first = lease.GetOrUploadAsync(bounds, 2, async () =>
            {
                await gate.Task;
                return Context(1, bounds);
            });

            lease.AbandonInFlight();
            Task<AutoPolygonizeUploadContext?> second = lease.GetOrUploadAsync(
                bounds,
                2,
                () => Task.FromResult<AutoPolygonizeUploadContext?>(Context(1, laterBounds)));

            gate.SetResult(true);
            AutoPolygonizeUploadContext? abandoned = await first;
            AutoPolygonizeUploadContext? current = await second;

            Assert.AreEqual(bounds, abandoned?.WorldBounds);
            Assert.AreEqual(laterBounds, current?.WorldBounds);

            int extraUploads = 0;
            AutoPolygonizeUploadContext? cached = await lease.GetOrUploadAsync(
                bounds,
                2,
                () =>
                {
                    extraUploads++;
                    return Task.FromResult<AutoPolygonizeUploadContext?>(null);
                });
            Assert.AreEqual(0, extraUploads);
            Assert.AreEqual(laterBounds, cached?.WorldBounds, "the abandoned capture must not replace the later one");
        }

        [TestMethod]
        public async Task ForgetIfViewMoved_DropsThePublishedCapture()
        {
            SharedViewportImageLease lease = new();
            Rectangle bounds = new(0, 1000, 0, 800);

            await lease.GetOrUploadAsync(bounds, 2, () => Task.FromResult<AutoPolygonizeUploadContext?>(Context(1, bounds)));

            lease.ForgetIfViewMoved(new Rectangle(20, 1020, 0, 800), 2);

            int uploadsAfterForget = 0;
            AutoPolygonizeUploadContext? afterForget = await lease.GetOrUploadAsync(
                bounds,
                2,
                () =>
                {
                    uploadsAfterForget++;
                    return Task.FromResult<AutoPolygonizeUploadContext?>(Context(1, bounds));
                });
            Assert.AreEqual(1, uploadsAfterForget);
            Assert.IsNotNull(afterForget);
        }
    }
}
