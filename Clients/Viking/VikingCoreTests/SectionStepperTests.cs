using System.Collections.Generic;
using System.Linq;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="SectionStepper.Target"/>: gaps in section numbering are skipped as before, and a step past the
    /// end of the volume stops at the last (or first) section instead of doing nothing (Shift+= at section 295 of 5..300).
    /// </summary>
    [TestClass]
    public class SectionStepperTests
    {
        private static readonly int[] Gappy = [5, 6, 7, 10, 20, 21, 300];
        private static readonly int[] FiveTo300 = Enumerable.Range(5, 296).ToArray();

        [TestMethod]
        public void StepUpTen_NearEnd_ClampsToLastSection()
        {
            // The old loop did nothing here although five sections remained.
            Assert.IsNull(OldTarget(FiveTo300, 295, 10));
            Assert.AreEqual(300, SectionStepper.Target(FiveTo300, 295, 10));
        }

        [TestMethod]
        public void StepDownTen_NearStart_ClampsToFirstSection()
        {
            Assert.IsNull(OldTarget(FiveTo300, 8, -10));
            Assert.AreEqual(5, SectionStepper.Target(FiveTo300, 8, -10));
        }

        [TestMethod]
        public void AlreadyAtEnd_StaysPut()
        {
            Assert.AreEqual(300, SectionStepper.Target(FiveTo300, 300, 10));
            Assert.AreEqual(300, SectionStepper.Target(FiveTo300, 300, 1));
            Assert.AreEqual(5, SectionStepper.Target(FiveTo300, 5, -10));
            Assert.AreEqual(5, SectionStepper.Target(FiveTo300, 5, -1));
        }

        [TestMethod]
        public void Gaps_AreSkippedInStepDirection()
        {
            Assert.AreEqual(10, SectionStepper.Target(Gappy, 7, 1));
            Assert.AreEqual(20, SectionStepper.Target(Gappy, 10, 1));
            Assert.AreEqual(300, SectionStepper.Target(Gappy, 21, 1));
            Assert.AreEqual(21, SectionStepper.Target(Gappy, 300, -1));
            Assert.AreEqual(7, SectionStepper.Target(Gappy, 10, -1));
            Assert.AreEqual(20, SectionStepper.Target(Gappy, 7, 10));
            Assert.AreEqual(10, SectionStepper.Target(Gappy, 20, -10));
            Assert.AreEqual(7, SectionStepper.Target(Gappy, 19, -10));
        }

        [TestMethod]
        public void StepOne_MatchesNextAndPrevious()
        {
            Assert.AreEqual(296, SectionStepper.Target(FiveTo300, 295, 1));
            Assert.AreEqual(294, SectionStepper.Target(FiveTo300, 295, -1));
        }

        [TestMethod]
        public void StepLargerThanVolume_GoesToEnd()
        {
            Assert.AreEqual(300, SectionStepper.Target(Gappy, 6, 10000));
            Assert.AreEqual(5, SectionStepper.Target(Gappy, 21, -10000));
            Assert.AreEqual(300, SectionStepper.Target(Gappy, 6, int.MaxValue));
            Assert.AreEqual(5, SectionStepper.Target(Gappy, 21, -int.MaxValue));
        }

        [TestMethod]
        public void EmptyOrSingleSection_StaysPut()
        {
            Assert.AreEqual(7, SectionStepper.Target([], 7, 10));
            Assert.AreEqual(7, SectionStepper.Target([], 7, -10));
            Assert.AreEqual(42, SectionStepper.Target([42], 42, 10));
            Assert.AreEqual(42, SectionStepper.Target([42], 42, -1));
        }

        [TestMethod]
        public void CurrentNotInList_StepsFromItsNumber()
        {
            Assert.AreEqual(10, SectionStepper.Target(Gappy, 8, 1));
            Assert.AreEqual(7, SectionStepper.Target(Gappy, 8, -1));
            Assert.AreEqual(300, SectionStepper.Target(Gappy, 250, 100));
            Assert.AreEqual(300, SectionStepper.Target(Gappy, 400, -1));
        }

        [TestMethod]
        public void ZeroStep_StaysPut()
        {
            Assert.AreEqual(8, SectionStepper.Target(Gappy, 8, 0));
        }

        /// <summary>
        /// Wherever the old loop moved, the new code moves to the same section; where it did nothing, the new code
        /// stays put or stops at the end of the volume in the step direction.
        /// </summary>
        [TestMethod]
        public void MatchesOldLoopWhereverItMoved_ElseClampsToEnd()
        {
            Prop.ForAll(
                Arb.From<sbyte[]>(),
                Arb.From<sbyte>(),
                Arb.From<sbyte>(),
                (set, currentRaw, stepRaw) =>
                {
                    int[] numbers = set.Select(s => (int)s).Distinct().OrderBy(x => x).ToArray();
                    int current = currentRaw;
                    int step = stepRaw;

                    int actual = SectionStepper.Target(numbers, current, step);
                    int? old = step == 0 || numbers.Length == 0 ? current : OldTarget(numbers, current, step);
                    if (old.HasValue)
                        return actual == old.Value;

                    int end = step > 0 ? numbers.Last() : numbers.First();
                    bool beyond = step > 0 ? end > current : end < current;
                    return actual == (beyond ? end : current);
                }).QuickCheckThrowOnFailure();
        }

        /// <summary>
        /// Copy of the loop <c>StepUpNSections</c>/<c>StepDownNSections</c> used before <see cref="SectionStepper"/>;
        /// returns null where it left the section unchanged.
        /// </summary>
        private static int? OldTarget(int[] sorted, int current, int step)
        {
            if (step > 0)
            {
                for (int i = current + step; i <= sorted.Max(); i++)
                {
                    if (sorted.Contains(i))
                        return i;
                }
            }
            else
            {
                for (int i = current + step; i >= sorted.Min(); i--)
                {
                    if (sorted.Contains(i))
                        return i;
                }
            }

            return null;
        }
    }
}
