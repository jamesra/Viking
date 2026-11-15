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
    /// Draws one help entry as a row: modifier keycaps, the input keycaps or mouse icons ("/" between alternatives), the
    /// <see cref="HelpEntry.Where"/> text, then the description. A new row is made on every call, because the closed bar and
    /// the dropdown each need their own.
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
                return new Rectangle { Height = 1, Fill = KeycapBorder, Margin = new Thickness(0, 4, 0, 4), MinWidth = 200 };

            StackPanel row = new() { Orientation = Orientation.Horizontal };
            if (!entry.IsKeyed)
            {
                row.Children.Add(Text(entry.Description));
                return row;
            }

            foreach (HelpModifiers modifier in entry.ModifierKeys())
                row.Children.Add(Keycap(HelpEntry.LabelFor(modifier)));

            HelpInput[] inputs = entry.Inputs ?? [];
            for (int i = 0; i < inputs.Length; i++)
            {
                if (i > 0)
                    row.Children.Add(Text("/", new Thickness(0, 0, 4, 0)));

                row.Children.Add(Input(inputs[i], entry.Label));
            }

            if (!string.IsNullOrEmpty(entry.Where))
                row.Children.Add(Text(entry.Where!, new Thickness(0, 0, 4, 0)));

            row.Children.Add(Text(entry.Description, new Thickness(6, 0, 0, 0)));
            return row;
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
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(6, 0, 6, 1),
            Margin = new Thickness(0, 1, 4, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = label,
                Foreground = KeycapText,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
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
        /// A 12 x 18 mouse: a rounded body split into left and right buttons with a wheel between them. The pressed part is
        /// filled: a button for clicks and drags, the wheel for the wheel, the wheel with side ticks for the tilt wheel, and two
        /// marks on the left side for the back and forward buttons.
        /// </summary>
        internal static FrameworkElement MouseIcon(HelpInput input)
        {
            Canvas canvas = new() { Width = 14, Height = 18, VerticalAlignment = VerticalAlignment.Center, SnapsToDevicePixels = true };
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

        private static Brush Frozen(Brush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
