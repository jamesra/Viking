using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Parses the store-parity table in <c>PARITY.md</c> and checks its Status column against the hand-written
    /// "Tally: N done, N partial, N missing, N n/a" sentence above it, so a row edit that forgets to update the
    /// tally fails the build instead of drifting silently (docs.md "No hand-kept counts"). Runs offline: no server,
    /// no database.
    /// </summary>
    [TestFixture]
    public class ParityTallyMatchesTableTests
    {
        private static readonly string[] KnownStatuses = { "done", "partial", "missing", "n/a" };

        [Test]
        public void TallySentenceMatchesStatusColumn()
        {
            var text = File.ReadAllText(RepoFile("Clients/WebAnnotationModel.gRPC/PARITY.md"));

            var counted = CountStatuses(text);
            var stated = ParseTallySentence(text);

            Assert.That(counted, Is.EqualTo(stated),
                "PARITY.md's Tally sentence no longer matches the Status column of the parity table; " +
                "update the sentence (or fix the row) in the same commit as the row edit.");
        }

        /// <summary>Counts each row's Status cell in the parity table (the first Markdown table in the file).</summary>
        private static Dictionary<string, int> CountStatuses(string markdown)
        {
            var counts = KnownStatuses.ToDictionary(s => s, _ => 0);
            var sawHeader = false;
            var sawSeparator = false;

            foreach (var line in markdown.Split('\n'))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("|", StringComparison.Ordinal))
                {
                    // A blank or prose line after the table body ends the table.
                    if (sawSeparator && trimmed.Length > 0)
                        break;
                    continue;
                }

                var cells = trimmed.Trim('|').Split('|').Select(c => c.Trim()).ToArray();

                if (!sawHeader)
                {
                    sawHeader = cells.Length > 0 && cells[0] == "WCF member";
                    continue;
                }
                if (!sawSeparator)
                {
                    sawSeparator = true; // the "|---|---|...|" row right after the header
                    continue;
                }

                Assert.That(cells.Length, Is.EqualTo(5), $"parity table row has {cells.Length} columns, expected 5: {trimmed}");
                var status = cells[2];
                var word = Regex.Match(status, @"^(done|partial|missing|n/a)\b", RegexOptions.IgnoreCase).Groups[1].Value.ToLowerInvariant();
                Assert.That(word, Is.Not.Empty, $"row's Status cell does not start with done/partial/missing/n/a: '{status}'");
                counts[word]++;
            }

            Assert.That(sawHeader, Is.True, "did not find the parity table header ('| WCF member | ... |') in PARITY.md");
            return counts;
        }

        /// <summary>Parses "Tally: N done, N partial, N missing, N n/a." into the same shape <see cref="CountStatuses"/> returns.</summary>
        private static Dictionary<string, int> ParseTallySentence(string markdown)
        {
            var match = Regex.Match(markdown,
                @"Tally:\s*(\d+)\s*done,\s*(\d+)\s*partial,\s*(\d+)\s*missing,\s*(\d+)\s*n/a");
            Assert.That(match.Success, Is.True, "did not find a 'Tally: N done, N partial, N missing, N n/a' sentence in PARITY.md");

            return new Dictionary<string, int>
            {
                ["done"] = int.Parse(match.Groups[1].Value),
                ["partial"] = int.Parse(match.Groups[2].Value),
                ["missing"] = int.Parse(match.Groups[3].Value),
                ["n/a"] = int.Parse(match.Groups[4].Value),
            };
        }

        /// <summary>Walks up from the test's output directory to find a path in the repo (mirrors SchemaDriftTests.RepoFile).</summary>
        private static string RepoFile(string relativePath)
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate) || Directory.Exists(candidate))
                    return candidate;
            }
            throw new FileNotFoundException("Not found above " + AppContext.BaseDirectory + ": " + relativePath);
        }
    }
}
