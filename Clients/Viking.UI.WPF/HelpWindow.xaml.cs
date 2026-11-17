#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Viking.Common;

namespace Viking.UI.WPF
{
    /// <summary>
    /// The command help in a window of its own (F1, or Help > Command Help Window), which the user can move and resize.
    /// The viewer sets <see cref="Entries"/> to each command's help as the command starts, and <see cref="AlwaysActive"/> to
    /// the keys that work whatever command runs. Entries are grouped by <see cref="HelpEntry.Section"/> into collapsible
    /// sections (annotation shortcuts, the current command, mouse over, always active, status bar; empty ones are left out). Within a section rows
    /// are drawn as on the help bar (<see cref="HelpRowConverter"/>), arranged by <see cref="HelpEntry.Arrange"/>, and flow
    /// down as many columns as fit the window's width (<see cref="ColumnCount"/>); plain-text lines span the full width above them.
    /// </summary>
    public partial class HelpWindow : Window
    {
        /// <summary>Width in device-independent pixels each column needs before the window adds another.</summary>
        public const double ColumnWidth = 420;

        /// <summary>How far a section's or group's rows sit right of its heading's arrow, so they read as under the heading.</summary>
        public const double SectionIndent = 20;

        private static readonly HelpRowConverter RowConverter = new();

        /// <summary>Which sections the user collapsed; kept while Viking runs, so reopening the window or changing command keeps them.</summary>
        private static readonly HashSet<HelpSection> Collapsed = [];

        /// <summary>Which groups (<see cref="HelpEntry.Group"/>) inside a section the user collapsed, kept like <see cref="Collapsed"/>.</summary>
        private static readonly HashSet<(HelpSection, string)> CollapsedGroups = [];

        private ObservableCollection<HelpEntry>? _entries;
        private IReadOnlyList<HelpEntry> _alwaysActive = [];
        private IReadOnlyList<(HelpSection Section, IReadOnlyList<HelpEntry> Rows)> _sections = [];
        private int _columnsShown = -1;

        /// <param name="ownerHandle">The Win32 window the help window stays above (the main Viking form); zero for none.</param>
        public HelpWindow(IntPtr ownerHandle)
        {
            InitializeComponent();

            if (ownerHandle != IntPtr.Zero)
                new WindowInteropHelper(this).Owner = ownerHandle;

            SizeChanged += (_, _) => Layout(force: false);
        }

        /// <summary>The active command's help. The window redraws whenever the collection is replaced or changes.</summary>
        public ObservableCollection<HelpEntry>? Entries
        {
            get => _entries;
            set
            {
                if (ReferenceEquals(_entries, value))
                    return;

                if (_entries is not null)
                    _entries.CollectionChanged -= OnCollectionChanged;

                _entries = value;

                if (_entries is not null)
                    _entries.CollectionChanged += OnCollectionChanged;

                Rebuild();
            }
        }

        /// <summary>
        /// Help listed whatever command runs, added to <see cref="Entries"/>: the viewer's and the overlays' keys, and the
        /// annotation shortcuts while they work. Each entry goes in its own <see cref="HelpEntry.Section"/>.
        /// </summary>
        public IReadOnlyList<HelpEntry> AlwaysActive
        {
            get => _alwaysActive;
            set
            {
                _alwaysActive = value ?? [];
                Rebuild();
            }
        }

        /// <summary>The order the sections are shown in, top to bottom.</summary>
        public static readonly HelpSection[] SectionOrder =
        [
            HelpSection.AnnotationShortcuts,
            HelpSection.Command,
            HelpSection.MouseOver,
            HelpSection.AlwaysActive,
            HelpSection.StatusBar,
        ];

        /// <summary>The heading of a section.</summary>
        public static string TitleOf(HelpSection section) => section switch
        {
            HelpSection.AnnotationShortcuts => "Annotation shortcuts",
            HelpSection.Command => "Active command",
            HelpSection.MouseOver => "Mouse over",
            HelpSection.AlwaysActive => "Always active",
            HelpSection.StatusBar => "Status bar",
            _ => section.ToString(),
        };

        /// <summary>
        /// The sections to show, in <see cref="SectionOrder"/>; sections with no entries are left out. A section's rows are its
        /// entries with no <see cref="HelpEntry.Group"/>, arranged by <see cref="HelpEntry.Arrange"/>, then each group's
        /// entries, arranged the same way, the groups in the order their first entry was given.
        /// </summary>
        public static IReadOnlyList<(HelpSection Section, IReadOnlyList<HelpEntry> Rows)> GroupIntoSections(IEnumerable<HelpEntry> entries)
        {
            HelpEntry[] all = [.. entries];
            List<(HelpSection, IReadOnlyList<HelpEntry>)> sections = [];
            foreach (HelpSection section in SectionOrder)
            {
                HelpEntry[] inSection = [.. all.Where(e => e.Section == section)];
                List<HelpEntry> rows = [.. HelpEntry.Arrange(inSection.Where(e => e.Group is null))];
                foreach (string group in GroupsInOrder(inSection))
                    rows.AddRange(HelpEntry.Arrange(inSection.Where(e => e.Group == group)));

                if (rows.Count > 0)
                    sections.Add((section, rows));
            }

            return sections;
        }

        /// <summary>The distinct <see cref="HelpEntry.Group"/> names of <paramref name="entries"/>, in the order each first appears.</summary>
        private static List<string> GroupsInOrder(IEnumerable<HelpEntry> entries)
        {
            List<string> groups = [];
            foreach (HelpEntry entry in entries)
            {
                if (entry.Group is { } group && !groups.Contains(group))
                    groups.Add(group);
            }

            return groups;
        }

        /// <summary>
        /// Splits a section's keyed rows into runs of consecutive rows with the same <see cref="HelpEntry.Group"/>, as
        /// <see cref="GroupIntoSections"/> lays them out: the ungrouped run (Group null) first, then one run per group.
        /// </summary>
        public static IReadOnlyList<(string? Group, IReadOnlyList<HelpEntry> Rows)> SplitIntoGroups(IReadOnlyList<HelpEntry> keyed)
        {
            List<(string?, IReadOnlyList<HelpEntry>)> runs = [];
            int start = 0;
            for (int i = 1; i <= keyed.Count; i++)
            {
                if (i == keyed.Count || keyed[i].Group != keyed[start].Group)
                {
                    runs.Add((keyed[start].Group, keyed.Skip(start).Take(i - start).ToArray()));
                    start = i;
                }
            }

            return runs;
        }

        /// <summary>
        /// How many columns <paramref name="rowCount"/> rows take in a window whose content is <paramref name="width"/> wide: one per
        /// <see cref="ColumnWidth"/>, at least one and never more than there are rows.
        /// </summary>
        public static int ColumnCount(double width, int rowCount)
        {
            if (rowCount <= 1 || double.IsNaN(width) || width <= 0)
                return 1;

            int fit = (int)Math.Floor(width / ColumnWidth);
            return Math.Max(1, Math.Min(fit, rowCount));
        }

        /// <summary>
        /// Splits <paramref name="rowCount"/> rows into <paramref name="columns"/> runs of consecutive rows, as even as possible,
        /// the earlier columns taking the extra row. Returns each column's first row and row count.
        /// </summary>
        public static IReadOnlyList<(int Start, int Count)> SplitIntoColumns(int rowCount, int columns)
        {
            columns = Math.Max(1, Math.Min(columns, Math.Max(rowCount, 1)));
            List<(int, int)> runs = new(columns);
            int start = 0;
            for (int c = 0; c < columns; c++)
            {
                int count = (rowCount / columns) + (c < rowCount % columns ? 1 : 0);
                runs.Add((start, count));
                start += count;
            }

            return runs;
        }

        private bool _rebuildQueued;

        /// <summary>
        /// The idle command clears and refills its collection one entry at a time whenever the annotation under the mouse
        /// changes, so the window redraws once after the last change instead of once per entry.
        /// </summary>
        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_rebuildQueued)
                return;

            _rebuildQueued = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _rebuildQueued = false;
                Rebuild();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void Rebuild()
        {
            IEnumerable<HelpEntry> command = _entries is { } entries ? entries : [];
            _sections = GroupIntoSections(command.Concat(_alwaysActive));
            Layout(force: true);
        }

        /// <summary>The widest column count any section uses at <paramref name="width"/>; a change of it is what forces a relayout on resize.</summary>
        private int ColumnsAt(double width) => _sections.Count == 0 ? 1 : _sections.Max(s => ColumnCount(width, SplitAtDivider(s.Rows).Keyed.Count));

        /// <summary>Lays the sections out; on a resize only when the number of columns changes.</summary>
        private void Layout(bool force)
        {
            double width = Sections.ActualWidth > 0 ? Sections.ActualWidth : Width - 40;
            int columns = ColumnsAt(width);
            if (!force && columns == _columnsShown)
                return;

            _columnsShown = columns;
            Sections.Children.Clear();
            foreach ((HelpSection section, IReadOnlyList<HelpEntry> rows) in _sections)
                Sections.Children.Add(SectionView(section, rows, width));
        }

        /// <summary>
        /// Splits a section's arranged rows (<see cref="HelpEntry.Arrange"/>) at its <see cref="HelpEntry.Divider"/>: the
        /// plain-text lines before it and the keyed entries after it. A section without a divider is all one kind.
        /// </summary>
        public static (IReadOnlyList<HelpEntry> Text, IReadOnlyList<HelpEntry> Keyed) SplitAtDivider(IReadOnlyList<HelpEntry> rows)
        {
            HelpEntry[] text = [.. rows.Where(r => !r.IsDivider && !r.IsKeyed)];
            HelpEntry[] keyed = [.. rows.Where(r => !r.IsDivider && r.IsKeyed)];
            return (text, keyed);
        }

        /// <summary>
        /// One section: its plain-text lines stacked down the full width, a full-width divider when there are both kinds,
        /// then the keyed entries in as many columns as fit <paramref name="width"/>, each <see cref="HelpEntry.Group"/> under a
        /// collapsible heading of its own. Only keyed rows are split into columns, so a divider never lands inside a column as
        /// a stray bar.
        /// </summary>
        private static Expander SectionView(HelpSection section, IReadOnlyList<HelpEntry> rows, double width)
        {
            (IReadOnlyList<HelpEntry> text, IReadOnlyList<HelpEntry> keyed) = SplitAtDivider(rows);
            // Indented under the section heading, as a group's rows are under the group's heading.
            StackPanel body = new() { Margin = new Thickness(SectionIndent, 4, 0, 0) };
            foreach (HelpEntry line in text)
            {
                if (Row(line) is { } row)
                    body.Children.Add(row);
            }

            if (text.Count > 0 && keyed.Count > 0)
                body.Children.Add(new Separator { Margin = new Thickness(0, 3, 0, 3) });

            foreach ((string? group, IReadOnlyList<HelpEntry> groupRows) in SplitIntoGroups(keyed))
            {
                Grid grid = Columns(groupRows, width);
                if (group is null)
                {
                    body.Children.Add(grid);
                    continue;
                }

                Expander groupView = new()
                {
                    Header = new TextBlock { Text = group, FontStyle = FontStyles.Italic },
                    IsExpanded = !CollapsedGroups.Contains((section, group)),
                    Content = grid,
                    Margin = new Thickness(0, 2, 0, 2),
                };
                grid.Margin = new Thickness(SectionIndent, 2, 0, 0);
                groupView.Expanded += (_, _) => CollapsedGroups.Remove((section, group));
                groupView.Collapsed += (_, _) => CollapsedGroups.Add((section, group));
                body.Children.Add(groupView);
            }

            Expander expander = new()
            {
                Header = new TextBlock { Text = TitleOf(section), FontWeight = FontWeights.SemiBold },
                IsExpanded = !Collapsed.Contains(section),
                Content = body,
                Margin = new Thickness(0, 0, 0, 6),
            };
            expander.Expanded += (_, _) => Collapsed.Remove(section);
            expander.Collapsed += (_, _) => Collapsed.Add(section);
            return expander;
        }

        /// <summary>Keyed rows flowing down as many columns as fit <paramref name="width"/>, each column's keys lined up at one right edge.</summary>
        private static Grid Columns(IReadOnlyList<HelpEntry> rows, double width)
        {
            Grid grid = new();
            IReadOnlyList<(int Start, int Count)> runs = SplitIntoColumns(rows.Count, ColumnCount(width, rows.Count));
            for (int c = 0; c < runs.Count; c++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                StackPanel column = new() { Margin = new Thickness(c == 0 ? 0 : 16, 0, 0, 0) };
                // Each column lines its keys and toggle marks up (HelpRowConverter.KeysColumn, ToggleColumn).
                Grid.SetIsSharedSizeScope(column, true);
                (int start, int count) = runs[c];
                for (int i = start; i < start + count; i++)
                {
                    if (Row(rows[i]) is { } row)
                        column.Children.Add(row);
                }

                Grid.SetColumn(column, c);
                grid.Children.Add(column);
            }

            return grid;
        }

        /// <summary>One help row drawn as on the help bar, with 1 px above and below.</summary>
        private static FrameworkElement? Row(HelpEntry entry)
        {
            if (RowConverter.Convert(entry, typeof(object), null!, CultureInfo.CurrentCulture) is not FrameworkElement row)
                return null;

            row.Margin = new Thickness(row.Margin.Left, row.Margin.Top + 1, row.Margin.Right, row.Margin.Bottom + 1);
            return row;
        }
    }
}
