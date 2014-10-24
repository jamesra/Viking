using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;

namespace WebAnnotationModel
{
    public class State
    {
        static public System.Net.NetworkCredential UserCredentials = new System.Net.NetworkCredential("anonymous", "connectome");

        static public bool UseAsynchEvents = true;

        static public EndpointAddress EndpointAddress
        {
            get; set;
        }
    }
}
