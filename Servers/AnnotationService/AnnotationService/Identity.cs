using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IdentityModel.Selectors;
using System.IdentityModel.Policy;
using System.Security.Principal;
using System.ServiceModel.Channels;
using IdentityModel;
using IdentityModel.Client;
using System.ServiceModel;
using System.Threading;

namespace Annotation.Identity
{
    
    public class IdentityValidator : UserNamePasswordValidator
    {
        public override void Validate(string userName, string password)
        {
            

        }
    }

    
    public class AuthenticationManager : ServiceAuthenticationManager
    {
        
        private static DiscoveryResponse _disco = null;
        public static DiscoveryResponse disco
        {
            get {
                if(_disco == null)
                {
                    string IdentityServerEndpoint = VikingWebAppSettings.AppSettings.GetIdentityServerURLString();
                    _disco = DiscoveryClient.GetAsync(IdentityServerEndpoint).Result;
                    if (_disco.IsError)
                    {
                        return null;
                    }
                }

                return _disco; 
            } 
        }
        
        public override ReadOnlyCollection<IAuthorizationPolicy> Authenticate(ReadOnlyCollection<IAuthorizationPolicy> authPolicy, Uri listenUri, ref Message message)
        {
            string IdentityServerEndpoint = VikingWebAppSettings.AppSettings.GetIdentityServerURLString();
            int iBearer = message.Headers.FindHeader("Bearer", IdentityServerEndpoint);

             
            if (iBearer >= 0 && iBearer <= 5)
            {
                var AccessToken = message.Headers.GetHeader<string>(iBearer);

                //string IdentityServerEndpoint = "https://webdev.connectomes.utah.edu/identityserver/";
                //var Disco = DiscoveryClient.GetAsync(IdentityServerEndpoint).Result;

                var validationClient = new IntrospectionClient(disco.IntrospectionEndpoint, "Viking.Annotation", "secret");
                var validation = validationClient.SendAsync(new IntrospectionRequest() { Token = AccessToken, ClientId = "Viking.Annotation", ClientSecret = "secret" }).Result;

                if (validation.IsError)
                {
                    Console.WriteLine(validation.Error);
                    return new ReadOnlyCollection<IAuthorizationPolicy>(new List<IAuthorizationPolicy>());
                }

                string[] Roles = validation.Claims.Where(c => c.Type == "role").Select(r => r.Value).ToArray();

                var userNameClaim = validation.Claims.FirstOrDefault(c => c.Type == "name").Value;

                GenericIdentity genericIdentity = new GenericIdentity(userNameClaim);
                GenericPrincipal principal = new GenericPrincipal(genericIdentity, Roles);
                message.Properties["Principal"] = principal;
           //     Thread.CurrentPrincipal = principal;
            }

            return authPolicy;
        }
         
    }


    public class RoleAuthorizationManager : ServiceAuthorizationManager
    {
        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            //Assign roles to the Principal property for runtime to match with PrincipalPermissionAttributes decorated on the service operation.
            if (!operationContext.IncomingMessageProperties.ContainsKey("Principal"))
            {
                operationContext.ServiceSecurityContext.AuthorizationContext.Properties["Principal"] = new GenericPrincipal(operationContext.ServiceSecurityContext.PrimaryIdentity, new string[]{"Admin","Read","Write"});
                return true;
            }
            else
            {
                operationContext.ServiceSecurityContext.AuthorizationContext.Properties["Principal"] = operationContext.IncomingMessageProperties["Principal"];
            }
            

            

            return true; 
        }

    }
    
}
