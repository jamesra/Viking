using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Viking.UI.WPF.Services
{
    /// <summary>What a block of the changelog is.</summary>
    public enum ChangelogBlockKind
    {
        Heading,
        Bullet,
        Paragraph,
    }

    /// <summary>A run of text with one style.</summary>
    public sealed class ChangelogSpan(string text, bool bold = false, bool code = false, string link = null)
    {
        public string Text { get; } = text;

        public bool Bold { get; } = bold;

        public bool Code { get; } = code;

        /// <summary>The link target, or null. Only http and https links are opened.</summary>
        public string Link { get; } = link;
    }

    /// <summary>A heading, bullet or paragraph of the changelog.</summary>
    public sealed class ChangelogBlock(ChangelogBlockKind kind, int level, IReadOnlyList<ChangelogSpan> spans)
    {
        public ChangelogBlockKind Kind { get; } = kind;

        /// <summary>The heading level (1 for #), or the bullet's nesting depth (0 at the margin). 0 for a paragraph.</summary>
        public int Level { get; } = level;

        public IReadOnlyList<ChangelogSpan> Spans { get; } = spans;

        public string PlainText
        {
            get
            {
                StringBuilder text = new();
                foreach (ChangelogSpan span in Spans)
                    text.Append(span.Text);
                return text.ToString();
            }
        }
    }

    /// <summary>
    /// Reads the user changelog (<c>VikingChangelog.md</c>): a heading per version with a bulleted list under it. Handles the
    /// Markdown the changelog uses: # headings, * or - bullets (nested by indenting two spaces), paragraphs, lines that
    /// continue the bullet or paragraph above, **bold**, `code` and [text](url) links. HTML comments are left out, so notes
    /// for developers can sit in the file without being shown.
    /// </summary>
    public static class ChangelogMarkdown
    {
        private static readonly Regex Comment = new(@"<!--.*?-->", RegexOptions.Singleline);
        private static readonly Regex Heading = new(@"^(#{1,6})\s+(.*)$");
        private static readonly Regex Bullet = new(@"^(\s*)[*-]\s+(.*)$");
        private static readonly Regex Inline = new(@"\*\*(?<bold>.+?)\*\*|`(?<code>[^`]+)`|\[(?<text>[^\]]+)\]\((?<url>[^)\s]+)\)");

        public static IReadOnlyList<ChangelogBlock> Parse(string markdown)
        {
            List<ChangelogBlock> blocks = [];
            if (string.IsNullOrEmpty(markdown))
                return blocks;

            string text = Comment.Replace(markdown.Replace("\r\n", "\n"), string.Empty);
            ChangelogBlockKind? openKind = null;
            int openLevel = 0;
            StringBuilder open = new();

            void Close()
            {
                if (openKind.HasValue && open.Length > 0)
                    blocks.Add(new ChangelogBlock(openKind.Value, openLevel, ParseInline(open.ToString())));
                openKind = null;
                open.Clear();
            }

            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.TrimEnd();
                if (line.Length == 0)
                {
                    Close();
                    continue;
                }

                Match heading = Heading.Match(line);
                if (heading.Success)
                {
                    Close();
                    blocks.Add(new ChangelogBlock(ChangelogBlockKind.Heading, heading.Groups[1].Length, ParseInline(heading.Groups[2].Value.Trim())));
                    continue;
                }

                Match bullet = Bullet.Match(line);
                if (bullet.Success)
                {
                    Close();
                    openKind = ChangelogBlockKind.Bullet;
                    openLevel = bullet.Groups[1].Value.Replace("\t", "  ").Length / 2;
                    open.Append(bullet.Groups[2].Value.Trim());
                    continue;
                }

                // A line under a bullet or paragraph continues it
                if (openKind is null)
                {
                    openKind = ChangelogBlockKind.Paragraph;
                    openLevel = 0;
                }
                else
                {
                    open.Append(' ');
                }

                open.Append(line.Trim());
            }

            Close();
            return blocks;
        }

        /// <summary>Splits a line into plain, **bold**, `code` and [link](url) spans.</summary>
        public static IReadOnlyList<ChangelogSpan> ParseInline(string text)
        {
            List<ChangelogSpan> spans = [];
            int at = 0;
            foreach (Match match in Inline.Matches(text))
            {
                if (match.Index > at)
                    spans.Add(new ChangelogSpan(text.Substring(at, match.Index - at)));

                if (match.Groups["bold"].Success)
                    spans.Add(new ChangelogSpan(match.Groups["bold"].Value, bold: true));
                else if (match.Groups["code"].Success)
                    spans.Add(new ChangelogSpan(match.Groups["code"].Value, code: true));
                else
                    spans.Add(new ChangelogSpan(match.Groups["text"].Value, link: IsWebLink(match.Groups["url"].Value) ? match.Groups["url"].Value : null));

                at = match.Index + match.Length;
            }

            if (at < text.Length)
                spans.Add(new ChangelogSpan(text.Substring(at)));

            return spans;
        }

        private static bool IsWebLink(string url) =>
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
