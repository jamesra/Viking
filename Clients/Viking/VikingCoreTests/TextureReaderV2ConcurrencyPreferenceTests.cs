using System;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins how the user's "max concurrent requests" preference becomes the HTTP worker limit of
    /// <see cref="TextureRequestQueue"/>: <see cref="TextureReaderV2.GetAutoMaxConcurrentRequests"/> sizes the Auto
    /// setting from the tile pixel width, and <see cref="TextureReaderV2.ApplyMaxConcurrentRequestPreference"/> picks
    /// between a hard limit, Auto, or leaving the limit alone. Callers are the volume loader and the section viewer,
    /// so a wrong limit either starves tile loading (1 worker) or floods the server.
    /// </summary>
    /// <remarks>
    /// The worker limit is process-wide static state, so each test restores the value it found and runs on the
    /// MSTest thread only; no worker pool is started because nothing is enqueued.
    /// </remarks>
    [TestClass]
    public class TextureReaderV2ConcurrencyPreferenceTests
    {
        private const int MaxWorkersCeiling = 256;

        private int _savedMaxWorkers;

        [TestInitialize]
        public void Initialize()
        {
            _savedMaxWorkers = TextureRequestQueue.MaxWorkers;
        }

        [TestCleanup]
        public void Cleanup()
        {
            TextureRequestQueue.SetMaxWorkers(_savedMaxWorkers);
        }

        /// <summary>Known tile sizes: 4096 / width tiles per row, doubled, never below one.</summary>
        [TestMethod]
        public void AutoLimitForCommonTileWidthsIsTwiceTheTilesPerFourThousandNinetySixPixels()
        {
            Assert.AreEqual(8192, TextureReaderV2.GetAutoMaxConcurrentRequests(1));
            Assert.AreEqual(32, TextureReaderV2.GetAutoMaxConcurrentRequests(256));
            Assert.AreEqual(16, TextureReaderV2.GetAutoMaxConcurrentRequests(512));
            Assert.AreEqual(8, TextureReaderV2.GetAutoMaxConcurrentRequests(1024));
            Assert.AreEqual(2, TextureReaderV2.GetAutoMaxConcurrentRequests(4096));
        }

        /// <summary>Widths larger than 4096 make the integer division 0; the limit still must not drop to 0.</summary>
        [TestMethod]
        public void AutoLimitForTileWiderThanFourThousandNinetySixIsOne()
        {
            Assert.AreEqual(1, TextureReaderV2.GetAutoMaxConcurrentRequests(4097));
            Assert.AreEqual(1, TextureReaderV2.GetAutoMaxConcurrentRequests(int.MaxValue));
        }

        /// <summary>A zero or negative width is treated as width 1 instead of dividing by zero.</summary>
        [TestMethod]
        public void AutoLimitForZeroOrNegativeTileWidthTreatsWidthAsOne()
        {
            int forOne = TextureReaderV2.GetAutoMaxConcurrentRequests(1);
            Assert.AreEqual(forOne, TextureReaderV2.GetAutoMaxConcurrentRequests(0));
            Assert.AreEqual(forOne, TextureReaderV2.GetAutoMaxConcurrentRequests(-1));
            Assert.AreEqual(forOne, TextureReaderV2.GetAutoMaxConcurrentRequests(int.MinValue));
        }

        /// <summary>
        /// For every tile width the Auto limit is the documented formula, is at least 1, and never grows as tiles
        /// get wider.
        /// </summary>
        [TestMethod]
        public void AutoLimitMatchesFormulaIsPositiveAndNeverGrowsWithWidth()
        {
            Prop.ForAll(Arb.Default.Int32(), Arb.From(Gen.Choose(0, 10000)), (int width, int extra) =>
            {
                int auto = TextureReaderV2.GetAutoMaxConcurrentRequests(width);
                int expected = Math.Max(1, 4096 / Math.Max(1, width) * 2);
                int wider = (int)Math.Min(int.MaxValue, (long)Math.Max(1, width) + extra);
                return auto == expected
                    && auto >= 1
                    && TextureReaderV2.GetAutoMaxConcurrentRequests(wider) <= auto;
            }).QuickCheckThrowOnFailure();
        }

        /// <summary>A positive preference is a hard limit, whatever the tile width, clamped to the queue's 1-256 range.</summary>
        [TestMethod]
        public void PositivePreferenceSetsWorkerLimitIgnoringTileWidth()
        {
            Gen<int?> widths = Gen.OneOf(Gen.Constant<int?>(null), Gen.Choose(1, 8192).Select(w => (int?)w));

            Prop.ForAll(Arb.From(Gen.Choose(1, 1000)), Arb.From(widths), (int preference, int? width) =>
            {
                TextureRequestQueue.SetMaxWorkers(7);

                TextureReaderV2.ApplyMaxConcurrentRequestPreference(preference, width);

                return TextureRequestQueue.MaxWorkers == Math.Min(preference, MaxWorkersCeiling);
            }).QuickCheckThrowOnFailure();
        }

        /// <summary>Preference 0 (Auto) with a known tile width uses the Auto formula, clamped to 1-256.</summary>
        [TestMethod]
        public void ZeroPreferenceWithTileWidthUsesAutoLimit()
        {
            Prop.ForAll(Arb.From(Gen.Choose(-10, 20000)), (int width) =>
            {
                TextureRequestQueue.SetMaxWorkers(7);

                TextureReaderV2.ApplyMaxConcurrentRequestPreference(0, width);

                int auto = TextureReaderV2.GetAutoMaxConcurrentRequests(width);
                return TextureRequestQueue.MaxWorkers == Math.Min(auto, MaxWorkersCeiling);
            }).QuickCheckThrowOnFailure();
        }

        /// <summary>
        /// Before a volume is loaded the tile width is unknown: Auto must leave the current limit untouched rather
        /// than reset it. A negative preference is not a hard limit either.
        /// </summary>
        [TestMethod]
        public void NonPositivePreferenceWithoutTileWidthLeavesWorkerLimitUnchanged()
        {
            Prop.ForAll(Arb.From(Gen.Choose(1, MaxWorkersCeiling)), Arb.From(Gen.Choose(-1000, 0)), (int current, int preference) =>
            {
                TextureRequestQueue.SetMaxWorkers(current);

                TextureReaderV2.ApplyMaxConcurrentRequestPreference(preference, null);

                return TextureRequestQueue.MaxWorkers == current;
            }).QuickCheckThrowOnFailure();
        }

        /// <summary>
        /// A negative preference with a known width falls through to Auto (only positive values are hard limits).
        /// </summary>
        [TestMethod]
        public void NegativePreferenceWithTileWidthUsesAutoLimit()
        {
            TextureRequestQueue.SetMaxWorkers(7);

            TextureReaderV2.ApplyMaxConcurrentRequestPreference(-5, 512);

            Assert.AreEqual(16, TextureRequestQueue.MaxWorkers);
        }

        /// <summary>Known clamps: a tiny tile width (Auto 8192) and an oversized preference both stop at 256.</summary>
        [TestMethod]
        public void WorkerLimitIsClampedToTwoHundredFiftySix()
        {
            TextureReaderV2.ApplyMaxConcurrentRequestPreference(0, 1);
            Assert.AreEqual(MaxWorkersCeiling, TextureRequestQueue.MaxWorkers);

            TextureReaderV2.ApplyMaxConcurrentRequestPreference(int.MaxValue, null);
            Assert.AreEqual(MaxWorkersCeiling, TextureRequestQueue.MaxWorkers);
        }
    }
}
