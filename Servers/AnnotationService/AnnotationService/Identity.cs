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
using System.Net.Http;

namespace Annotation.Identity
{
    /*
    public class IdentityValidator : UserNamePasswordValidator
    {
        public override void Validate(string userName, string password)
        {
            

        }
    }
    */

    public class IdentityServerPrincipal : IPrincipal
    {
        public IIdentity Identity {get;}

        /// <summary>
        /// Token associated with identity
        /// </summary>
        public string Token { get; }

        private List<string> ValidatedClaims = new List<string>();

        public bool IsInRole(string role)
        {
            if (ValidatedClaims.Contains(role))
                return true; 

            string VolumeName = VikingWebAppSettings.AppSettings.GetApplicationSetting("VolumeName");
            string ClaimRequired = GetClaimRequired(VolumeName, role);

            var validated = IdentityServerHelper.CheckClaims(Token, ClaimRequired).Result;
            if(validated)
            {
                ValidatedClaims.Add(role);
            }

            return validated;
        }

        private string GetClaimRequired(string VolumeName, string permission)
        {
            return $"{VolumeName}.{permission}";
        }
    }

    public static class IdentityServerHelper
    { 
        public const string Secret = "CorrectHorseBatteryStaple";

        private static DiscoveryCache _disco = null;

        public static async Task<DiscoveryDocumentResponse> GetDiscoveryDocumentAsync()
        {
            if (_disco == null)
            {
                string IdentityServerEndpoint = VikingWebAppSettings.AppSettings.GetIdentityServerURLString();
                _disco = new DiscoveryCache(IdentityServerEndpoint);
            }

            var response = await _disco.GetAsync();
            if (response.IsError)
            {
                Trace.WriteLine($"Error retrieving discovery document: {response.Error}");
                return null;
            }

            return response;
        }
        

        public static async Task<bool> CheckClaims(string AccessToken, string scope)
        {
            DiscoveryDocumentResponse disco = await GetDiscoveryDocumentAsync();

            var client = new HttpClient();

            var validation = await client.IntrospectTokenAsync(new TokenIntrospectionRequest()
            {
                Address = disco.IntrospectionEndpoint,
                ClientId = scope,
                ClientSecret = Secret,
                Token = AccessToken, 
            }); 

            if (validation.IsError)
            {
#if DEBUG
                Trace.WriteLine($"{scope}: {validation.Error}");
#endif
                return false;
            }
#if DEBUG
            /*
            Console.WriteLine($"Validated Claim: {scope}");

            Console.ForegroundColor = ConsoleColor.Yellow;
            foreach (var claim in validation.Claims)
            {
                Console.WriteLine(claim.ToString());
            }
            Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine(validation.Json);
            */
#endif
            bool FoundClaim = false;
            foreach (var c in validation.Claims)
            { 
                if (c.Type == "scope")
                    FoundClaim = FoundClaim | c.Value.Split().Contains(scope);
            }

            return FoundClaim;
        }
    }
    
    public class AuthenticationManager : ServiceAuthenticationManager
    {
        public override ReadOnlyCollection<IAuthorizationPolicy> Authenticate(ReadOnlyCollection<IAuthorizationPolicy> authPolicy, Uri listenUri, ref Message message)
        {
            string IdentityServerEndpoint = VikingWebAppSettings.AppSettings.GetIdentityServerURLString();
            int iBearer = message.Headers.FindHeader("Bearer", IdentityServerEndpoint);

            string Secret = IdentityServerHelper.Secret;

            if (iBearer >= 0 && iBearer <= 5)
            {
                var AccessToken = message.Headers.GetHeader<string>(iBearer);

                //string IdentityServerEndpoint = "https://webdev.connectomes.utah.edu/identityserver/";
                //var Disco = DiscoveryClient.GetAsync(IdentityServerEndpoint).Result;
                var client = new HttpClient();
                var disco = IdentityServerHelper.GetDiscoveryDocumentAsync().Result;

                var validation = client.IntrospectTokenAsync(new TokenIntrospectionRequest()
                {
                    Address = disco.IntrospectionEndpoint,
                    ClientId = null, //TODO: Pull required volume and right from configuration.
                    ClientSecret = Secret,
                    Token = AccessToken,
                }).Result;

                //var validationClient = new IntrospectionClient(IdentityServerHelper.Discovery.IntrospectionEndpoint, "Viking.Annotation", Secret);
                //var validation = validationClient.SendAsync(new IntrospectionRequest() { Token = AccessToken, ClientId = "Viking.Annotation", ClientSecret = Secret }).Result;

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

                //Todo: Use dependency injection to get a connection to the Identity database and pull the name of the volume endpoint from the URL?
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
#if DEBUG
                string[] roles = new string[] { "Admin", "Read", "Write" }; 
#else
                string[] roles = new string[] { "Read" }; 
#endif
                operationContext.ServiceSecurityContext.AuthorizationContext.Properties["Principal"] = new GenericPrincipal(operationContext.ServiceSecurityContext.PrimaryIdentity, roles);
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
