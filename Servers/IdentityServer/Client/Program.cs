// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using IdentityModel.Client; 
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdentityModel;
using DotNetEnv;


namespace Client
{
    public class Program
    {

        //private const string IdentityServerEndpoint = "https://identity.connectomes.utah.edu/identityserver/"; 
        //private const string IdentityServerEndpoint = "https://localhost:5001/";
        private const string IdentityServerEndpoint = "https://identity.codepharm.net:5001/";
        //private const string IdentityServerEndpoint = "https://localhost:44387/";

        //private const string IdentityServerApiEndpoint = "https://identity.connectomes.utah.edu/api/";
        //private const string IdentityServerApiEndpoint = "https://localhost:44387/";
        //private const string IdentityServerApiEndpoint = "https://localhost:6001/";
        private const string IdentityServerApiEndpoint = "https://identity.codepharm.net:6001/";

        private static string Secret = Environment.GetEnvironmentVariable("IDENTITY_SERVER_SECRET") ?? "CorrectHorseBatteryStaple"; // TODO: Remove fallback in production

        private const string Client = "api";

        private static async Task Pause()
        {
            Console.WriteLine("Press a key to continue");
            while (Console.KeyAvailable == false)
            {
                await Task.Delay(500);
            } 
        }
        public static async Task Main(string[] args)
        {
            var envFile = ".env";
            Env.TraversePath().Load(envFile);

            var buildEnvFile = $".env.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}";
            Env.TraversePath().Load(buildEnvFile);
            Secret = Environment.GetEnvironmentVariable("IDENTITY_SERVER_SECRET") ?? "CorrectHorseBatteryStaple"; // TODO: Remove fallback in production

            try
            {
                const string VolumeName = "RC1";
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

                Console.WriteLine($"Client: {Client}");
                Console.WriteLine($"Client Secret: {Secret}");

                var requested_scopes = new List<string> { "openid", "Viking.Annotation", $"{VolumeName}.Read", $"{VolumeName}.Annotate" };
                var tokenResponse = await client.RequestPasswordTokenAsync(new PasswordTokenRequest
                {
                    Address = disco.TokenEndpoint,
                    UserName = "jamesan",
                    Password = "JulyNinth2005!",
                    ClientId = Client,
                    ClientSecret = Secret,
                    Scope = string.Join(" ", requested_scopes), //Add desired permissions to scope
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
                try
                {
                    await GetUserPermissions(IdentityServerApiEndpoint, tokenResponse, VolumeName);
                } 
                catch (Exception e) 
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Could not retrieve user permissions for {VolumeName}:\n{e}");
                }

                Console.WriteLine("\n\n");

                await CheckClaims(disco, tokenResponse, Client, requested_scopes, "RC1.Read");
                await CheckClaims(disco, tokenResponse, Client, requested_scopes, "RC1.Annotate");
                await CheckClaims(disco, tokenResponse, Client, requested_scopes, "Viking.Annotation");
                await CheckClaims(disco, tokenResponse, Client, requested_scopes, "openid");

                await CheckClaims(disco, tokenResponse, Client, requested_scopes, "Bogus.Read");
                await CheckClaims(disco, tokenResponse, Client, requested_scopes, "RC1.Bogus");

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
            finally
            {
                await Pause();
              } 
        }

        private static async Task<bool> GetUserPermissions(string identityServerEndpoint, TokenResponse tokenResponse, string VolumeName)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Permissions on {VolumeName}");
            using (var client = new System.Net.Http.HttpClient())
            {
                client.SetBearerToken(tokenResponse.AccessToken);

                string userAddress = $"{identityServerEndpoint}permissions/CurrentUser";

                var appUserResponse = await client.GetStringAsync(userAddress);
                string appUser = JsonSerializer.Deserialize<string>(appUserResponse);
                 
                Console.WriteLine($"Server reports username = {appUser}");

                string userIdAddress = $"{identityServerEndpoint}permissions/CurrentUserId";

                var appUserIdResponse = await client.GetStringAsync(userIdAddress);
                string appUserId = JsonSerializer.Deserialize<string>(appUserIdResponse);

                Console.WriteLine($"Server reports userId = {appUserId}");

                //client.SetToken("token", tokenResponse.AccessToken);

                //client.SetBasicAuthentication("jamesan", "Wat>com3");

                {
                    string address = $"{identityServerEndpoint}permissions/{appUserId}/resource/{VolumeName}";

                    var permissionsResponse = await client.GetStringAsync(address);
                    var permissions = JsonSerializer.Deserialize<string[]>(permissionsResponse);
                    Console.WriteLine($"\t{VolumeName} permissions, explicit userId, {permissionsResponse}");
                }

                {
                    string address = $"{identityServerEndpoint}permissions/resource/{VolumeName}";

                    var permissionsResponse = await client.GetStringAsync(address);
                    var permissions = JsonSerializer.Deserialize<string[]>(permissionsResponse);
                    Console.WriteLine($"\t{VolumeName} permissions, token userId, {permissionsResponse}");
                }

            }

            Console.ResetColor();
            return true;
        }

        private static async Task<bool> CheckClaims(DiscoveryDocumentResponse disco, TokenResponse tokenResponse, string clientId,  ICollection<string> requested_scopes, string scope)
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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Could not connect to client {clientId} to validate scope claim {scope}:\n\t{validation.Error}");
                Console.ForegroundColor = ConsoleColor.White;
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

            bool ClaimMatchesExpectation = requested_scopes.Contains(scope) == FoundClaim;
            Console.ForegroundColor = ClaimMatchesExpectation ? ConsoleColor.Green : ConsoleColor.Red;
            if (FoundClaim)
            {
                Console.WriteLine($"Validated scope claim: {scope} - Matched expectation: {ClaimMatchesExpectation}");
            }
            else
            {
                Console.WriteLine($"Cound not validate scope claim: {scope} - Matched expectation: {ClaimMatchesExpectation}");
            }

            Console.ForegroundColor = ConsoleColor.White;

            return FoundClaim;
        }
    }
}