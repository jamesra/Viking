using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Viking.UI.WPF.Services;

namespace Viking.UI.WPF.Forms
{
    /// <summary>
    /// Shows the user changelog (<c>VikingChangelog.md</c>, installed beside Viking.exe) as a formatted, scrollable document:
    /// a heading per version and its bulleted changes. Opened from Help > Change Log, and once when a new version first starts.
    /// </summary>
    public sealed class ChangelogWindow : Window
    {
        private static ChangelogWindow _open;

        private ChangelogWindow(IReadOnlyList<ChangelogBlock> blocks, string title, string intro)
        {
            Title = title;
            Width = 680;
            Height = 760;
            MinWidth = 360;
            MinHeight = 300;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            FlowDocumentScrollViewer viewer = new()
            {
                Document = BuildDocument(blocks, intro),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                IsToolBarVisible = false,
            };

            Button close = new()
            {
                Content = "Close",
                Width = 90,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Right,
                IsCancel = true,
                IsDefault = true,
            };
            close.Click += (_, _) => Close();

            DockPanel layout = new();
            DockPanel.SetDock(close, Dock.Bottom);
            layout.Children.Add(close);
            layout.Children.Add(viewer);
            Content = layout;

            Closed += (_, _) => _open = null;
        }

        /// <summary>
        /// Shows the changelog at <paramref name="path"/>, or brings the open one to the front. A missing or unreadable file is
        /// reported in a message box instead.
        /// </summary>
        /// <param name="title">The window title.</param>
        /// <param name="intro">A line shown above the changelog, or null.</param>
        /// <param name="beforeShow">
        /// Called with the new window before it is shown, or null. A Windows Forms host passes
        /// <c>ElementHost.EnableModelessKeyboardInterop</c> so the window gets keyboard input; this assembly does not reference
        /// Windows Forms.
        /// </param>
        public static void Show(string path, string title, string intro = null, Action<Window> beforeShow = null)
        {
            if (_open is not null)
            {
                _open.Activate();
                return;
            }

            string markdown;
            try
            {
                markdown = File.ReadAllText(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Trace.WriteLine($"Change log not shown: {e.Message}");
                MessageBox.Show($"The change log could not be read from {path}:\n{e.Message}", "Change Log", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _open = new ChangelogWindow(ChangelogMarkdown.Parse(markdown), title, intro);
            beforeShow?.Invoke(_open);
            _open.Show();
        }

        private static FlowDocument BuildDocument(IReadOnlyList<ChangelogBlock> blocks, string intro)
        {
            FlowDocument document = new()
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                PagePadding = new Thickness(18, 12, 18, 12),
                TextAlignment = TextAlignment.Left,
                Background = SystemColors.WindowBrush,
                Foreground = SystemColors.WindowTextBrush,
            };

            if (!string.IsNullOrWhiteSpace(intro))
                document.Blocks.Add(new Paragraph(new Run(intro)) { FontStyle = FontStyles.Italic, Margin = new Thickness(0, 0, 0, 8) });

            // Lists open at each bullet depth; a heading or paragraph closes them all
            List<List> lists = [];
            foreach (ChangelogBlock block in blocks)
            {
                if (block.Kind != ChangelogBlockKind.Bullet)
                    lists.Clear();

                switch (block.Kind)
                {
                    case ChangelogBlockKind.Heading:
                        Paragraph heading = new()
                        {
                            FontSize = block.Level == 1 ? 22 : block.Level == 2 ? 17 : 14,
                            FontWeight = FontWeights.SemiBold,
                            Margin = new Thickness(0, block.Level == 1 ? 0 : 14, 0, 4),
                        };
                        AddSpans(heading.Inlines, block.Spans);
                        document.Blocks.Add(heading);
                        break;

                    case ChangelogBlockKind.Paragraph:
                        Paragraph paragraph = new() { Margin = new Thickness(0, 0, 0, 6) };
                        AddSpans(paragraph.Inlines, block.Spans);
                        document.Blocks.Add(paragraph);
                        break;

                    case ChangelogBlockKind.Bullet:
                        int depth = Math.Min(block.Level, lists.Count);
                        if (lists.Count > depth + 1)
                            lists.RemoveRange(depth + 1, lists.Count - depth - 1);

                        if (lists.Count == depth)
                        {
                            List list = new() { MarkerStyle = depth == 0 ? TextMarkerStyle.Disc : TextMarkerStyle.Circle, Margin = new Thickness(0, 0, 0, 4), Padding = new Thickness(20, 0, 0, 0) };
                            if (depth == 0)
                                document.Blocks.Add(list);
                            else
                                lists[depth - 1].ListItems.LastListItem.Blocks.Add(list);
                            lists.Add(list);
                        }

                        Paragraph item = new() { Margin = new Thickness(0, 0, 0, 4) };
                        AddSpans(item.Inlines, block.Spans);
                        lists[depth].ListItems.Add(new ListItem(item));
                        break;
                }
            }

            return document;
        }

        private static void AddSpans(InlineCollection inlines, IReadOnlyList<ChangelogSpan> spans)
        {
            foreach (ChangelogSpan span in spans)
            {
                Run run = new(span.Text);
                if (span.Bold)
                    run.FontWeight = FontWeights.SemiBold;
                if (span.Code)
                    run.FontFamily = new FontFamily("Consolas");

                if (span.Link is null)
                {
                    inlines.Add(run);
                    continue;
                }

                Hyperlink link = new(run) { NavigateUri = new Uri(span.Link) };
                link.RequestNavigate += (_, e) =>
                {
                    Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                    e.Handled = true;
                };
                inlines.Add(link);
            }
        }
    }
}
