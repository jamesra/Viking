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

        private static readonly HelpRowConverter RowConverter = new();

        /// <summary>Which sections the user collapsed; kept while Viking runs, so reopening the window or changing command keeps them.</summary>
        private static readonly HashSet<HelpSection> Collapsed = [];

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
        /// The sections to show, in <see cref="SectionOrder"/>, each with its entries arranged by
        /// <see cref="HelpEntry.Arrange"/>; sections with no entries are left out.
        /// </summary>
        public static IReadOnlyList<(HelpSection Section, IReadOnlyList<HelpEntry> Rows)> GroupIntoSections(IEnumerable<HelpEntry> entries)
        {
            HelpEntry[] all = [.. entries];
            List<(HelpSection, IReadOnlyList<HelpEntry>)> sections = [];
            foreach (HelpSection section in SectionOrder)
            {
                IReadOnlyList<HelpEntry> rows = HelpEntry.Arrange(all.Where(e => e.Section == section));
                if (rows.Count > 0)
                    sections.Add((section, rows));
            }

            return sections;
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
        /// then the keyed entries in as many columns as fit <paramref name="width"/>. Only keyed rows are split into columns,
        /// so a divider never lands inside a column as a stray bar.
        /// </summary>
        private static Expander SectionView(HelpSection section, IReadOnlyList<HelpEntry> rows, double width)
        {
            (IReadOnlyList<HelpEntry> text, IReadOnlyList<HelpEntry> keyed) = SplitAtDivider(rows);
            StackPanel body = new() { Margin = new Thickness(4, 4, 0, 0) };
            foreach (HelpEntry line in text)
            {
                if (Row(line) is { } row)
                    body.Children.Add(row);
            }

            if (text.Count > 0 && keyed.Count > 0)
                body.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });

            if (keyed.Count > 0)
            {
                Grid grid = new();
                IReadOnlyList<(int Start, int Count)> runs = SplitIntoColumns(keyed.Count, ColumnCount(width, keyed.Count));
                for (int c = 0; c < runs.Count; c++)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    StackPanel column = new() { Margin = new Thickness(c == 0 ? 0 : 16, 0, 0, 0) };
                    (int start, int count) = runs[c];
                    for (int i = start; i < start + count; i++)
                    {
                        if (Row(keyed[i]) is { } row)
                            column.Children.Add(row);
                    }

                    Grid.SetColumn(column, c);
                    grid.Children.Add(column);
                }

                body.Children.Add(grid);
            }

            Expander expander = new()
            {
                Header = new TextBlock { Text = TitleOf(section), FontWeight = FontWeights.SemiBold },
                IsExpanded = !Collapsed.Contains(section),
                Content = body,
                Margin = new Thickness(0, 0, 0, 10),
            };
            expander.Expanded += (_, _) => Collapsed.Remove(section);
            expander.Collapsed += (_, _) => Collapsed.Add(section);
            return expander;
        }

        /// <summary>One help row drawn as on the help bar, with a little space above and below.</summary>
        private static FrameworkElement? Row(HelpEntry entry)
        {
            if (RowConverter.Convert(entry, typeof(object), null!, CultureInfo.CurrentCulture) is not FrameworkElement row)
                return null;

            row.Margin = new Thickness(row.Margin.Left, row.Margin.Top + 2, row.Margin.Right, row.Margin.Bottom + 2);
            return row;
        }
    }
}
