// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using IdentityModel.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Client
{
    public class Program
    {
        public static void Main(string[] args) => MainAsync().GetAwaiter().GetResult();

        private const string Secret = "CorrectHorseBatteryStaple";

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
            var tokenClient = new TokenClient(disco.TokenEndpoint, "ro.viking", Secret);
            //var tokenResponse = await tokenClient.RequestClientCredentialsAsync("api1");
            //var tokenResponse = await tokenClient.RequestResourceOwnerPasswordAsync("jander42@hotmail.com", "Wat>com3", "Viking.Annotation openid");
            var tokenResponse = await tokenClient.RequestResourceOwnerPasswordAsync("jamesan", "Wat>com3", "openid Viking.Annotation RC1.Annotate RC1.Read");

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

            await CheckClaims(disco, tokenResponse, "RC1.Annotate");
            await CheckClaims(disco, tokenResponse, "RC1.Read");
            await CheckClaims(disco, tokenResponse, "Viking.Annotation");
            await CheckClaims(disco, tokenResponse, "openid");

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

        private static async Task<bool> CheckClaims(DiscoveryResponse disco, TokenResponse tokenResponse, string client)
        {
            var validationClient = new IntrospectionClient(disco.IntrospectionEndpoint, client, Secret);
            var validation = await validationClient.SendAsync(new IntrospectionRequest() { Token = tokenResponse.AccessToken, ClientId = client, ClientSecret = Secret });

            if (validation.IsError)
            {
                Console.WriteLine($"{client}: {validation.Error}");
                return false;
            }
             
            Console.WriteLine($"Validated Claim: {client}");

            Console.ForegroundColor = ConsoleColor.Yellow;
            foreach (var claim in validation.Claims)
            {
                Console.WriteLine(claim.ToString());
            }
            Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine(validation.Json);

            return true;
        }
    }
}