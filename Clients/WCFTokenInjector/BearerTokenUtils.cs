﻿using IdentityModel.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;


namespace Viking.Tokens
{
    public static class UriHelper
    {
        public static string UriCombine(this string val, string append)
        {
            if (String.IsNullOrEmpty(val)) return append;
            if (String.IsNullOrEmpty(append)) return val;
            return $"{val.TrimEnd('/')}/{append.TrimStart('/')}";
        }
    }

    public class IdentityServerHelper
    {
        public string ClientId { get; set; } = "ro.viking";
        public string ClientSecret { get; set; } = "CorrectHorseBatteryStaple";

        public Uri IdentityServerURL { get; set; }

        private DiscoveryCache _disco = null;

        /// <summary>
        /// Returns null if there is an error obtaining the Discovery document
        /// </summary>
        public DiscoveryDocumentResponse DiscoveryDocument => GetDiscoveryDocumentAsync().Result as DiscoveryDocumentResponse;

        public IdentityServerHelper()
        {

        }

        
        public async Task<DiscoveryDocumentResponse> GetDiscoveryDocumentAsync()
        {
            if (_disco is null)
            {
                _disco = new DiscoveryCache(IdentityServerURL.ToString());
            }

            return await _disco.GetAsync();
        }


        public async Task<bool> CheckClaims(string AccessToken, string scope)
        {
            var disco_response = await GetDiscoveryDocumentAsync();
            if (disco_response.IsError)
            {
                return false;
            }
             
            var disco = disco_response;

            if (disco.IntrospectionEndpoint == null)
                throw new ArgumentException($"No discovery endpoint found at {IdentityServerURL}");

            using (var client = new System.Net.Http.HttpClient())
            {
                var validation = await client.IntrospectTokenAsync(new TokenIntrospectionRequest()
                {
                    Address = disco.IntrospectionEndpoint,
                    ClientId = scope,
                    ClientSecret = ClientSecret,
                    Token = AccessToken,
                });

                if (validation.IsError)
                {
#if DEBUG
                    Trace.WriteLine($"{scope}: {validation.Error}");
#endif
                    return false;
                }
                 
                bool FoundClaim = false;
                foreach (var c in validation.Claims)
                {
                    if (c.Type == "scope")
                        FoundClaim |= c.Value.Split().Contains(scope);
                }

                return FoundClaim;
            }
        }

        /// <summary>
        /// If the result is not an error the result must be case to TokenResponse.
        /// </summary>
        /// <param name="AuthenticationServiceURL"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="ClientId"></param>
        /// <param name="ClientSecret"></param>
        /// <param name="scopes"></param>
        /// <returns></returns>
        public async Task<ProtocolResponse> RetrieveBearerToken(string username, string password, string[] scopes = null)
        {
            if (scopes is null)
                scopes = new string[] { "openid profile Viking.Annotation" };

            string scopes_string = "";
            foreach (string s in scopes)
            {
                scopes_string += s + " ";
            }

            // discover endpoints from metadata 
            var disco_response = await GetDiscoveryDocumentAsync();
            if (disco_response.IsError)
            { 
                return disco_response;
            }

            var disco = disco_response as DiscoveryDocumentResponse;

            //The url must match and is case-sensitive
            //var discoTask = DiscoveryClient.GetAsync("http://localhost:5000");
            using (var client = new System.Net.Http.HttpClient())
            {
                // request token
                PasswordTokenRequest request = new PasswordTokenRequest()
                {
                    Address = disco.TokenEndpoint,
                    ClientId = ClientId,
                    ClientSecret = ClientSecret,
                    Scope = scopes_string,
                    UserName = username,
                    Password = password
                };

                var tokenResponse = await client.RequestPasswordTokenAsync(request);
                return tokenResponse;
            }
        }

        public async Task<string> GetUserId(string accessToken)
        {
            var disco = await GetDiscoveryDocumentAsync();
            using (var client = new System.Net.Http.HttpClient())
            {
                var userInfo = await client.GetUserInfoAsync(new UserInfoRequest()
                {
                    Address = disco.UserInfoEndpoint,
                    Token = accessToken
                });

                var userIdClaim = userInfo.Claims.FirstOrDefault(c => c.Type.Equals("sub"));
                return userIdClaim is null ? throw new ArgumentException($"No sub claim found for access token {accessToken}") : userIdClaim.Value;
            }
        }

        public async Task<string[]> RetrieveUserVolumePermissions(TokenResponse user_token, string VolumeName)
        {
            using (var client = new System.Net.Http.HttpClient())
            {
                client.SetBearerToken(user_token.AccessToken); 
                var address_uri = $"{IdentityServerURL.Scheme}://" + IdentityServerURL.Host.UriCombine($"api/permissions/resource/{VolumeName}");
                string address = address_uri.ToString();

                var response = await client.GetStringAsync(address); 
                var permissions = JsonSerializer.Deserialize<string[]>(response);
                System.Diagnostics.Trace.WriteLine(permissions);
                return permissions;
            }
        }
    } 
}
