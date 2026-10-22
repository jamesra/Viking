using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using WebAnnotation.View;

namespace WebAnnotationTests
{
    /// <summary>
    /// Covers the case naming, repeat-step counting and log line of the check run after a section change.
    /// </summary>
    [TestClass]
    public class SectionLoadCheckTests
    {
        [TestMethod]
        public void Classify_QueriesCompleteWinsOverQueued()
        {
            Assert.AreEqual(SectionLoadCheckOutcome.QueriesComplete, SectionLoadCheck.Classify(true, false));
            Assert.AreEqual(SectionLoadCheckOutcome.QueriesComplete, SectionLoadCheck.Classify(true, true));
        }

        [TestMethod]
        public void Classify_IncompleteAndQueuedIsStillLoading_IncompleteAndIdleIsNotLoaded()
        {
            Assert.AreEqual(SectionLoadCheckOutcome.StillLoading, SectionLoadCheck.Classify(false, true));
            Assert.AreEqual(SectionLoadCheckOutcome.NotLoaded, SectionLoadCheck.Classify(false, false));
        }

        [TestMethod]
        public void ShouldRequestAgain_OnlyLeavesAnInFlightLoadAlone()
        {
            Assert.IsFalse(SectionLoadCheck.ShouldRequestAgain(SectionLoadCheckOutcome.StillLoading));
            Assert.IsTrue(SectionLoadCheck.ShouldRequestAgain(SectionLoadCheckOutcome.NotLoaded));
            Assert.IsTrue(SectionLoadCheck.ShouldRequestAgain(SectionLoadCheckOutcome.QueriesComplete));
        }

        [TestMethod]
        public void UpdateRun_SameDirectionExtendsRun()
        {
            SectionLoadCheck.UpdateRun(10, 9, -1, 1, out int direction, out int run);
            Assert.AreEqual(-1, direction);
            Assert.AreEqual(2, run);
        }

        [TestMethod]
        public void UpdateRun_ReversalOrFirstStepStartsRunAtOne()
        {
            SectionLoadCheck.UpdateRun(10, 11, -1, 3, out int direction, out int run);
            Assert.AreEqual(1, direction);
            Assert.AreEqual(1, run);

            SectionLoadCheck.UpdateRun(10, 9, 0, 1, out direction, out run);
            Assert.AreEqual(-1, direction);
            Assert.AreEqual(1, run);
        }

        [TestMethod]
        public void UpdateRun_JumpUnknownOrSameSectionResetsToJump()
        {
            SectionLoadCheck.UpdateRun(10, 15, 1, 4, out int direction, out int run);
            Assert.AreEqual(0, direction);
            Assert.AreEqual(1, run);

            SectionLoadCheck.UpdateRun(0, 5, 1, 4, out direction, out run);
            Assert.AreEqual(0, direction);
            Assert.AreEqual(1, run);

            SectionLoadCheck.UpdateRun(5, 5, 1, 4, out direction, out run);
            Assert.AreEqual(0, direction);
            Assert.AreEqual(1, run);
        }

        [TestMethod]
        public void FormatLine_NamesSectionZoomStepRunAndCase()
        {
            string line = SectionLoadCheck.FormatLine(72, 2.5, -1, 3, SectionLoadCheckOutcome.NotLoaded, true);
            Assert.AreEqual("section=72 downsample=2.5 step=down run=3 outcome=NotLoaded requestedAgain=True", line);
        }

        [TestMethod]
        public void Append_WritesTimestampedLineAndIgnoresAnUnwritablePath()
        {
            string original = SectionLoadCheck.LogPath;
            string path = Path.Combine(Path.GetTempPath(), "viking-section-load-check-test-" + System.Guid.NewGuid().ToString("N") + ".log");
            try
            {
                SectionLoadCheck.LogPath = path;
                SectionLoadCheck.Append("hello");
                StringAssert.EndsWith(File.ReadAllText(path).TrimEnd(), " hello");

                SectionLoadCheck.LogPath = Path.Combine(path, "no-such-folder", "x.log");
                SectionLoadCheck.Append("ignored");
            }
            finally
            {
                SectionLoadCheck.LogPath = original;
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
