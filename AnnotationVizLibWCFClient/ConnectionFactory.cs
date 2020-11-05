using AnnotationVizLib.WCFClient.AnnotationClient;
using System.Diagnostics;

namespace AnnotationVizLib.WCFClient
{
    public static class ConnectionFactory
    {
        public static string Endpoint
        {
            get
            {
                System.Diagnostics.Debug.Assert(_Endpoint != null, "AnnotationGraphLib.ConnectionFactory used before SetConnection(endpoint, userCredentials) was called");

                return _Endpoint;
            }
        }
        private static string _Endpoint = null;

        public static System.Net.NetworkCredential userCredentials
        {
            get
            {
                System.Diagnostics.Debug.Assert(_userCredentials != null, "AnnotationGraphLib.ConnectionFactory used before SetConnection(endpoint, userCredentials) was called");

                return _userCredentials;
            }
        }
        private static System.Net.NetworkCredential _userCredentials = null;

        public static void SetConnection(string endpoint, System.Net.NetworkCredential userCredentials)
        {
            ConnectionFactory._Endpoint = endpoint;
            ConnectionFactory._userCredentials = userCredentials;
        }

        public static AnnotateStructureTypesClient CreateStructureTypesClient()
        {
            Debug.Assert(_Endpoint != null, "SetConnection(endpoint, userCredentials) has not been called");
            AnnotateStructureTypesClient proxy = new AnnotateStructureTypesClient();
            proxy.Endpoint.Address = new System.ServiceModel.EndpointAddress(ConnectionFactory.Endpoint);
            proxy.ClientCredentials.UserName.UserName = ConnectionFactory.userCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = ConnectionFactory.userCredentials.Password;
            return proxy;
        }

        public static AnnotateStructuresClient CreateStructuresClient()
        {
            Debug.Assert(_Endpoint != null, "SetConnection(endpoint, userCredentials) has not been called");
            AnnotateStructuresClient proxy = new AnnotateStructuresClient();
            proxy.Endpoint.Address = new System.ServiceModel.EndpointAddress(ConnectionFactory.Endpoint);
            proxy.ClientCredentials.UserName.UserName = ConnectionFactory.userCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = ConnectionFactory.userCredentials.Password;
            return proxy;
        }

        public static AnnotateLocationsClient CreateLocationsClient()
        {
            Debug.Assert(_Endpoint != null, "SetConnection(endpoint, userCredentials) has not been called");
            AnnotateLocationsClient proxy = new AnnotateLocationsClient();
            proxy.Endpoint.Address = new System.ServiceModel.EndpointAddress(ConnectionFactory.Endpoint);
            proxy.ClientCredentials.UserName.UserName = ConnectionFactory.userCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = ConnectionFactory.userCredentials.Password;
            return proxy;
        }

        public static VolumeMetaClient CreateVolumeMetaClient()
        {
            Debug.Assert(_Endpoint != null, "SetConnection(endpoint, userCredentials) has not been called");
            VolumeMetaClient proxy = new VolumeMetaClient();
            proxy.Endpoint.Address = new System.ServiceModel.EndpointAddress(ConnectionFactory.Endpoint);
            proxy.ClientCredentials.UserName.UserName = ConnectionFactory.userCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = ConnectionFactory.userCredentials.Password;
            return proxy;
        }

        static ConnectionFactory()
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback += RemoteCertificateValidate;
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
