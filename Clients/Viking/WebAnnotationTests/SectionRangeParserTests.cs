using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation;

namespace WebAnnotationTests
{
    [TestClass]
    public class SectionRangeParserTests
    {
        [TestMethod]
        public void BlankMeansAllSections()
        {
            SectionRangeParse parsed = SectionRangeParser.Parse("  \r\n\t ");
            Assert.IsTrue(parsed.Success);
            Assert.IsTrue(parsed.IsAllSections);
            Assert.AreEqual("All sections", parsed.Interpretation);
            Assert.AreEqual(0, parsed.Sections.Count);
        }

        [TestMethod]
        public void SingleNumber()
        {
            SectionRangeParse parsed = SectionRangeParser.Parse("5");
            Assert.IsTrue(parsed.Success);
            Assert.AreEqual("5", parsed.Interpretation);
            CollectionAssert.AreEqual(new long[] { 5 }, (System.Collections.ICollection)parsed.Sections);
        }

        [TestMethod]
        public void InclusiveRange()
        {
            SectionRangeParse parsed = SectionRangeParser.Parse("1-3");
            Assert.IsTrue(parsed.Success);
            Assert.AreEqual("1-3", parsed.Interpretation);
            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, (System.Collections.ICollection)parsed.Sections);
        }

        [TestMethod]
        public void ReversedRangeIsNormalized()
        {
            SectionRangeParse parsed = SectionRangeParser.Parse("7-3");
            Assert.IsTrue(parsed.Success);
            Assert.AreEqual("3-7", parsed.Interpretation);
            Assert.AreEqual(5, parsed.Sections.Count);
            Assert.AreEqual(3, parsed.Sections[0]);
            Assert.AreEqual(7, parsed.Sections[4]);
        }

        [TestMethod]
        public void MixedSeparatorsAndDuplicateOverlapCollapse()
        {
            SectionRangeParse parsed = SectionRangeParser.Parse("1, 3, 5, 3-7");
            Assert.IsTrue(parsed.Success);
            Assert.AreEqual("1, 3-7", parsed.Interpretation);
            CollectionAssert.AreEqual(new long[] { 1, 3, 4, 5, 6, 7 }, (System.Collections.ICollection)parsed.Sections);
        }

        [TestMethod]
        public void NewLinesAndAdjacentRunsCollapse()
        {
            SectionRangeParse parsed = SectionRangeParser.Parse("1\n2, 4;6");
            Assert.IsTrue(parsed.Success);
            Assert.AreEqual("1-2, 4, 6", parsed.Interpretation);
        }

        [TestMethod]
        public void SpacesAroundDash()
        {
            SectionRangeParse parsed = SectionRangeParser.Parse("1 - 3, 3");
            Assert.IsTrue(parsed.Success);
            Assert.AreEqual("1-3", parsed.Interpretation);
        }

        [TestMethod]
        public void OverlappingRangesCollapse()
        {
            SectionRangeParse parsed = SectionRangeParser.Parse("1-5, 3-7");
            Assert.IsTrue(parsed.Success);
            Assert.AreEqual("1-7", parsed.Interpretation);
        }

        [TestMethod]
        public void SinglePointRange()
        {
            SectionRangeParse parsed = SectionRangeParser.Parse("4-4");
            Assert.IsTrue(parsed.Success);
            Assert.AreEqual("4", parsed.Interpretation);
        }

        [TestMethod]
        public void GarbageTokenFails()
        {
            SectionRangeParse parsed = SectionRangeParser.Parse("1, foo");
            Assert.IsFalse(parsed.Success);
            Assert.IsFalse(parsed.IsAllSections);
            StringAssert.Contains(parsed.Interpretation, "foo");
            Assert.AreEqual(0, parsed.Sections.Count);
        }

        [TestMethod]
        public void RangeThatIsTooLargeFails()
        {
            SectionRangeParse parsed = SectionRangeParser.Parse("1-1000001");
            Assert.IsFalse(parsed.Success);
            StringAssert.Contains(parsed.Interpretation, "too large");
        }

        [TestMethod]
        public void UnknownSectionsAreListedCanonically()
        {
            VolumeSectionSelection selection = SectionRangeParser.SelectInVolume(
                new long[] { 1, 3, 4, 9 },
                new HashSet<int> { 1, 2, 3, 4 });
            Assert.IsTrue(selection.CanStart);
            CollectionAssert.AreEqual(new long[] { 1, 3, 4 }, (System.Collections.ICollection)selection.Present);
            CollectionAssert.AreEqual(new long[] { 9 }, (System.Collections.ICollection)selection.Missing);
            Assert.AreEqual("Will update: 1, 3-4" + System.Environment.NewLine + "Skipped (not in this volume): 9", selection.Interpretation);
        }

        [TestMethod]
        public void KnownSectionsHaveNoMembershipError()
        {
            VolumeSectionSelection selection = SectionRangeParser.SelectInVolume(
                new long[] { 2, 4 },
                new[] { 1, 2, 4 });
            Assert.IsTrue(selection.CanStart);
            Assert.AreEqual(0, selection.Missing.Count);
            Assert.AreEqual("2, 4", selection.Interpretation);
        }

        [TestMethod]
        public void RangeAcrossMissingSectionsKeepsEveryPresentNumber()
        {
            SectionRangeParse parsed = SectionRangeParser.Parse("1-10, 20-22");
            VolumeSectionSelection selection = SectionRangeParser.SelectInVolume(
                parsed.Sections,
                new HashSet<int> { 1, 2, 3, 5, 8, 9, 10, 21 });
            Assert.IsTrue(selection.CanStart);
            CollectionAssert.AreEqual(new long[] { 1, 2, 3, 5, 8, 9, 10, 21 }, (System.Collections.ICollection)selection.Present);
            CollectionAssert.AreEqual(new long[] { 4, 6, 7, 20, 22 }, (System.Collections.ICollection)selection.Missing);
            StringAssert.Contains(selection.Interpretation, "Will update: 1-3, 5, 8-10, 21");
            StringAssert.Contains(selection.Interpretation, "Skipped (not in this volume): 4, 6-7, 20, 22");
        }

        [TestMethod]
        public void NoPresentSectionsCannotStart()
        {
            VolumeSectionSelection selection = SectionRangeParser.SelectInVolume(
                new long[] { 4, 6, 7 },
                new HashSet<int> { 1, 2, 3 });
            Assert.IsFalse(selection.CanStart);
            Assert.AreEqual(0, selection.Present.Count);
            Assert.AreEqual("None of these sections are in this volume: 4, 6-7", selection.Interpretation);
        }
    }
}
