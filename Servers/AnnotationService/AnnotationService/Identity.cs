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

                var IsActive = validation.Claims.FirstOrDefault(c => c.Type == "active");
                if (IsActive?.Value != "True")
                {
                    message.Properties["Principal"] = CreateAnonymousUser();
                    return authPolicy;
                }

                var userNameClaim = validation.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
                if (userNameClaim == null)
                {
                    message.Properties["Principal"] = CreateAnonymousUser();
                    return authPolicy;
                }

                string[] Roles;
                string[] AllowedOrgs = VikingWebAppSettings.AppSettings.GetAllowedOrganizations();
                if (AllowedOrgs.Length == 0)
                {
                    //If the organizations are not specified then use the default role assigned to the user
                    Roles = validation.Claims.Where(c => c.Type == "role").Select(r => r.Value).ToArray();
                }
                else if (IsUserInAllowedOrganization(AllowedOrgs, validation.Claims))
                {
                    //Users have the normal permissions if they are in an allowed organization
                    Roles = validation.Claims.Where(c => c.Type == "role").Select(r => r.Value).ToArray();
                }
                else
                {
                    //Users not in an allowed organization can only read
                    Roles = new string[] { "Reader" };
                }

                GenericIdentity genericIdentity = new GenericIdentity(userNameClaim);
                GenericPrincipal principal = new GenericPrincipal(genericIdentity, Roles);
                message.Properties["Principal"] = principal;
                //     Thread.CurrentPrincipal = principal;
            }
            else
            {
                message.Properties["Principal"] = CreateAnonymousUser();
            }

            return authPolicy;
        }

        private static GenericPrincipal CreateAnonymousUser()
        {
            GenericIdentity genericIdentity = new GenericIdentity("anonymous");
            GenericPrincipal principal = new GenericPrincipal(genericIdentity, new string[] { "Reader" });
            return principal;
        }

        private static bool IsUserInAllowedOrganization(string[] AllowedOrgs, IEnumerable<System.Security.Claims.Claim> claims)
        {
            List<string> organizationClaims = claims.Where(c => c.Type == "affiliation").Select(c => c.Value).ToList();
            foreach(string orgClaim in organizationClaims)
            {
                if (AllowedOrgs.Contains(orgClaim))
                    return true;
            }

            return false;
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
