using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using WebAnnotationModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// Covers skip rules and watermark merge for the 30s visible-section location poll.
    /// </summary>
    [TestClass]
    public class SectionLocationPollPolicyTests
    {
        [TestMethod]
        public void ShouldPollSection_FalseWhenWatermarkNotSeeded()
        {
            Assert.IsFalse(SectionLocationPollPolicy.ShouldPollSection(DateTime.MinValue, false, false));
        }

        [TestMethod]
        public void ShouldPollSection_FalseWhenOutstandingSectionQuery()
        {
            DateTime seeded = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.IsFalse(SectionLocationPollPolicy.ShouldPollSection(seeded, hasOutstandingSectionQuery: true, sectionAnnotationLoadInFlight: false));
        }

        [TestMethod]
        public void ShouldPollSection_FalseWhenSectionLoadInFlight()
        {
            DateTime seeded = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.IsFalse(SectionLocationPollPolicy.ShouldPollSection(seeded, hasOutstandingSectionQuery: false, sectionAnnotationLoadInFlight: true));
        }

        [TestMethod]
        public void ShouldPollSection_TrueWhenSeededAndIdle()
        {
            DateTime seeded = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.IsTrue(SectionLocationPollPolicy.ShouldPollSection(seeded, false, false));
        }

        [TestMethod]
        public void MergeWatermark_IgnoresNonPositiveTicks()
        {
            DateTime existing = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(existing, SectionLocationPollPolicy.MergeWatermark(existing, 0));
            Assert.AreEqual(existing, SectionLocationPollPolicy.MergeWatermark(existing, -1));
        }

        [TestMethod]
        public void MergeWatermark_AdvancesWhenServerTimeIsLater()
        {
            DateTime existing = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            DateTime later = existing.AddSeconds(30);
            Assert.AreEqual(later, SectionLocationPollPolicy.MergeWatermark(existing, later.Ticks));
        }

        [TestMethod]
        public void MergeWatermark_DoesNotRewindWhenServerTimeIsEarlier()
        {
            DateTime existing = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            DateTime earlier = existing.AddSeconds(-30);
            Assert.AreEqual(existing, SectionLocationPollPolicy.MergeWatermark(existing, earlier.Ticks));
        }

        [TestMethod]
        public void IntervalMilliseconds_IsThirtySeconds()
        {
            Assert.AreEqual(30_000, SectionLocationPollPolicy.IntervalMilliseconds);
        }
    }
}
