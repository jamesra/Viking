using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IdentityModel.Policy;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Duende.IdentityModel.Client;

namespace Annotation.Identity
{
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
                if (userNameClaim is null)
                {
                    message.Properties["Principal"] = CreateAnonymousUser();
                    return authPolicy;
                }

                //Todo: Use dependency injection to get a connection to the Identity database and pull the name of the volume endpoint from the URL?
                string[] roles;
                string[] AllowedOrgs = VikingWebAppSettings.AppSettings.GetAllowedOrganizations();
                if (AllowedOrgs.Length == 0)
                {
                    //If the organizations are not specified then use the default role assigned to the user
                    roles = validation.Claims.Where(c => c.Type == "role").Select(r => r.Value).ToArray();
                }
                else if (IsUserInAllowedOrganization(AllowedOrgs, validation.Claims))
                {
                    //Users have the normal permissions if they are in an allowed organization
                    roles = validation.Claims.Where(c => c.Type == "role").Select(r => r.Value).ToArray();
                }
                else
                {
                    //Users not in an allowed organization can only read
                    roles = new string[] { nameof(Roles.Read) };
                }

                GenericIdentity genericIdentity = new GenericIdentity(userNameClaim);
                GenericPrincipal principal = new GenericPrincipal(genericIdentity, roles);
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
            GenericPrincipal principal = new GenericPrincipal(genericIdentity, new string[] { nameof(Roles.Read) });
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
}