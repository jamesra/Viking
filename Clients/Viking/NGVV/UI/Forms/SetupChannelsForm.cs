using System.Windows.Forms;
using Viking.VolumeModel;

namespace Viking.UI.Forms
{
    internal partial class SetupChannelsForm : Form
    {
        public ChannelInfo[] ChannelInfo
        {
            get
            {
                if (ChannelControl != null)
                    return ChannelControl.Channels;

                return new ChannelInfo[0];
            }
        }
        internal SetupChannelsForm(ChannelInfo[] Channels, string[] ChannelNames)
        {
            InitializeComponent();

            ChannelControl.SetChannelData(Channels, ChannelNames);
        }
    }
}
