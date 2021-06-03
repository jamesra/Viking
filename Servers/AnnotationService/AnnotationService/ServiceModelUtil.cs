using System;
using System.Security.Principal;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Annotation
{
    static class ServiceModelUtil
    {
        /// <summary>
        /// Returns username if present or IP of client if anonymous
        /// </summary>
        /// <returns></returns>
        public static string GetUserForCall()
        {
            if(ServiceSecurityContext.Current == null)
                return GetIPForCall(); 

            if(ServiceSecurityContext.Current.IsAnonymous)
                return GetIPForCall(); 

            IIdentity identity = ServiceSecurityContext.Current.PrimaryIdentity;
            if (identity == null)
                return GetIPForCall(); 

            string Username = identity.Name; 

            if(Username == null)
                return GetIPForCall();

            if (Username.Length == 0 || Username.ToLower() == "anonymous")
                return GetIPForCall();

            return Username;
        }

        /// <summary>
        /// Returns IP address of client or NULL if an error occurs
        /// </summary>
        /// <returns></returns>
        public static string GetIPForCall()
        {
            try
            {
                OperationContext context = OperationContext.Current;
                MessageProperties messageProperties = context.IncomingMessageProperties;
                if (false == messageProperties.ContainsKey(RemoteEndpointMessageProperty.Name))
                    return null;

                RemoteEndpointMessageProperty endpointProperty =
                    messageProperties[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;

                return endpointProperty.Address;
            }
            catch (NullReferenceException)
            {
                return "localhost";
            }
        }
    }
}
