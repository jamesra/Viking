using Duende.IdentityModel.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace Viking.Tokens
{
    /// <summary>
    /// API model for volume tree nodes returned by UserAccessibleVolumeTree endpoint
    /// Nodes are organizational units, Volumes are the leaves of the tree
    /// </summary>
    public class ApiVolumeTreeNode
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public long? ParentId { get; set; }
        /// <summary>
        /// The resource's type, e.g., "Volume", "SegmentationService", etc.
        /// </summary>
        public string ResourceType { get; set; }
        public List<UserResourcePermissions> Volumes { get; set; } = new List<UserResourcePermissions>();
        public List<ApiVolumeTreeNode> Children { get; set; } = new List<ApiVolumeTreeNode>();
    }

    public class UserResourcePermissions
    {
        /// <summary>
        /// The resource ID
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// The resource's name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The resource's type, e.g., "Volume", "SegmentationService", etc.
        /// </summary>
        public string ResourceType { get; set; }

        /// <summary>
        /// The permissions the user has on this resource
        /// </summary>
        public IEnumerable<string> Permissions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Optional parent resource ID (for hierarchical objects)
        /// </summary>
        public long? ParentId { get; set; }

        /// <summary>
        /// Additional resource metadata - set as needed (optional)
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}

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

        /// <summary>
        /// Uri of service that provides tokens
        /// </summary>
        public Uri IdentityServerURL { get; set; }

        /// <summary>
        /// Uri of Api service that informs about what authority a token holder can request.
        /// </summary>
        public Uri IdentityApiURL { get; set;}

        private DiscoveryCache _disco = null;

        /// <summary>
        /// JSON serializer options configured to match ASP.NET Core's default camelCase naming policy
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

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


        /// <summary>
        /// Determine if an access token includes the provided scope
        /// </summary>
        /// <param name="AccessToken"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<bool> CheckClaims(string AccessToken, string scope)
        {
            var disco_response = await GetDiscoveryDocumentAsync();
            if (disco_response.IsError)
            {
                return false;
            }
             
            var disco = disco_response;

            if (disco.IntrospectionEndpoint is null)
                throw new ArgumentException($"No discovery endpoint found at {IdentityServerURL}");

            //using (var client = new System.Net.Http.HttpClient())
            var client = Viking.Common.SharedResources.HttpClient;
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
        /// Requests a token with the provided scopes
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
            var client = Viking.Common.SharedResources.HttpClient;
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

        /// <summary>
        /// Returns the username of the user who created the accessToken
        /// </summary>
        /// <param name="accessToken"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<string> GetUserId(string accessToken)
        {
            var disco = await GetDiscoveryDocumentAsync();
            var client = Viking.Common.SharedResources.HttpClient;
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

        /// <summary>
        /// Determine which scopes/rights are available to the provided user_token
        /// </summary>
        /// <param name="user_token"></param>
        /// <param name="VolumeName"></param>
        /// <returns></returns>
        public async Task<string[]> RetrieveUserVolumePermissions(TokenResponse user_token, string VolumeName)
        {
            using (var client = new System.Net.Http.HttpClient())
            {
                client.SetBearerToken(user_token.AccessToken); 
                var address_uri = new Uri(IdentityApiURL, $"Permissions/resource/{VolumeName}");
                string address = address_uri.ToString();

                var response = await client.GetStringAsync(address); 
                var permissions = JsonSerializer.Deserialize<string[]>(response, JsonOptions);
                System.Diagnostics.Trace.WriteLine(permissions);
                return permissions;
            }
        }

        /// <summary>
        /// Retrieves all volumes accessible to the authenticated user.
        /// </summary>
        /// <param name="user_token">The user's bearer token</param>
        /// <returns>Dictionary mapping volume IDs to volume metadata objects</returns>
        public async Task<System.Collections.Generic.Dictionary<long, object>> RetrieveUserAccessibleVolumes(TokenResponse user_token)
        {
            using (var client = new System.Net.Http.HttpClient())
            {
                client.SetBearerToken(user_token.AccessToken);
                var address_uri = new Uri(IdentityApiURL, "Permissions/AccessibleVolumes");
                string address = address_uri.ToString();

                // Debug logging
                Trace.WriteLine($"[IdentityServerHelper] IdentityApiURL base: {IdentityApiURL}");
                Trace.WriteLine($"[IdentityServerHelper] Calling UserAccessibleVolumes at: {address}");
                Trace.WriteLine($"[IdentityServerHelper] URI Scheme: {address_uri.Scheme}, Host: {address_uri.Host}, Port: {address_uri.Port}, Path: {address_uri.PathAndQuery}");

                var response = await client.GetStringAsync(address);
                var volumes = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<long, object>>(response, JsonOptions);
                Trace.WriteLine($"[IdentityServerHelper] Retrieved {volumes?.Count ?? 0} accessible volumes");
                return volumes;
            }
        }

        /// <summary>
        /// Retrieves all segmentation services accessible to the authenticated user.
        /// </summary>
        /// <param name="user_token">The user's bearer token</param>
        /// <returns>Dictionary mapping segmentation service IDs to metadata objects</returns>
        public async Task<System.Collections.Generic.Dictionary<long, object>> RetrieveUserAccessibleSegmentationServices(TokenResponse user_token)
        {
            using (var client = new System.Net.Http.HttpClient())
            {
                client.SetBearerToken(user_token.AccessToken);
                var address_uri = new Uri(IdentityApiURL, "Permissions/AccessibleSegmentationServices");
                string address = address_uri.ToString();

                Trace.WriteLine($"[IdentityServerHelper] IdentityApiURL base: {IdentityApiURL}");
                Trace.WriteLine($"[IdentityServerHelper] Calling UserAccessibleSegmentationServices at: {address}");
                Trace.WriteLine($"[IdentityServerHelper] URI Scheme: {address_uri.Scheme}, Host: {address_uri.Host}, Port: {address_uri.Port}, Path: {address_uri.PathAndQuery}");

                var response = await client.GetStringAsync(address);
                var services = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<long, object>>(response, JsonOptions);
                Trace.WriteLine($"[IdentityServerHelper] Retrieved {services?.Count ?? 0} accessible segmentation services");
                return services;
            }
        }

        /// <summary>
        /// Retrieves the hierarchical volume tree accessible to the authenticated user.
        /// </summary>
        /// <param name="user_token">The user's bearer token</param>
        /// <returns>List of root VolumeTreeNode objects representing the organizational tree structure</returns>
        public async Task<List<ApiVolumeTreeNode>> RetrieveUserAccessibleVolumeTree(TokenResponse user_token)
        {
            using (var client = new System.Net.Http.HttpClient())
            {
                client.SetBearerToken(user_token.AccessToken);
                var address_uri = new Uri(IdentityApiURL, "Permissions/UserAccessibleVolumeTree");
                string address = address_uri.ToString();

                // Debug logging
                Trace.WriteLine($"[IdentityServerHelper] IdentityApiURL base: {IdentityApiURL}");
                Trace.WriteLine($"[IdentityServerHelper] Calling UserAccessibleVolumeTree at: {address}");
                Trace.WriteLine($"[IdentityServerHelper] URI Scheme: {address_uri.Scheme}, Host: {address_uri.Host}, Port: {address_uri.Port}, Path: {address_uri.PathAndQuery}");

                var response = await client.GetStringAsync(address);
                var treeNodes = JsonSerializer.Deserialize<List<ApiVolumeTreeNode>>(response, JsonOptions);
                Trace.WriteLine($"[IdentityServerHelper] Retrieved {treeNodes?.Count ?? 0} root tree nodes");
                return treeNodes ?? new List<ApiVolumeTreeNode>();
            }
        }
    } 
}
