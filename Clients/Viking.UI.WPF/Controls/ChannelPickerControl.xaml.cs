using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Viking.VolumeModel;

namespace Viking.UI.WPF.Controls
{
    public partial class ChannelPickerControl : UserControl
    {
        private static readonly IReadOnlyList<string> SectionOptions = ["Selected", "Above", "Below", "Fixed..."];

        private static readonly IReadOnlyDictionary<string, Color> PresetColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "White", Colors.White },
            { "Red", Colors.Red },
            { "Green", Colors.Lime },
            { "Blue", Colors.DodgerBlue }
        };

        private const string DefaultChannelName = "Selected";

        private bool _isInitialized;

        public ChannelPickerControl()
        {
            InitializeComponent();
            SectionCombo.ItemsSource = SectionOptions;
            ColorCombo.ItemsSource = PresetColors.Keys.Concat(["Custom..."]).ToArray();
            LabelsVisibility = Visibility.Visible;
        }

        public event EventHandler DeleteClicked;

        public ChannelInfo Info { get; private set; } = new ChannelInfo();

        public IReadOnlyList<string> ChannelNames
        {
            get => _channelNames;
            set
            {
                _channelNames = value ?? Array.Empty<string>();
                RefreshChannels();
            }
        }

        private IReadOnlyList<string> _channelNames = Array.Empty<string>();

        public Visibility LabelsVisibility
        {
            get => (Visibility)GetValue(LabelsVisibilityProperty);
            set => SetValue(LabelsVisibilityProperty, value);
        }

        public static readonly DependencyProperty LabelsVisibilityProperty =
            DependencyProperty.Register(nameof(LabelsVisibility), typeof(Visibility), typeof(ChannelPickerControl),
                new PropertyMetadata(Visibility.Visible));

        public bool ShowDelete
        {
            get => DeleteButton.Visibility == Visibility.Visible;
            set => DeleteButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        public void LoadChannel(ChannelInfo info)
        {
            Info = info is null ? new ChannelInfo() : (ChannelInfo)info.Clone();
            _isInitialized = false;

            RefreshChannels();
            RefreshSection();
            RefreshColor();

            _isInitialized = true;
        }

        private void RefreshChannels()
        {
            string selected = Info?.ChannelName;

            ChannelCombo.ItemsSource = new[] { DefaultChannelName }
                .Concat(ChannelNames ?? Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            ChannelCombo.SelectedItem = string.IsNullOrEmpty(selected)
                ? DefaultChannelName
                : ChannelCombo.Items.Cast<string>()
                    .FirstOrDefault(c => c.Equals(selected, StringComparison.OrdinalIgnoreCase))
                    ?? DefaultChannelName;
        }

        private void RefreshSection()
        {
            switch (Info.SectionSource)
            {
                case ChannelInfo.SectionInfo.ABOVE:
                    SectionCombo.SelectedItem = "Above";
                    FixedSectionTextBox.Text = string.Empty;
                    FixedSectionTextBox.IsEnabled = false;
                    break;
                case ChannelInfo.SectionInfo.BELOW:
                    SectionCombo.SelectedItem = "Below";
                    FixedSectionTextBox.Text = string.Empty;
                    FixedSectionTextBox.IsEnabled = false;
                    break;
                case ChannelInfo.SectionInfo.FIXED:
                    SectionCombo.SelectedItem = "Fixed...";
                    FixedSectionTextBox.Text = Info.FixedSectionNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                    FixedSectionTextBox.IsEnabled = true;
                    break;
                default:
                    SectionCombo.SelectedItem = "Selected";
                    FixedSectionTextBox.Text = string.Empty;
                    FixedSectionTextBox.IsEnabled = false;
                    break;
            }
        }

        private void RefreshColor()
        {
            Color color = ConvertColor(Info.FormColor);
            ColorPreview.Background = new SolidColorBrush(color);

            string preset = PresetColors.FirstOrDefault(kvp => kvp.Value == color).Key;
            ColorCombo.SelectedItem = preset ?? "Custom...";
        }

        private static Color ConvertColor(System.Drawing.Color color) => Color.FromArgb(color.A, color.R, color.G, color.B);

        private static System.Drawing.Color ConvertColor(Color color) => System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);

        private void SectionCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized)
            {
                return;
            }

            switch (SectionCombo.SelectedItem as string)
            {
                case "Above":
                    Info.SectionSource = ChannelInfo.SectionInfo.ABOVE;
                    FixedSectionTextBox.IsEnabled = false;
                    break;
                case "Below":
                    Info.SectionSource = ChannelInfo.SectionInfo.BELOW;
                    FixedSectionTextBox.IsEnabled = false;
                    break;
                case "Fixed...":
                    Info.SectionSource = ChannelInfo.SectionInfo.FIXED;
                    FixedSectionTextBox.IsEnabled = true;
                    break;
                default:
                    Info.SectionSource = ChannelInfo.SectionInfo.SELECTED;
                    FixedSectionTextBox.IsEnabled = false;
                    break;
            }
        }

        private void ColorCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized)
            {
                return;
            }

            string selected = ColorCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            if (PresetColors.TryGetValue(selected, out Color preset))
            {
                ApplyColor(preset);
            }
            else if (string.Equals(selected, "Custom...", StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.Forms.ColorDialog dlg = new()
                {
                    Color = Info.FormColor
                };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ApplyColor(ConvertColor(dlg.Color));
                }
                else
                {
                    // revert to previous selection
                    _isInitialized = false;
                    RefreshColor();
                    _isInitialized = true;
                }
            }
        }

        private void ApplyColor(Color color)
        {
            ColorPreview.Background = new SolidColorBrush(color);
            Info.FormColor = ConvertColor(color);
        }

        private void DeleteButton_OnClick(object sender, RoutedEventArgs e) => DeleteClicked?.Invoke(this, EventArgs.Empty);

        public void CommitChanges()
        {
            string selectedChannel = ChannelCombo.SelectedItem as string;
            Info.ChannelName = string.Equals(selectedChannel, DefaultChannelName, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : selectedChannel ?? string.Empty;

            if (Info.SectionSource == ChannelInfo.SectionInfo.FIXED)
            {
                if (int.TryParse(FixedSectionTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    Info.FixedSectionNumber = value;
                }
            }
        }
    }
}

