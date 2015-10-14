using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Viking.Properties;

namespace Viking.UI.Forms
{
    public partial class FindVolumeForm : Form
    {
        public string VolumeURL
        {
            get; set;
        }

        public string ServerURL
        {
            get; set;
        }


        public FindVolumeForm()
        {
            InitializeComponent();

            List<string> servers = new List<string>(Settings.Default.ServerURLs.Count);
            foreach (string server in Settings.Default.ServerURLs)
            {
                servers.Add(server);
            }

            volumeList.SetServers(servers.ToArray());
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
