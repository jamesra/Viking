using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using ConnectomeViz.Models;


namespace ConnectomeViz.Views.Shared
{

    public partial class EndpointSelectorControl : System.Web.Mvc.ViewUserControl
    {
        public string serverListClientID = "<%=serverList.ClientID%>";
        public string volumeListClientID = "<%=volumeList.ClientID%>";

        public string selectedServer
        {
            get
            {
                return serverList.Text;
            }
        }

        public string selectedVolume
        {
            get
            {
                return volumeList.Text;
            }
        }

        

        protected void Page_Load(object sender, EventArgs e)
        {
            foreach (ConnectomeViz.Models.ServerData serverData in ConnectomeViz.Models.State.ServerToEndpointURLBase.Values)
            {
                serverList.Items.Insert(serverList.Items.Count, new ListItem(serverData.Name, serverData.Name));

                if (serverData.Name == ConnectomeViz.Models.State.selectedServer)
                {
                    serverList.SelectedIndex = serverList.Items.Count - 1;
                }
            }

            //ConnectomeViz.Models.State.selectedServer = serverList.SelectedValue.ToString();

            ConnectomeViz.Models.ServerData server = ConnectomeViz.Models.State.SelectedServerData;

            foreach (string vName in server.Volumes)
            {
                volumeList.Items.Insert(volumeList.Items.Count, new ListItem(vName, vName));

                if (vName == ConnectomeViz.Models.State.selectedVolume)
                {
                    volumeList.SelectedIndex = volumeList.Items.Count - 1;
                }
            }

            //ConnectomeViz.Models.State.selectedServer = volumeList.SelectedValue.ToString(); 
        }
        
    }
}