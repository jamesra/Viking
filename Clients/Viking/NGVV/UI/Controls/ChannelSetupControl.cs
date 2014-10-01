using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Viking.Common;
using Viking.VolumeModel; 

namespace Viking.UI.Controls
{
    internal partial class ChannelSetupControl : System.Windows.Forms.UserControl
    {
        /// <summary>
        /// Channel info to display.
        /// </summary>
        public ChannelInfo[] Channels
        {
            get {
                if(radioGreyscale.Checked)
                {
                    return new ChannelInfo[0]; 
                }
                else
                {
                    //Fetch the channel info from our controls
                    List<ChannelInfo> listChannelInfo = new List<ChannelInfo>(ChannelPickerList.Count);
                    foreach (ChannelPickerControl channelControl in ChannelPickerList)
                    {
                        listChannelInfo.Add(channelControl.Info);
                    }

                    return listChannelInfo.ToArray(); 
                }
            }
        }

        private List<ChannelPickerControl> ChannelPickerList = new List<ChannelPickerControl>();

        private string[] _ChannelNames; 

        public ChannelSetupControl()
        {
            InitializeComponent();
        }

        public void SetChannelData(ChannelInfo[] ChannelsToAdd, string[] channelNames)
        {
            _ChannelNames = channelNames; 

            if (ChannelsToAdd == null)
            {
                radioGreyscale.Checked = true;
                groupChannels.Enabled = false; 
                return;
            }

            if (ChannelsToAdd.Length == 0)
            {
                radioGreyscale.Checked = true;
                groupChannels.Enabled = false; 
                return;
            }

            //Delete existing channels
            for (int i = panelChannels.Controls.Count; i > 0; i--)
            {
                panelChannels.Controls.RemoveAt(i - 1);
            }

            //Create a Channel Picker for each channel 
            for (int i = 0; i < ChannelsToAdd.Length; i++)
            {
                AddPickerControl(ChannelsToAdd[i]);
            }

            radioColor.Checked = true;
        }

        private ChannelPickerControl AddPickerControl(ChannelInfo info)
        {
            ChannelPickerControl Picker = new ChannelPickerControl(info);

            this.panelChannels.Controls.Add(Picker); 
            Picker.Channels = this._ChannelNames; 
            Picker.Dock = DockStyle.Top;
            Picker.TabIndex = ChannelPickerList.Count; 
            Picker.OnDeleteClicked += new EventHandler(this.OnChannelDelete);
            ChannelPickerList.Add(Picker);

            ToggleDeleteButton();

            return Picker;
        }

        private void OnChannelDelete(object sender, EventArgs e)
        {
            ChannelPickerControl Picker = sender as ChannelPickerControl;
            if (panelChannels.Controls.Contains(Picker))
            {
                panelChannels.Controls.Remove(Picker);
                ChannelPickerList.Remove(Picker);
            }

            ToggleDeleteButton();
        }

        /// <summary>
        /// Turns off the delete button on the Channel Picker Controls if there is only one left
        /// </summary>
        private void ToggleDeleteButton()
        {
            for (int i = 0; i < panelChannels.Controls.Count; i++)
            {
                ChannelPickerControl Picker = panelChannels.Controls[i] as ChannelPickerControl;
                if (Picker == null)
                    continue;

                Picker.ShowLabels = i == panelChannels.Controls.Count - 1; //Only show label for first control
                Picker.ShowDelete = panelChannels.Controls.Count > 1;
                
            }
        }

        private void checkGreyscale_CheckedChanged(object sender, EventArgs e)
        {
            groupChannels.Enabled = !radioGreyscale.Checked;
        }

        private void radioColor_CheckedChanged(object sender, EventArgs e)
        {
            groupChannels.Enabled = radioColor.Checked;

            //Create the first channel if none exist
            if (ChannelPickerList.Count == 0)
            {
                AddPickerControl(new ChannelInfo());
            }
        }

        private void buttonAddChannel_Click(object sender, EventArgs e)
        {
            AddPickerControl(new ChannelInfo()); 
        }

        private void radioGreyscale_CheckedChanged(object sender, EventArgs e)
        {
            
        }
    }
}
