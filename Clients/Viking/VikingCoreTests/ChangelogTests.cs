using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Viking.UI;
using Viking.UI.WPF.Services;

namespace VikingCoreTests
{
    /// <summary>
    /// The user changelog (<c>VikingChangelog.md</c>) that Help > Change Log and the first start of each version show: how it is
    /// read, when it is shown, and that the file keeps the shape the window expects.
    /// </summary>
    [TestClass]
    public class ChangelogTests
    {
        [TestMethod]
        public void HeadingsBulletsAndContinuationLinesAreRead()
        {
            const string markdown = "# Viking changelog\n\n## 1.2.3 — 2026-01-02\n\n* First change\n  continued here.\n  * Nested detail\n* Second change\n";

            IReadOnlyList<ChangelogBlock> blocks = ChangelogMarkdown.Parse(markdown);

            Assert.AreEqual(5, blocks.Count);
            Assert.AreEqual(ChangelogBlockKind.Heading, blocks[0].Kind);
            Assert.AreEqual(1, blocks[0].Level);
            Assert.AreEqual("1.2.3 — 2026-01-02", blocks[1].PlainText);
            Assert.AreEqual(2, blocks[1].Level);
            Assert.AreEqual(ChangelogBlockKind.Bullet, blocks[2].Kind);
            Assert.AreEqual("First change continued here.", blocks[2].PlainText);
            Assert.AreEqual(1, blocks[3].Level, "an indented bullet nests");
            Assert.AreEqual("Second change", blocks[4].PlainText);
            Assert.AreEqual(0, blocks[4].Level);
        }

        [TestMethod]
        public void CommentsAreNotShown()
        {
            IReadOnlyList<ChangelogBlock> blocks = ChangelogMarkdown.Parse("# Title\n<!--\nFor developers\n-->\n## 1.0\n* Shown <!-- hidden -->\n");

            CollectionAssert.AreEqual(new[] { "Title", "1.0", "Shown" }, blocks.Select(b => b.PlainText.Trim()).ToArray());
        }

        [TestMethod]
        public void BoldCodeAndWebLinksAreStyled()
        {
            IReadOnlyList<ChangelogSpan> spans = ChangelogMarkdown.ParseInline("Set **Link visibility** with `F8`, see [the site](https://connectomes.utah.edu/) or [notes](docs/x.md).");

            Assert.IsTrue(spans.Single(s => s.Text == "Link visibility").Bold);
            Assert.IsTrue(spans.Single(s => s.Text == "F8").Code);
            Assert.AreEqual("https://connectomes.utah.edu/", spans.Single(s => s.Text == "the site").Link);
            Assert.IsNull(spans.Single(s => s.Text == "notes").Link, "a link into the repository is shown as text");
        }

        [TestMethod]
        public void TheChangelogIsShownOnlyTheFirstTimeAVersionStarts()
        {
            string marker = Path.Combine(Path.GetTempPath(), "VikingChangelogTest-" + Guid.NewGuid().ToString("N"), "ChangelogShown");
            try
            {
                Assert.IsTrue(ChangelogStartup.TryMarkFirstRun(marker));
                Assert.IsFalse(ChangelogStartup.TryMarkFirstRun(marker));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(marker)!, recursive: true);
            }
        }

        /// <summary>
        /// Every version in the shipped changelog has a heading followed by a bulleted list, and the newest heading is the
        /// version Viking.csproj builds, so the first start of a new version shows its own changes at the top.
        /// </summary>
        [TestMethod]
        public void TheShippedChangelogHasAHeadingAndBulletsPerVersionNewestFirst()
        {
            string root = FindRepositoryRoot();
            IReadOnlyList<ChangelogBlock> blocks = ChangelogMarkdown.Parse(File.ReadAllText(Path.Combine(root, "VikingChangelog.md")));

            List<int> versionHeadings = [.. Enumerable.Range(0, blocks.Count).Where(i => blocks[i].Kind == ChangelogBlockKind.Heading && blocks[i].Level == 2)];
            Assert.IsTrue(versionHeadings.Count > 0, "the changelog has a heading per version");
            foreach (int i in versionHeadings)
            {
                int next = versionHeadings.Where(j => j > i).DefaultIfEmpty(blocks.Count).First();
                Assert.IsTrue(Enumerable.Range(i + 1, next - i - 1).Any(j => blocks[j].Kind == ChangelogBlockKind.Bullet),
                    $"version \"{blocks[i].PlainText}\" has no bulleted list");
            }

            string csproj = File.ReadAllText(Path.Combine(root, "Clients", "Viking", "Viking", "Viking.csproj"));
            string built = Regex.Match(csproj, @"<ApplicationVersion>(\d+\.\d+\.\d+)").Groups[1].Value;
            StringAssert.StartsWith(blocks[versionHeadings[0]].PlainText, built + " ",
                "the newest heading is the version being built; rename it when Viking.csproj's version changes");
        }

        private static string FindRepositoryRoot()
        {
            for (DirectoryInfo dir = new(AppDomain.CurrentDomain.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "VikingChangelog.md")))
                    return dir.FullName;
            }

            Assert.Inconclusive("VikingChangelog.md is not above the test folder.");
            return null!;
        }
    }
}
