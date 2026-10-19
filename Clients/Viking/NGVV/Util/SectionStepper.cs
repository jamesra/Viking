using System.Collections.Generic;

namespace Viking.Common
{
    /// <summary>
    /// Picks the section a "step N sections" command lands on.
    /// Used by <c>SectionViewerControl.StepUpNSections</c> and <c>StepDownNSections</c>; kept free of UI types so it can be tested.
    /// </summary>
    public static class SectionStepper
    {
        /// <summary>
        /// Returns the section number reached by stepping <paramref name="n"/> sections from <paramref name="current"/>.
        /// Stepping up (positive <paramref name="n"/>) lands on the first existing section at or above <c>current + n</c>,
        /// so gaps in the numbering are skipped; stepping down (negative <paramref name="n"/>) lands on the last existing
        /// section at or below <c>current + n</c>. When that target lies past the end of the volume, the step stops at the
        /// last (or first) section instead of doing nothing.
        /// </summary>
        /// <param name="sortedNumbers">The volume's section numbers in ascending order, without duplicates.</param>
        /// <param name="current">The section being viewed. It need not be in <paramref name="sortedNumbers"/>.</param>
        /// <param name="n">Sections to step: positive moves up, negative moves down.</param>
        /// <returns>
        /// The target section number, or <paramref name="current"/> when there is nothing to move to in that direction
        /// (already at the end, an empty list, or <paramref name="n"/> is zero).
        /// </returns>
        public static int Target(IList<int> sortedNumbers, int current, int n)
        {
            if (sortedNumbers is null || sortedNumbers.Count == 0 || n == 0)
                return current;

            // long: current + n must not wrap for large steps
            long goal = (long)current + n;

            if (n > 0)
            {
                int i = FirstIndexAtOrAbove(sortedNumbers, goal);
                if (i < sortedNumbers.Count)
                    return sortedNumbers[i];

                int last = sortedNumbers[sortedNumbers.Count - 1];
                return last > current ? last : current;
            }
            else
            {
                // The last number <= goal sits just before the first number > goal.
                int i = FirstIndexAtOrAbove(sortedNumbers, goal + 1) - 1;
                if (i >= 0)
                    return sortedNumbers[i];

                int first = sortedNumbers[0];
                return first < current ? first : current;
            }
        }

        /// <summary>Index of the first number &gt;= <paramref name="value"/>, or Count when every number is smaller.</summary>
        private static int FirstIndexAtOrAbove(IList<int> sortedNumbers, long value)
        {
            int lo = 0;
            int hi = sortedNumbers.Count;
            while (lo < hi)
            {
                int mid = lo + ((hi - lo) / 2);
                if (sortedNumbers[mid] < value)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }
    }
}
