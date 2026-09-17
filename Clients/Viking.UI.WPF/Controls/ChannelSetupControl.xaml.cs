using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Viking.VolumeModel;

namespace Viking.UI.WPF.Controls
{
    public partial class ChannelSetupControl : UserControl
    {
        private readonly List<ChannelPickerControl> _pickerControls = [];
        private IReadOnlyList<string> _channelNames = Array.Empty<string>();

        public ChannelSetupControl()
        {
            InitializeComponent();
            GreyscaleRadio.IsChecked = true;
        }

        public ChannelInfo[] Channels
        {
            get
            {
                if (GreyscaleRadio.IsChecked == true)
                {
                    return [];
                }

                foreach (ChannelPickerControl control in _pickerControls)
                {
                    control.CommitChanges();
                }

                return [.. _pickerControls.Select(p => p.Info)];
            }
        }

        public void SetChannelData(ChannelInfo[] channels, string[] channelNames)
        {
            _channelNames = channelNames ?? [];

            ClearPickers();

            if (channels is null || channels.Length == 0)
            {
                GreyscaleRadio.IsChecked = true;
                ChannelPanel.IsEnabled = false;
                AddChannelButton.IsEnabled = false;
                return;
            }

            foreach (ChannelInfo channel in channels)
            {
                AddPicker(channel);
            }

            GreyscaleRadio.IsChecked = false;
            ColorRadio.IsChecked = true;
            ChannelPanel.IsEnabled = true;
            AddChannelButton.IsEnabled = true;
            UpdatePickerChrome();
        }

        private void ClearPickers()
        {
            ChannelPanel.Children.Clear();
            _pickerControls.Clear();
        }

        private void AddPicker(ChannelInfo channelInfo = null)
        {
            ChannelPickerControl picker = new()
            {
                ChannelNames = _channelNames
            };
            picker.LoadChannel(channelInfo ?? new ChannelInfo());
            picker.DeleteClicked += PickerOnDeleteClicked;

            ChannelPanel.Children.Insert(0, picker);
            _pickerControls.Insert(0, picker);

            UpdatePickerChrome();
        }

        private void PickerOnDeleteClicked(object sender, EventArgs e)
        {
            if (sender is ChannelPickerControl picker)
            {
                picker.DeleteClicked -= PickerOnDeleteClicked;
                ChannelPanel.Children.Remove(picker);
                _pickerControls.Remove(picker);
                UpdatePickerChrome();
            }
        }

        private void UpdatePickerChrome()
        {
            for (int i = 0; i < _pickerControls.Count; i++)
            {
                ChannelPickerControl picker = _pickerControls[i];
                picker.ShowDelete = _pickerControls.Count > 1;
                picker.LabelsVisibility = i == _pickerControls.Count - 1 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void GreyscaleRadio_OnChecked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            if (GreyscaleRadio.IsChecked == true)
            {
                ChannelPanel.IsEnabled = false;
                AddChannelButton.IsEnabled = false;
            }
        }

        private void ColorRadio_OnChecked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            if (ColorRadio.IsChecked == true)
            {
                ChannelPanel.IsEnabled = true;
                AddChannelButton.IsEnabled = true;

                if (_pickerControls.Count == 0)
                {
                    AddPicker();
                }
            }
        }

        private void AddChannelButton_OnClick(object sender, RoutedEventArgs e) => AddPicker(new ChannelInfo());
    }
}

