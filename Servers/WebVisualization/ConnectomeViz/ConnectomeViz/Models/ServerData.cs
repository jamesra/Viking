using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConnectomeViz.Models
{
    public class ServerData
    {
        public readonly string Name;
        public readonly string URL;

        private IDictionary<string, string> _VolumeToServiceURL = null;

        public string EndpointForVolume(string volumeName)
        {
            return URL + _VolumeToServiceURL[volumeName];
        }

        public ServerData(string name, string URL, IDictionary<string, string> volumeToService)
        {
            this.Name = name;
            this.URL = URL;
            this._VolumeToServiceURL = volumeToService;
        }

        public string[] Volumes
        {
            get
            {
                return _VolumeToServiceURL.Keys.ToArray();
            }
        }
    }
}