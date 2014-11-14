using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Hosting;
using System.Xml;
using System.Xml.Linq;

namespace ConnectomeViz.Models
{
    public class LoadServices
    {
        private static LoadServices instance = null;

        private LoadServices()
        { }

        public static void Initialize()
        {

            instance = new LoadServices();
            instance.Task();
        }

        public void Task()
        {
            string path = System.IO.Path.Combine(new string[] {HostingEnvironment.ApplicationPhysicalPath,"Services.xml"});
            
            XDocument xmlDoc = XDocument.Load(path);

            var Servers = from server in xmlDoc.Descendants("Server")
                          select server;

            string DefaultServer = null;
            string DefaultVolume = null;

            foreach (var server in Servers)
            {
                string serverName = server.Attribute("name").Value.ToString();
                string serverURL = server.Attribute("url").Value.ToString();

                XAttribute DefaultAttribute = server.Attribute("Selected");
                if (DefaultAttribute != null)
                {
                    if (DefaultAttribute.Value.ToLower() != "false")
                        DefaultServer = serverName;
                }

                var res = from vol in server.Descendants("Volume")
                          select new
                          {
                              Name = vol.Attribute("name").Value,
                              RelPath = vol.Attribute("relPath").Value.ToString(),
                              DefaultAttribute = vol.Attribute("Selected")
                          };

                SortedDictionary<string, string> VolumeToURL = new SortedDictionary<string, string>();

                foreach (var volume in res)
                {
                    if (volume.DefaultAttribute != null)
                    {
                        if (volume.DefaultAttribute.Value.ToLower() != "false")
                            DefaultVolume = volume.Name;
                    }

                    try
                    {
                        if (!VolumeToURL.ContainsKey(volume.Name))
                            VolumeToURL.Add(volume.Name, volume.RelPath);
                        else
                            VolumeToURL[volume.Name] = volume.RelPath;
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Trace.Write("Exception adding volume to server:\n" + e.Message);
                    }
                }

                ServerData serverData = new ServerData(serverName, serverURL, VolumeToURL);

                try
                {
                    if (!VolumeToURL.ContainsKey(serverName))
                        State.ServerToEndpointURLBase.Add(serverName, serverData);
                    else
                        State.ServerToEndpointURLBase[serverName] = serverData;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Trace.Write("Exception adding server " + serverName + ":\n" + e.Message);
                }
            }

            if (DefaultServer == null)
                DefaultServer = State.ServerToEndpointURLBase.Keys.First<string>();

            if (DefaultVolume == null)
                DefaultVolume = State.ServerToEndpointURLBase[State.selectedServer].Volumes[0];

            State.selectedServer = DefaultServer;
            State.selectedVolume = DefaultVolume;


        }
    }
}
