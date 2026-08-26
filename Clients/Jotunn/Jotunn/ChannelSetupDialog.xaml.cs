using System.Windows;
using Viking.VolumeModel;

namespace Jotunn
{
    public partial class ChannelSetupDialog : Window
    {
        public ChannelSetupDialog()
        {
            InitializeComponent();
        }

        public ChannelInfo[] Channels => ChannelSetup.Channels;

        public void SetChannelData(ChannelInfo[] channels, string[] channelNames)
        {
            ChannelSetup.SetChannelData(channels, channelNames);
        }

        void OnOk(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
