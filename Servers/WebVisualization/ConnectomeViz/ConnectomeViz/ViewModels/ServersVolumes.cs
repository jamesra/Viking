using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ConnectomeViz.ViewModels
{
    public class ServersVolumes
    {
        public string SelectedServer;
        public string SelectedVolume;

        public SelectList Servers;
        public SelectList Volumes;
    }
}