using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace AnnotationUtils
{
    public static class ConnectionFactory
    {
        public static string Endpoint 
        {
            get {
                System.Diagnostics.Debug.Assert(_Endpoint != null, "AnnotationGraphLib.ConnectionFactory used before SetConnection(endpoint, userCredentials) was called");
                 
                return _Endpoint;
            }
        }
        private static string _Endpoint = null;

        public static System.Net.NetworkCredential  userCredentials
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

        public static AnnotationService.AnnotateStructureTypesClient CreateStructureTypesClient()
        {
            Debug.Assert(_Endpoint != null, "SetConnection(endpoint, userCredentials) has not been called");
            AnnotationService.AnnotateStructureTypesClient proxy = new AnnotationService.AnnotateStructureTypesClient();
            proxy.Endpoint.Address = new System.ServiceModel.EndpointAddress(ConnectionFactory.Endpoint);
            proxy.ClientCredentials.UserName.UserName = ConnectionFactory.userCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = ConnectionFactory.userCredentials.Password;
            return proxy;
        }

        public static AnnotationService.AnnotateStructuresClient CreateStructuresClient()
        {
            Debug.Assert(_Endpoint != null, "SetConnection(endpoint, userCredentials) has not been called");
            AnnotationService.AnnotateStructuresClient proxy = new AnnotationService.AnnotateStructuresClient();
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
