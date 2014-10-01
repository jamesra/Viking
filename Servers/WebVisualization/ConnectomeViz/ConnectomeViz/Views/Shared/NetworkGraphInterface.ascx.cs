using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using AnnotationUtils.AnnotationService;

namespace ConnectomeViz.Views.Shared
{
    public partial class NetworkGraphInterface : System.Web.UI.UserControl
    {
        protected void Page_Load(object sender, EventArgs e)
        {

            string[] structures; 
            using ( CircuitClient client = ConnectomeViz.Models.State.CreateNetworkClient())
            {

                structures = client.getTopConnectedStructures(1); // 1 for structures and 0 for locations

                client.Close();
            }

            if (structures == null)
                return;

            SortedList<string, string> sortedStructures = new SortedList<string, string>();
           
            foreach (string s in structures)
            {
                string[] parts = s.Split('~');

                int idNum =  System.Convert.ToInt32(parts[1]);
                string idType = parts[0];
                int count = System.Convert.ToInt32(parts[2]);

                string itemText = idType + " " + idNum.ToString() + "  > Connections = " + count.ToString();

                sortedStructures.Add(count.ToString("D6") + " " + idNum.ToString("D7"), itemText); 
            }

            foreach (KeyValuePair<string, string> structItem in sortedStructures)
            {
                ListItem item = new ListItem(structItem.Value, structItem.Key.ToString());
                this.structureList.Items.Insert(0, item); 
            } 
        }
    }
}