using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using WebAnnotationModel;

namespace WebAnnotationTests
{
    [TestClass]
    public class RegionRefreshTimingTests
    {
        [TestMethod]
        public void IntervalShorterThanOneMinuteDoesNotFire()
        {
            DateTime last = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime now = last.AddSeconds(30);
            Assert.IsFalse(RegionRefreshTiming.IsIntervalElapsed(last, now, 180));
        }

        [TestMethod]
        public void IntervalJustOverThreeMinutesFires()
        {
            DateTime last = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime now = last.AddSeconds(181);
            Assert.IsTrue(RegionRefreshTiming.IsIntervalElapsed(last, now, 180));
        }

        [TestMethod]
        public void TimeSpanSecondsWouldNeverExceed180()
        {
            TimeSpan elapsed = TimeSpan.FromSeconds(181);
            Assert.IsTrue(elapsed.Seconds < 180);
            Assert.IsTrue(elapsed.TotalSeconds > 180);
        }
    }
}
