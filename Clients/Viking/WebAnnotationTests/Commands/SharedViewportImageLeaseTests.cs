using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Viewport image sharing between auto-polygonize and segmentation commands.
    /// Bounds must stay within 1% and downsample must not move by 2×.
    /// </summary>
    [TestClass]
    public class SharedViewportImageLeaseTests
    {
        private static AutoPolygonizeUploadContext Context(ulong id, double downsample, Rectangle bounds)
            => new(id, downsample, bounds, 64, 64);

        [TestMethod]
        public void CanReuse_SameView_IsTrue()
        {
            Rectangle bounds = new(0, 1000, 0, 800);
            AutoPolygonizeUploadContext context = Context(7, 4, bounds);

            Assert.IsTrue(SharedViewportImageLease.CanReuse(context, bounds, 4));
            Assert.IsTrue(SharedViewportImageLease.CanReuse(context, new Rectangle(4, 1004, 0, 800), 5));
        }

        [TestMethod]
        public void CanReuse_ViewMovedPastOnePercent_IsFalse()
        {
            Rectangle bounds = new(0, 1000, 0, 800);
            AutoPolygonizeUploadContext context = Context(7, 4, bounds);

            Assert.IsFalse(SharedViewportImageLease.CanReuse(context, new Rectangle(20, 1020, 0, 800), 4));
        }

        [TestMethod]
        public void CanReuse_DownsampleDoubled_IsFalse()
        {
            Rectangle bounds = new(0, 1000, 0, 800);
            AutoPolygonizeUploadContext context = Context(7, 4, bounds);

            Assert.IsFalse(SharedViewportImageLease.CanReuse(context, bounds, 8));
        }

        [TestMethod]
        public async Task GetOrUpload_SecondCallerJoinsInFlightCapture()
        {
            AutoPolygonizeCache cache = new();
            SharedViewportImageLease lease = new(cache);
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

            Task<AutoPolygonizeUploadContext?> first = lease.GetOrUploadAsync(bounds, 4, Upload);
            Task<AutoPolygonizeUploadContext?> second = lease.GetOrUploadAsync(bounds, 4, Upload);
            await started.Task;

            Assert.AreEqual(1, calls);
            release.SetResult(Context(11, 4, bounds));

            AutoPolygonizeUploadContext? a = await first;
            AutoPolygonizeUploadContext? b = await second;
            Assert.AreEqual(11ul, a?.ImageId);
            Assert.AreEqual(11ul, b?.ImageId);
            Assert.IsTrue(cache.IsImageHeld(11));
            Assert.IsTrue(lease.TryAdopt(bounds, 4, out AutoPolygonizeUploadContext adopted));
            Assert.AreEqual(11ul, adopted.ImageId);
        }

        [TestMethod]
        public async Task AbandonInFlight_DoesNotReplaceALaterUpload()
        {
            AutoPolygonizeCache cache = new();
            SharedViewportImageLease lease = new(cache);
            Rectangle bounds = new(0, 1000, 0, 800);
            var gate = new TaskCompletionSource<bool>();

            Task<AutoPolygonizeUploadContext?> first = lease.GetOrUploadAsync(bounds, 4, async () =>
            {
                await gate.Task;
                return Context(1, 4, bounds);
            });

            lease.AbandonInFlight();
            Task<AutoPolygonizeUploadContext?> second = lease.GetOrUploadAsync(
                bounds,
                4,
                () => Task.FromResult<AutoPolygonizeUploadContext?>(Context(2, 4, bounds)));

            gate.SetResult(true);
            AutoPolygonizeUploadContext? abandoned = await first;
            AutoPolygonizeUploadContext? current = await second;

            Assert.AreEqual(1ul, abandoned?.ImageId);
            Assert.AreEqual(2ul, current?.ImageId);
            Assert.IsFalse(cache.IsImageHeld(1));
            Assert.IsTrue(cache.IsImageHeld(2));
            Assert.IsTrue(lease.TryAdopt(bounds, 4, out AutoPolygonizeUploadContext adopted));
            Assert.AreEqual(2ul, adopted.ImageId);
        }

        [TestMethod]
        public async Task ForgetIfViewMoved_ReleasesTheLeaseHold()
        {
            AutoPolygonizeCache cache = new();
            SharedViewportImageLease lease = new(cache);
            Rectangle bounds = new(0, 1000, 0, 800);
            ulong? released = null;
            cache.ImageLeaseReleased += id => released = id;

            await lease.GetOrUploadAsync(bounds, 4, () => Task.FromResult<AutoPolygonizeUploadContext?>(Context(5, 4, bounds)));
            Assert.IsTrue(cache.IsImageHeld(5));

            lease.ForgetIfViewMoved(new Rectangle(20, 1020, 0, 800), 4);

            Assert.IsFalse(cache.IsImageHeld(5));
            Assert.AreEqual(5ul, released);
            Assert.IsFalse(lease.TryAdopt(bounds, 4, out _));
        }
    }
}
