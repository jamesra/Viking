using System;
using System.Windows.Forms;
using Viking.VolumeModel;
using WpfChannelSetup = Viking.UI.WPF.Controls.ChannelSetupControl;

namespace Viking.UI.Forms
{
    internal partial class SetupChannelsForm : Form
    {
        private readonly WpfChannelSetup _channelControl;

        public ChannelInfo[] ChannelInfo => _channelControl?.Channels ?? [];

        internal SetupChannelsForm(ChannelInfo[] Channels, string[] ChannelNames)
        {
            InitializeComponent();

            _channelControl = new WpfChannelSetup();
            this.channelHost.Child = _channelControl;

            _channelControl.SetChannelData(Channels, ChannelNames);
        }
    }
}
