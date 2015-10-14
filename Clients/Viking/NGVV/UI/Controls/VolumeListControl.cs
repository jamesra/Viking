using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Viking.UI.Forms;

namespace Viking.UI.Controls
{
    public partial class VolumeListControl : UserControl
    {
        public string VolumeUrl
        {
            get
            {
                if (listVolumes.SelectedItems.Count == 0)
                    return null; 

                return listVolumes.SelectedItems[0].Text;
            }
        }

        public string ServerUrl
        {
            get
            {
                if (listServers.SelectedItem == null)
                    return null;

                return listServers.SelectedItem.ToString();
            }
        }

        public string[] ServerUrls
        {
            get
            {
                List<string> servers = new List<string>();
                foreach (object item in listServers.Items)
                {
                    servers.Add(item.ToString());
                }
                return servers.ToArray();
            }
        }

        public VolumeListControl()
        {            
            InitializeComponent(); 
        }

        public void SetServers(string[] serverUrls)
        {
            foreach (string server in serverUrls)
            {
                this.listServers.Items.Add(server);
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            InputBox input = new InputBox("Enter URL for volume server", "http://", VolumeListControl.IsServerValid);
            if (input.ShowDialog() != DialogResult.OK)
                return;

            string Server = input.Value;

            listServers.Items.Add(Server);
        }

        private string EnsureServerFormattingCorrect(string server)
        {
            Uri serverURI = new Uri(server);
            server = serverURI.ToString();
            if (server[server.Length - 1] != '/')
                server += '/';

            return server;
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if(this.listServers.SelectedItem != null)
            {
                this.listServers.Items.Remove(this.listServers.SelectedItem);
            }
        }

        internal static bool IsServerValid(string text)
        {
            try
            {
                Uri ServerURI = new Uri(text);
            }
            catch (UriFormatException)
            {
                return false;
            }

            return true;
        }
          
        private void listServers_SelectedIndexChanged(object sender, EventArgs e)
        {
            listVolumes.Clear();

            if (listServers.SelectedItem == null)
            {
                return;
            }

            string OCPServerURL = listServers.SelectedItem.ToString();

            string[] volumes = Viking.Common.OCPVolumes.ReadServer(new Uri(OCPServerURL + "public_tokens/"));            

            foreach(string v in volumes)
            {
                listVolumes.Items.Add( new ListViewItem(v));
            }

            if(volumes.Length > 0)
            {
                listVolumes.SelectedIndices.Clear();
                listVolumes.SelectedIndices.Add(0);
            }
            
            listVolumes.Refresh();
        }
    }
}
