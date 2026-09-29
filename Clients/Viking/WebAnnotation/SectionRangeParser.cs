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
    /// Requested section numbers split by whether they exist in the open volume.
    /// The volume-position dialog shows <see cref="Interpretation"/> and starts the job only when <see cref="CanStart"/> is true.
    /// </summary>
    public readonly struct VolumeSectionSelection
    {
        /// <summary>Requested sections that exist in the volume, sorted.</summary>
        public IReadOnlyList<long> Present { get; }

        /// <summary>Requested sections that are not in the volume, sorted. These are not updated.</summary>
        public IReadOnlyList<long> Missing { get; }

        /// <summary>Dialog text: the sections that will run, plus any numbers that were skipped.</summary>
        public string Interpretation { get; }

        /// <summary>False when every requested section is missing, so the dialog must not start.</summary>
        public bool CanStart => Present.Count > 0;

        public VolumeSectionSelection(IReadOnlyList<long> present, IReadOnlyList<long> missing, string interpretation)
        {
            Present = present;
            Missing = missing;
            Interpretation = interpretation;
        }
    }

    /// <summary>
    /// Parses free-text section lists into a sorted, de-duplicated set and a canonical range string.
    /// Called by the volume-position dialog as the user types. Adjacent and overlapping numbers collapse into inclusive ranges.
    /// A range may include numbers the volume does not have; <see cref="SelectInVolume"/> drops those before the job starts.
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
        /// Keeps requested sections that exist in the open volume and lists the rest as skipped.
        /// Called by the volume-position dialog so one range can cross gaps. A missing section is omitted; the job still runs on every number that is present.
        /// <see cref="VolumeSectionSelection.CanStart"/> is false only when none of the requested numbers exist.
        /// </summary>
        public static VolumeSectionSelection SelectInVolume(IReadOnlyList<long> sections, IEnumerable<int> volumeSectionNumbers)
        {
            var volume = volumeSectionNumbers as HashSet<int> ?? new HashSet<int>(volumeSectionNumbers);
            var present = new List<long>();
            var missing = new List<long>();
            foreach (long section in sections)
            {
                if (section >= int.MinValue && section <= int.MaxValue && volume.Contains((int)section))
                    present.Add(section);
                else
                    missing.Add(section);
            }

            present.Sort();
            missing.Sort();

            string interpretation;
            if (present.Count == 0)
                interpretation = "None of these sections are in this volume: " + FormatCanonical(missing);
            else if (missing.Count == 0)
                interpretation = FormatCanonical(present);
            else
                interpretation = "Will update: " + FormatCanonical(present) + Environment.NewLine
                    + "Skipped (not in this volume): " + FormatCanonical(missing);

            return new VolumeSectionSelection(present, missing, interpretation);
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
