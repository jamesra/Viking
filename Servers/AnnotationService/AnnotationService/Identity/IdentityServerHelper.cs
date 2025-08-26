using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Duende.IdentityModel.Client;

namespace Annotation.Identity
{
    public static class IdentityServerHelper
    { 
        public const string Secret = "CorrectHorseBatteryStaple";

        private static DiscoveryCache _disco = null;

        public static async Task<DiscoveryDocumentResponse> GetDiscoveryDocumentAsync()
        {
            if (_disco is null)
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
            bool foundClaim = false;
            foreach (var c in validation.Claims)
            { 
                if (c.Type == "scope")
                    foundClaim |= c.Value.Split().Contains(scope);
            }

            return foundClaim;
        }
    }
}