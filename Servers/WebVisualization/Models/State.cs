using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net; 
using AnnotationUtils.AnnotationService;


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

    public static class State
    {
        public static NetworkCredential userCredentials = new NetworkCredential("anonymous", "connectome");

        public static string selectedVolume = "Rabbit Retinal Connectome 1";

        public static string selectedServer = "connectomes.utah.edu";
            
        public static string filesPath;

        public static string globalPath;

        public static string VikingPlotFileName = "VikingPlot3D.xml";

        public static string Structure3DFileName = "Structure3D.xml";

        public static string className = "VikingPlot";

        public static Boolean networkFreshQuery = true;

        public static int networkID;

        public static bool redirected = false;

        public static string redirectedQuery = "";

        public static int loginAttempts = 0;

        public static IDictionary<string, ServerData> ServerToEndpointURLBase = new SortedDictionary<string, ServerData>();
 
        public static void ReadServices()
        {
            LoadServices.Initialize();
        }

        public static string SelectedEndpoint
        {
            get
            {
                ServerData serverData = ServerToEndpointURLBase[State.selectedServer];
                string EndpointURL = serverData.EndpointForVolume(State.selectedVolume); 
                return EndpointURL; 
            }
        }
        
     
        public static AnnotateStructuresClient CreateStructureClient()
        {
            AnnotateStructuresClient proxy = new AnnotateStructuresClient();
            proxy.Endpoint.Address = new System.ServiceModel.EndpointAddress(SelectedEndpoint);
            proxy.ClientCredentials.UserName.UserName = userCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = userCredentials.Password;
        //    if (proxy.State != System.ServiceModel.CommunicationState.Opened)
        //        proxy.Open();

            return proxy;
        }

        public static AnnotateStructureTypesClient CreateStructureTypeClient()
        {
            AnnotateStructureTypesClient proxy = new AnnotateStructureTypesClient();
            proxy.Endpoint.Address = new System.ServiceModel.EndpointAddress(State.SelectedEndpoint); 
            proxy.ClientCredentials.UserName.UserName = userCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = userCredentials.Password;
        //    if(proxy.State != System.ServiceModel.CommunicationState.Opened)
        //        proxy.Open();

            return proxy;
        }

        public static AnnotateLocationsClient CreateLocationsClient()
        {
            AnnotateLocationsClient proxy = new AnnotateLocationsClient();
            proxy.Endpoint.Address = new System.ServiceModel.EndpointAddress(State.SelectedEndpoint);
            proxy.ClientCredentials.UserName.UserName = userCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = userCredentials.Password;
       //     if (proxy.State != System.ServiceModel.CommunicationState.Opened)
       //         proxy.Open();

            return proxy;
        }

        public static CircuitClient CreateNetworkClient()
        {

            CircuitClient proxy = new CircuitClient();
            proxy.Endpoint.Address = new System.ServiceModel.EndpointAddress(State.SelectedEndpoint);
            proxy.ClientCredentials.UserName.UserName = userCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = userCredentials.Password;
       //     if (proxy.State != System.ServiceModel.CommunicationState.Opened)
       //         proxy.Open();

            return proxy;
        }

        public static bool GetEndpoint(System.Web.HttpRequestBase request, out string ServerName, out string VolumeName)
        {
            ServerName = null; 
            VolumeName = null; 

            bool found = true;

            foreach(string key in request.Params.AllKeys)
            {
                if(key.EndsWith("$volumeList"))
                    VolumeName = request[key];

                if(key.EndsWith("$serverList"))
                    ServerName = request[key]; 
            }

            if(ServerName == null || VolumeName == null)
                found = false; 

            return found;
        }
        
        
        public static string[] stringLong;
        public static String userFile;
        public static String userFileName;

        private static String _userURL;

        public static String userURL 
        {
            
            get{
                return State._userURL;
            }
         
            set{
                State._userURL = HttpUtility.UrlPathEncode(value);
            }
        }
       
        public static String virtualRoot;

        public static Dictionary<long, long> longLong = new Dictionary<long, long>();

        public static string graphType = "generate";

        public static System.Diagnostics.Process svgProcessReference;

        
        static State()
        {
            ServicePointManager.ServerCertificateValidationCallback += RemoteCertificateValidate;

        }

        /// <summary>
        /// Remotes the certificate validate.
        /// </summary>
        private static bool RemoteCertificateValidate(
           object sender, System.Security.Cryptography.X509Certificates.X509Certificate cert,
            System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors error)
        {
            // trust any certificate!!!
            System.Console.WriteLine("Warning, trust any certificate");
            return true;
        } 
    }
}
