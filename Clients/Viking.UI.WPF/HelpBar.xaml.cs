#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Viking.Common;

namespace Viking.UI.WPF
{
    /// <summary>
    /// The command help bar under the viewer. It shows one help entry at a time (the viewer advances it on a timer) and drops
    /// down the whole list (F1). Entries are arranged by <see cref="HelpEntry.Arrange"/>: plain-text lines first in their order,
    /// a divider, then keyed lines sorted by key and modifiers. Keys are drawn as keycaps, mouse actions as mouse icons.
    /// </summary>
    public partial class HelpBar : UserControl
    {
        public static readonly DependencyProperty EntriesProperty = DependencyProperty.Register(nameof(Entries),
            typeof(ObservableCollection<HelpEntry>), typeof(HelpBar),
            new FrameworkPropertyMetadata(null, OnEntriesChanged));

        private static readonly Random Random = new();

        private IReadOnlyList<HelpEntry> _arranged = [];

        public HelpBar()
        {
            InitializeComponent();
        }

        /// <summary>The command's help. The bar rebuilds its list whenever the collection changes.</summary>
        public ObservableCollection<HelpEntry>? Entries
        {
            get => (ObservableCollection<HelpEntry>?)GetValue(EntriesProperty);
            set => SetValue(EntriesProperty, value);
        }

        public bool IsDropDownOpen
        {
            get => comboHelp.IsDropDownOpen;
            set => comboHelp.IsDropDownOpen = value;
        }

        /// <summary>Shows the next entry, skipping the divider; called by the viewer's timer.</summary>
        public void ShowNext()
        {
            if (_arranged.Count == 0)
                return;

            int index = comboHelp.SelectedIndex;
            for (int step = 0; step < _arranged.Count; step++)
            {
                index = (index + 1) % _arranged.Count;
                if (!_arranged[index].IsDivider)
                {
                    comboHelp.SelectedIndex = index;
                    return;
                }
            }
        }

        private static void OnEntriesChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            if (o is not HelpBar bar)
                return;

            if (e.OldValue is ObservableCollection<HelpEntry> oldEntries)
                oldEntries.CollectionChanged -= bar.OnCollectionChanged;

            if (e.NewValue is ObservableCollection<HelpEntry> newEntries)
                newEntries.CollectionChanged += bar.OnCollectionChanged;

            bar.Rebuild();
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

        /// <summary>Arranges the entries and starts at a random one, so a long list is not always read from the top.</summary>
        private void Rebuild()
        {
            _arranged = Entries is { } entries ? HelpEntry.Arrange(entries) : [];
            comboHelp.ItemsSource = _arranged;

            int[] choices = Enumerable.Range(0, _arranged.Count).Where(i => !_arranged[i].IsDivider).ToArray();
            comboHelp.SelectedIndex = choices.Length > 0 ? choices[Random.Next(choices.Length)] : -1;
        }
    }

    /// <summary>
    /// Draws one help entry as a row: modifier keycaps and the input keycaps or mouse icons ("/" between alternatives),
    /// right-aligned in a shared column, then the description, the <see cref="HelpEntry.Where"/> text and ⇌ for a toggle. A
    /// new row is made on every call, because the closed bar and the dropdown each need their own.
    /// </summary>
    public sealed class HelpRowConverter : IValueConverter
    {
        // The look of the annotation gallery's keycaps (Sam2Trainer annotation-gallery app.css .keycap), sized for one line.
        private static readonly Brush KeycapFill = Frozen(new SolidColorBrush(Color.FromArgb(240, 35, 40, 51)));
        private static readonly Brush KeycapBorder = Frozen(new SolidColorBrush(Color.FromRgb(0x4b, 0x55, 0x63)));
        private static readonly Brush KeycapText = Frozen(new SolidColorBrush(Color.FromRgb(0xf8, 0xfa, 0xfc)));
        private static readonly Brush IconInk = Frozen(new SolidColorBrush(Color.FromRgb(0x23, 0x28, 0x33)));
        private static readonly Brush IconPressed = Frozen(new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb)));

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not HelpEntry entry)
                return value;

            if (entry.IsDivider)
                return new Rectangle { Height = 1, Fill = KeycapBorder, Margin = new Thickness(0, 3, 0, 3), MinWidth = 200 };

            StackPanel description = DescriptionOf(entry);
            if (!entry.IsKeyed)
            {
                description.Margin = new Thickness(IndentWidth * entry.Indent, 0, 0, 0);
                return description;
            }

            StackPanel keys = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            foreach (HelpModifiers modifier in entry.ModifierKeys())
                keys.Children.Add(Keycap(HelpEntry.LabelFor(modifier)));

            HelpInput[] inputs = entry.Inputs ?? [];
            for (int i = 0; i < inputs.Length; i++)
            {
                if (i > 0)
                    keys.Children.Add(Text("/", new Thickness(0, 0, 4, 0)));

                keys.Children.Add(Input(inputs[i], entry.Label));
            }

            // Keys right-aligned in a column every row of the list shares (KeysColumn), then ⇌ for a toggle in a column of its
            // own (ToggleColumn, no width when no row is a toggle), then the description left-aligned.
            Grid row = new();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = KeysColumn });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = ToggleColumn });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(keys);
            if (entry.IsToggle)
            {
                TextBlock mark = new()
                {
                    Text = HelpEntry.ToggleMark,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                    ToolTip = "Switches on or off",
                };
                Grid.SetColumn(mark, 1);
                row.Children.Add(mark);
            }

            description.Margin = new Thickness(6 + (IndentWidth * entry.Indent), 0, 0, 0);
            Grid.SetColumn(description, 2);
            row.Children.Add(description);
            return row;
        }

        /// <summary>
        /// The <see cref="Grid.SharedSizeGroup"/> of the keys column. A list that sets <see cref="Grid.IsSharedSizeScopeProperty"/>
        /// lines its rows' keys up at one right edge.
        /// </summary>
        public const string KeysColumn = "HelpKeys";

        /// <summary>How far each <see cref="HelpEntry.Indent"/> level moves a row's description right, in device-independent pixels.</summary>
        public const double IndentWidth = 16;

        /// <summary>The <see cref="Grid.SharedSizeGroup"/> of the column ⇌ is drawn in, between the keys and the description.</summary>
        public const string ToggleColumn = "HelpToggle";

        /// <summary>
        /// The description, then the <see cref="HelpEntry.Where"/> text in grey ("on a control point"). A toggle's ⇌ is drawn
        /// before it, in the keyed row's toggle column.
        /// </summary>
        private static StackPanel DescriptionOf(HelpEntry entry)
        {
            StackPanel panel = new() { Orientation = Orientation.Horizontal };
            panel.Children.Add(Text(entry.Description));
            if (!string.IsNullOrEmpty(entry.Where))
                panel.Children.Add(new TextBlock { Text = entry.Where!, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) });

            return panel;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        private static FrameworkElement Input(HelpInput input, string? label) => HelpEntry.KindOf(input) switch
        {
            HelpInputKind.Mouse => MouseAction(input),
            _ => Keycap(HelpEntry.LabelFor(input, label)),
        };

        private static TextBlock Text(string text, Thickness? margin = null) => new()
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = margin ?? new Thickness(0),
        };

        private static Border Keycap(string label) => new()
        {
            Background = KeycapFill,
            BorderBrush = KeycapBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5, 0, 5, 0),
            Margin = new Thickness(0, 0, 3, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = label,
                Foreground = KeycapText,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        /// <summary>A mouse icon with the button used filled in, followed by the action's words ("drag", "double-click").</summary>
        private static FrameworkElement MouseAction(HelpInput input)
        {
            StackPanel panel = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(MouseIcon(input));
            panel.Children.Add(Text(HelpEntry.LabelFor(input), new Thickness(3, 0, 0, 0)));
            return panel;
        }

        /// <summary>
        /// A 12 x 18 mouse (scaled to 15 px tall to fit a keycap row): a rounded body split into left and right buttons with a wheel between them. The pressed part is
        /// filled: a button for clicks and drags, the wheel for the wheel, the wheel with side ticks for the tilt wheel, and two
        /// marks on the left side for the back and forward buttons.
        /// </summary>
        internal static FrameworkElement MouseIcon(HelpInput input)
        {
            Canvas canvas = new() { Width = 14, Height = 18, VerticalAlignment = VerticalAlignment.Center, SnapsToDevicePixels = true, LayoutTransform = new ScaleTransform(15.0 / 18.0, 15.0 / 18.0) };
            RectangleGeometry body = new(new Rect(1, 0.5, 12, 17), 6, 6);
            System.Windows.Media.Geometry left = new CombinedGeometry(GeometryCombineMode.Intersect, body, new RectangleGeometry(new Rect(1, 0.5, 6, 7.5)));
            System.Windows.Media.Geometry right = new CombinedGeometry(GeometryCombineMode.Intersect, body, new RectangleGeometry(new Rect(7, 0.5, 6, 7.5)));

            bool leftPressed = input is HelpInput.LeftClick or HelpInput.LeftDrag or HelpInput.LeftDoubleClick;
            bool rightPressed = input is HelpInput.RightClick or HelpInput.RightDrag or HelpInput.RightDoubleClick;
            bool wheelPressed = input is HelpInput.MiddleClick or HelpInput.Wheel or HelpInput.TiltWheel;

            canvas.Children.Add(new Path { Data = body, Fill = Brushes.White, Stroke = IconInk, StrokeThickness = 1 });
            if (leftPressed)
                canvas.Children.Add(new Path { Data = left, Fill = IconPressed });
            if (rightPressed)
                canvas.Children.Add(new Path { Data = right, Fill = IconPressed });

            canvas.Children.Add(new Path { Data = new LineGeometry(new Point(1, 8), new Point(13, 8)), Stroke = IconInk, StrokeThickness = 1 });
            canvas.Children.Add(new Path { Data = new LineGeometry(new Point(7, 0.5), new Point(7, 8)), Stroke = IconInk, StrokeThickness = 1 });
            canvas.Children.Add(new Path
            {
                Data = new RectangleGeometry(new Rect(5.5, 2, 3, 4.5), 1.5, 1.5),
                Fill = wheelPressed ? IconPressed : Brushes.White,
                Stroke = IconInk,
                StrokeThickness = 1,
            });

            if (input == HelpInput.TiltWheel)
            {
                canvas.Children.Add(new Path { Data = System.Windows.Media.Geometry.Parse("M 4,4.25 L 2.5,3.25 L 2.5,5.25 Z"), Fill = IconPressed });
                canvas.Children.Add(new Path { Data = System.Windows.Media.Geometry.Parse("M 10,4.25 L 11.5,3.25 L 11.5,5.25 Z"), Fill = IconPressed });
            }

            if (input == HelpInput.BackForwardButtons)
            {
                canvas.Children.Add(new Path { Data = new RectangleGeometry(new Rect(0, 9, 2, 2.5)), Fill = IconPressed });
                canvas.Children.Add(new Path { Data = new RectangleGeometry(new Rect(0, 12.5, 2, 2.5)), Fill = IconPressed });
            }

            return canvas;
        }

        private static readonly Brush TagFill = Frozen(new SolidColorBrush(Color.FromRgb(0xfd, 0xe6, 0x8a)));

        /// <summary>
        /// A 28 x 14 luggage tag (drawn 15 px tall to fit a keycap row): pointed at the left with a punched hole, rounded at
        /// the right, manila filled, with "tag" printed small on it so the shape is not read as an arrow or a label.
        /// Not drawn anywhere yet: it was tried after tag hotkeys' descriptions (2026-11-16) and left off, but kept for later.
        /// Its proportions and colour need another look before it is used.
        /// </summary>
        internal static FrameworkElement TagIcon()
        {
            Canvas canvas = new() { Width = 28, Height = 14, VerticalAlignment = VerticalAlignment.Center, SnapsToDevicePixels = true, LayoutTransform = new ScaleTransform(15.0 / 14.0, 15.0 / 14.0) };
            canvas.Children.Add(new Path
            {
                Data = System.Windows.Media.Geometry.Parse("M 0.5,7 L 6.5,0.5 L 25.5,0.5 Q 27.5,0.5 27.5,2.5 L 27.5,11.5 Q 27.5,13.5 25.5,13.5 L 6.5,13.5 Z"),
                Fill = TagFill,
                Stroke = IconInk,
                StrokeThickness = 1,
                StrokeLineJoin = PenLineJoin.Round,
            });
            canvas.Children.Add(new Path { Data = new EllipseGeometry(new Point(5.5, 7), 1.6, 1.6), Fill = Brushes.White, Stroke = IconInk, StrokeThickness = 0.8 });

            TextBlock word = new()
            {
                Text = "tag",
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Foreground = IconInk,
                Width = 18,
                TextAlignment = TextAlignment.Center,
                LineHeight = 8,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            };
            Canvas.SetLeft(word, 8.5);
            Canvas.SetTop(word, 2.5);
            canvas.Children.Add(word);
            return canvas;
        }

        private static Brush Frozen(Brush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
