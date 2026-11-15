#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Viking.Common;

namespace Viking.UI.WPF
{
    /// <summary>
    /// The command help in a window of its own (Shift+F1, or Help > Command Help Window), which the user can move and resize.
    /// The viewer sets <see cref="Entries"/> to each command's help as the command starts. Rows are drawn as on the help bar
    /// (<see cref="HelpRowConverter"/>), arranged by <see cref="HelpEntry.Arrange"/>, and flow down as many columns as fit the
    /// window's width (<see cref="ColumnCount"/>), reading top to bottom and then left to right.
    /// </summary>
    public partial class HelpWindow : Window
    {
        /// <summary>Width in device-independent pixels each column needs before the window adds another.</summary>
        public const double ColumnWidth = 420;

        private static readonly HelpRowConverter RowConverter = new();

        private ObservableCollection<HelpEntry>? _entries;
        private IReadOnlyList<HelpEntry> _arranged = [];
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

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

        private void Rebuild()
        {
            _arranged = _entries is { } entries ? HelpEntry.Arrange(entries) : [];
            Layout(force: true);
        }

        /// <summary>Lays the rows out in columns; on a resize only when the number of columns changes.</summary>
        private void Layout(bool force)
        {
            double width = Columns.ActualWidth > 0 ? Columns.ActualWidth : Width - 40;
            int columns = ColumnCount(width, _arranged.Count);
            if (!force && columns == _columnsShown)
                return;

            _columnsShown = columns;
            Columns.Children.Clear();
            Columns.ColumnDefinitions.Clear();

            IReadOnlyList<(int Start, int Count)> runs = SplitIntoColumns(_arranged.Count, columns);
            for (int c = 0; c < runs.Count; c++)
            {
                Columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                StackPanel column = new() { Margin = new Thickness(c == 0 ? 0 : 16, 0, 0, 0) };
                (int start, int count) = runs[c];
                for (int i = start; i < start + count; i++)
                {
                    if (RowConverter.Convert(_arranged[i], typeof(object), null!, CultureInfo.CurrentCulture) is FrameworkElement row)
                    {
                        row.Margin = new Thickness(row.Margin.Left, row.Margin.Top + 2, row.Margin.Right, row.Margin.Bottom + 2);
                        column.Children.Add(row);
                    }
                }

                Grid.SetColumn(column, c);
                Columns.Children.Add(column);
            }
        }
    }
}
