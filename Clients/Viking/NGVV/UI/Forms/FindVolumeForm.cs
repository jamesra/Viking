using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using Viking.Properties;

namespace Viking.UI.Forms
{
    public partial class FindVolumeForm : Form
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string VolumeURL
        {
            get; set;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ServerURL
        {
            get; set;
        }


        public FindVolumeForm()
        {
            InitializeComponent();

            List<string> servers = [.. Settings.Default.ServerURLs];

            volumeList.SetServers([.. servers]);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            this.ServerURL = volumeList.ServerUrl;
            this.VolumeURL = volumeList.VolumeUrl;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
