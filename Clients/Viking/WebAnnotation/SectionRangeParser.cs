using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WebAnnotation
{
    /// <summary>
    /// How <see cref="SectionRangeParser"/> read a section-range box.
    /// The volume-position dialog shows <see cref="Interpretation"/> as the user types.
    /// </summary>
    public readonly struct SectionRangeParse
    {
        /// <summary>False when <see cref="Interpretation"/> is an error and <see cref="Sections"/> is empty.</summary>
        public bool Success { get; }

        /// <summary>True when the box was blank, meaning every section in the open volume.</summary>
        public bool IsAllSections { get; }

        /// <summary>Canonical range text such as "1, 3-7", "All sections", or the parse error.</summary>
        public string Interpretation { get; }

        /// <summary>Sorted unique section numbers. Empty when <see cref="IsAllSections"/> is true or <see cref="Success"/> is false.</summary>
        public IReadOnlyList<long> Sections { get; }

        public SectionRangeParse(bool success, bool isAllSections, string interpretation, IReadOnlyList<long> sections)
        {
            Success = success;
            IsAllSections = isAllSections;
            Interpretation = interpretation;
            Sections = sections;
        }
    }

    /// <summary>
    /// Parses free-text section lists into a sorted, de-duplicated set and a canonical range string.
    /// Called by the volume-position dialog as the user types. Adjacent and overlapping numbers collapse into inclusive ranges.
    /// </summary>
    public static class SectionRangeParser
    {
        /// <summary>Inclusive span limit so a typo such as 1-9999999999 does not allocate a huge list.</summary>
        private const long MaxRangeCount = 1_000_000;

        private static readonly Regex HyphenSpacing = new(@"\s*-\s*", RegexOptions.Compiled);
        private static readonly Regex TokenSplit = new(@"[\s,;]+", RegexOptions.Compiled);
        private static readonly Regex RangeToken = new(@"^(\d+)-(\d+)$", RegexOptions.Compiled);
        private static readonly Regex SingleToken = new(@"^(\d+)$", RegexOptions.Compiled);

        /// <summary>
        /// Parses section text. Blank or whitespace means all sections. Tokens may be separated by commas, semicolons, spaces, or new lines.
        /// A reversed range such as 7-3 is read as 3-7.
        /// </summary>
        public static SectionRangeParse Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new SectionRangeParse(success: true, isAllSections: true, interpretation: "All sections", sections: []);

            string normalized = HyphenSpacing.Replace(text, "-");
            string[] tokens = TokenSplit.Split(normalized.Trim());
            var numbers = new HashSet<long>();
            foreach (string raw in tokens)
            {
                string token = raw.Trim();
                if (token.Length == 0)
                    continue;

                if (!TryParseToken(token, out long start, out long end, out string? error))
                {
                    return Failed(error ?? $"Could not read \"{token}\" as a section or range.");
                }

                if (start > end)
                    (start, end) = (end, start);

                if (end - start >= MaxRangeCount)
                    return Failed($"Range {token} is too large.");

                for (long number = start; number <= end; number++)
                    numbers.Add(number);
            }

            if (numbers.Count == 0)
                return Failed("Enter a section or range, or leave the box blank to process every section.");

            long[] sorted = new long[numbers.Count];
            numbers.CopyTo(sorted);
            Array.Sort(sorted);
            return new SectionRangeParse(success: true, isAllSections: false, interpretation: FormatCanonical(sorted), sections: sorted);
        }

        /// <summary>
        /// Null when every section is in the volume. Otherwise an error listing the missing numbers in canonical form.
        /// </summary>
        public static string? VolumeMembershipError(IReadOnlyList<long> sections, IEnumerable<int> volumeSectionNumbers)
        {
            var volume = volumeSectionNumbers as HashSet<int> ?? new HashSet<int>(volumeSectionNumbers);
            var missing = new List<long>();
            foreach (long section in sections)
            {
                if (section < int.MinValue || section > int.MaxValue || !volume.Contains((int)section))
                    missing.Add(section);
            }

            if (missing.Count == 0)
                return null;

            missing.Sort();
            return "Not in this volume: " + FormatCanonical(missing);
        }

        /// <summary>
        /// Formats sorted unique section numbers as comma-separated runs. A run of one number stays a number; a longer run uses a dash.
        /// </summary>
        public static string FormatCanonical(IReadOnlyList<long> sortedUnique)
        {
            if (sortedUnique.Count == 0)
                return "";

            var parts = new List<string>();
            int index = 0;
            while (index < sortedUnique.Count)
            {
                long start = sortedUnique[index];
                long end = start;
                while (index + 1 < sortedUnique.Count && sortedUnique[index + 1] == end + 1)
                {
                    index++;
                    end = sortedUnique[index];
                }

                parts.Add(start == end ? start.ToString() : $"{start}-{end}");
                index++;
            }

            return string.Join(", ", parts);
        }

        private static SectionRangeParse Failed(string error)
            => new(success: false, isAllSections: false, interpretation: error, sections: []);

        private static bool TryParseToken(string token, out long start, out long end, out string? error)
        {
            error = null;
            Match range = RangeToken.Match(token);
            if (range.Success)
            {
                start = long.Parse(range.Groups[1].Value);
                end = long.Parse(range.Groups[2].Value);
                return true;
            }

            Match single = SingleToken.Match(token);
            if (single.Success)
            {
                start = end = long.Parse(single.Groups[1].Value);
                return true;
            }

            start = 0;
            end = 0;
            error = $"Could not read \"{token}\" as a section or range.";
            return false;
        }
    }
}
