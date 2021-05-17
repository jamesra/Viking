// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using IdentityModel.Client; 
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;


namespace Client
{
    public class Program
    {
        public static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
            Pause();
        }

        private const string IdentityServerEndpoint = "https://identity.connectomes.utah.edu/";
        //private const string IdentityServerEndpoint = "https://localhost:44322/";

        private const string Secret = "CorrectHorseBatteryStaple";

        private const string Client = "ro.viking";

        private static void Pause()
        {
            Console.WriteLine("Press a key to continue");
            Console.ReadKey();
        }

        private static async Task MainAsync()
        {
            //string IdentityServerEndpoint = "https://identity.connectomes.utah.edu"; 

            DiscoveryCache _disco_cache = new DiscoveryCache(IdentityServerEndpoint);

            // discover endpoints from metadata 
            var disco = await _disco_cache.GetAsync();
            if (disco.IsError)
            {
                Console.WriteLine(disco.Error);
                return;
            }
             
            // request token
            //var tokenClient = new TokenClient(disco.TokenEndpoint, Client , Secret);

            HttpClient client = new HttpClient();
            
            var tokenResponse = await client.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = disco.TokenEndpoint,
                UserName = "jamesan",
                Password = "Wat>com3",
                ClientId = Client,
                ClientSecret = Secret,
                Scope = "openid Viking.Annotation",
            });

            //var tokenResponse = await tokenClient.RequestClientCredentialsAsync("api1");
            //var tokenResponse = await tokenClient.RequestResourceOwnerPasswordAsync("jander42@hotmail.com", "Wat>com3", "Viking.Annotation openid");
            //var tokenResponse = await tokenClient.RequestResourceOwnerPasswordAsync("jamesan", "Wat>com3", "openid Viking.Annotation RC1.Read");

            if (tokenResponse.IsError)
            {
                Console.WriteLine(tokenResponse.Error); 
                return;
            }

            Console.WriteLine(tokenResponse.Json);

            Console.WriteLine(tokenResponse.IdentityToken);
            Console.WriteLine("\n\n");

            var userInfo = await client.GetUserInfoAsync(new UserInfoRequest()
            {
                Address = disco.UserInfoEndpoint, 
                Token = tokenResponse.AccessToken
            });

//            var userInfoClient = new UserInfoClient(disco.UserInfoEndpoint);
            //var userInfo = await userInfoClient.GetAsync(tokenResponse.AccessToken);

            if (userInfo.IsError)
            {
                Console.WriteLine($"Error: {userInfo.Error}");
                return;
            }
            
            Console.WriteLine("Claims");
            Console.ForegroundColor = ConsoleColor.Yellow;
            foreach (var claim in userInfo.Claims)
            {
                Console.WriteLine(claim.ToString()); 
            }
            Console.ForegroundColor = ConsoleColor.White;
             

            Console.WriteLine("\n\n");
            await GetUserPermissions( tokenResponse, "RC1");
            Console.WriteLine("\n\n");

            await CheckClaims(disco, tokenResponse, Client, "RC1.Read");
            await CheckClaims(disco, tokenResponse, Client, "RC1.Annotate");
            await CheckClaims(disco, tokenResponse, Client, "Viking.Annotation");
            await CheckClaims(disco, tokenResponse, Client, "openid");

            await CheckClaims(disco, tokenResponse, Client, "Bogus.Read");
            await CheckClaims(disco, tokenResponse, Client, "RC1.Bogus");
               
            /*
            // call api
            var client = new HttpClient();
            client.SetBearerToken(tokenResponse.AccessToken);

            //var response = await client.GetAsync("http://localhost:5001/identity");
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(response.StatusCode);
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine(JArray.Parse(content));
            }
            */
        }

        private static async Task<bool> GetUserPermissions(TokenResponse tokenResponse, string VolumeName)
        {

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Permissions on {VolumeName}");
            using (var client = new System.Net.Http.HttpClient())
            {
                client.SetBearerToken(tokenResponse.AccessToken);

                //client.SetToken("token", tokenResponse.AccessToken);

                //client.SetBasicAuthentication("jamesan", "Wat>com3");
  
                string address = $"{IdentityServerEndpoint}Resources/UserPermissions?id={VolumeName}";

                 var response = await client.GetStringAsync(address); 

                 Console.WriteLine('\t' + response);
            }

            Console.ResetColor();
            return true;
        }

        private static async Task<bool> CheckClaims(DiscoveryDocumentResponse disco, TokenResponse tokenResponse, string clientId, string scope)
        {
            //The way I'm using scope and client is a bit odd, after a lot of troubleshooting I am basing it off of this post:
            //https://stackoverflow.com/questions/42126909/how-to-correctly-use-the-introspection-endpoint-with-identity-server-4

            var client = new HttpClient();

            var validation = await client.IntrospectTokenAsync(new TokenIntrospectionRequest()
            {
                Address = disco.IntrospectionEndpoint,
                ClientId = scope,
                ClientSecret = Secret,
                Token = tokenResponse.AccessToken,
            });
            //var validationClient = new IntrospectionClient(disco.IntrospectionEndpoint, clientId: scope, clientSecret: Secret);
            
            //var validation = await validationClient.SendAsync(new IntrospectionRequest() { Token = tokenResponse.AccessToken });

            if (validation.IsError)
            {
                Console.WriteLine($"Could not connect to client {clientId} to validate scope claim {scope}:\n\t{validation.Error}");
                return false;
            }

            bool FoundClaim = false;

            Console.ForegroundColor = ConsoleColor.Yellow;
            foreach (var c in validation.Claims)
            {
                Console.WriteLine($"\t{c}");
                if (c.Type == "scope")
                    FoundClaim = FoundClaim | c.Value.Split().Contains(scope);

            }
            Console.ForegroundColor = ConsoleColor.DarkGray;

            //Console.WriteLine(validation.Json);

            Console.ForegroundColor = FoundClaim ? ConsoleColor.Green : ConsoleColor.Red;
            if (FoundClaim)
            {
                Console.WriteLine($"Validated scope claim: {scope}");
            }
            else
            {
                Console.WriteLine($"Cound not validate scope claim: {scope}");
            }

            Console.ForegroundColor = ConsoleColor.White;

            return FoundClaim;
        }
    }
}