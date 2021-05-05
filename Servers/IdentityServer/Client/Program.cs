// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using IdentityModel.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;


namespace Client
{
    public class Program
    {
        public static void Main(string[] args) => MainAsync().GetAwaiter().GetResult();

        private const string Secret = "CorrectHorseBatteryStaple";

        private const string Client = "ro.viking";

        private static async Task MainAsync()
        {
            //string IdentityServerEndpoint = "https://identity.connectomes.utah.edu";
            string IdentityServerEndpoint = "https://localhost:44322";

            // discover endpoints from metadata
            //var disco = await DiscoveryClient.GetAsync("http://localhost:5000");
            var disco = await DiscoveryClient.GetAsync(IdentityServerEndpoint);
            if (disco.IsError)
            {
                Console.WriteLine(disco.Error);
                return;
            }
            
            // request token
            var tokenClient = new TokenClient(disco.TokenEndpoint, Client , Secret);
            
            //var tokenResponse = await tokenClient.RequestClientCredentialsAsync("api1");
            //var tokenResponse = await tokenClient.RequestResourceOwnerPasswordAsync("jander42@hotmail.com", "Wat>com3", "Viking.Annotation openid");
            var tokenResponse = await tokenClient.RequestResourceOwnerPasswordAsync("jamesan", "Wat>com3", "openid Viking.Annotation RC1.Read");

            if (tokenResponse.IsError)
            {
                Console.WriteLine(tokenResponse.Error);
                return;
            }

            Console.WriteLine(tokenResponse.Json);
            Console.WriteLine("\n\n");

            var userInfoClient = new UserInfoClient(disco.UserInfoEndpoint);
            var userInfo = await userInfoClient.GetAsync(tokenResponse.AccessToken);

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

            await CheckClaims(disco, tokenResponse, Client, "RC1.Read");
            await CheckClaims(disco, tokenResponse, Client, "RC1.Annotate");
            await CheckClaims(disco, tokenResponse, Client, "Viking.Annotation");
            await CheckClaims(disco, tokenResponse, Client, "openid");

            await CheckClaims(disco, tokenResponse, Client, "Bogus.Read");
            await CheckClaims(disco, tokenResponse, Client, "RC1.Bogus");

            Console.WriteLine("Press a key to continue");

            var key = Console.ReadKey();
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

        private static async Task<bool> CheckClaims(DiscoveryResponse disco, TokenResponse tokenResponse, string client, string scope)
        {
            //The way I'm using scope and client is a bit odd, after a lot of troubleshooting I am basing it off of this post:
            //https://stackoverflow.com/questions/42126909/how-to-correctly-use-the-introspection-endpoint-with-identity-server-4
            
            var validationClient = new IntrospectionClient(disco.IntrospectionEndpoint, clientId: scope, clientSecret: Secret);
            
            var validation = await validationClient.SendAsync(new IntrospectionRequest() { Token = tokenResponse.AccessToken });

            if (validation.IsError)
            {
                Console.WriteLine($"Could not connect to client {client} to validate scope claim {scope}:\n\t{validation.Error}");
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