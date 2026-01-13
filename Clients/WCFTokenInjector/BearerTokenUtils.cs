using Duende.IdentityModel.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
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

    /// <summary>
    /// Helper class for identity server operations (token management, discovery, claims validation)
    /// </summary>
    public class BearerTokenHelper
    {
        public string ClientId { get; set; } = "ro.viking";
        public string ClientSecret { get; set; } = "CorrectHorseBatteryStaple";

        /// <summary>
        /// Uri of service that provides tokens
        /// </summary>
        public Uri IdentityServerURL { get; set; }

        private DiscoveryCache _disco = null;

        // Cache HttpClient instances per IdentityServerURL endpoint
        private static readonly ConcurrentDictionary<string, HttpClient> _httpClientCache = new ConcurrentDictionary<string, HttpClient>();

        public BearerTokenHelper()
        {
        }

        /// <summary>
        /// Creates a BearerTokenHelper instance from application settings (for server-side usage)
        /// This method uses reflection to avoid direct dependency on VikingWebAppSettings
        /// </summary>
        public static BearerTokenHelper CreateFromAppSettings()
        {
            try
            {
                // Try to use VikingWebAppSettings if available (server-side)
                // Use reflection to avoid compile-time dependency
                var appSettingsType = Type.GetType("VikingWebAppSettings.AppSettings, VikingWebAppSettings");
                if (appSettingsType != null)
                {
                    var getMethod = appSettingsType.GetMethod("GetIdentityServerURLString");
                    if (getMethod != null)
                    {
                        var identityServerUrlString = getMethod.Invoke(null, null) as string;
                        if (!string.IsNullOrEmpty(identityServerUrlString) && Uri.TryCreate(identityServerUrlString, UriKind.Absolute, out Uri identityServerUrl))
                        {
                            return new BearerTokenHelper
                            {
                                IdentityServerURL = identityServerUrl,
                                ClientSecret = "CorrectHorseBatteryStaple"
                            };
                        }
                    }
                }
            }
            catch
            {
                // If VikingWebAppSettings is not available, return null
            }
            return null;
        }

        /// <summary>
        /// Gets or creates a cached HttpClient instance for the IdentityServerURL endpoint
        /// </summary>
        private HttpClient GetHttpClient()
        {
            if (IdentityServerURL == null)
                return Viking.Common.SharedResources.HttpClient;

            string key = IdentityServerURL.ToString();
            return _httpClientCache.GetOrAdd(key, _ => new HttpClient());
        }

        public async Task<DiscoveryDocumentResponse> GetDiscoveryDocumentAsync()
        {
            if (_disco is null)
            {
                if (IdentityServerURL == null)
                    throw new InvalidOperationException("IdentityServerURL must be set before calling GetDiscoveryDocumentAsync");
                _disco = new DiscoveryCache(IdentityServerURL.ToString());
            }

            var result = await _disco.GetAsync();
            if(result is null)
            {
                throw new Exception($"No discovery document returned from identity server: {IdentityServerURL.ToString()}");
            }
            else if(result.IsError)
            {
                throw new Exception($"result.Error from { IdentityServerURL.ToString() }");
            }

            return result;
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

            var client = GetHttpClient();
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

        /// <summary>
        /// Requests a token with the provided scopes
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="scopes"></param>
        /// <returns></returns>
        public async Task<ProtocolResponse> RetrieveBearerToken(string username, string password, string[] scopes = null)
        {
            scopes ??= new string[] { "openid profile Viking.Annotation" };

            string scopes_string = string.Join(" ", scopes);

            // discover endpoints from metadata 
            var disco_response = await GetDiscoveryDocumentAsync();
            if (disco_response.IsError)
            { 
                return disco_response;
            }

            var disco = disco_response as DiscoveryDocumentResponse;

            var client = GetHttpClient();
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

        /// <summary>
        /// Returns the username of the user who created the accessToken
        /// </summary>
        /// <param name="accessToken"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<string> GetUserId(string accessToken)
        {
            var disco = await GetDiscoveryDocumentAsync();
            var client = GetHttpClient();
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
    /// Helper class for Identity API operations (permissions, volumes, services)
    /// </summary>
    public class IdentityApiHelper
    {
        /// <summary>
        /// Uri of Api service that informs about what authority a token holder can request.
        /// </summary>
        public Uri IdentityApiURL { get; set; }

        /// <summary>
        /// JSON serializer options configured to match ASP.NET Core's default camelCase naming policy
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        // Cache HttpClient instances per IdentityApiURL endpoint
        private static readonly ConcurrentDictionary<string, HttpClient> _httpClientCache = new ConcurrentDictionary<string, HttpClient>();

        public IdentityApiHelper()
        {
        }

        /// <summary>
        /// Gets or creates a cached HttpClient instance for the IdentityApiURL endpoint
        /// </summary>
        private HttpClient GetHttpClient()
        {
            if (IdentityApiURL == null)
                return Viking.Common.SharedResources.HttpClient;

            string key = IdentityApiURL.ToString();
            return _httpClientCache.GetOrAdd(key, _ => new HttpClient());
        }

        /// <summary>
        /// Logs API request details for debugging
        /// </summary>
        private void LogApiRequest(string operationName, Uri address)
        {
            Trace.WriteLine($"[IdentityApiHelper] IdentityApiURL base: {IdentityApiURL}");
            Trace.WriteLine($"[IdentityApiHelper] Calling {operationName} at: {address}");
            Trace.WriteLine($"[IdentityApiHelper] URI Scheme: {address.Scheme}, Host: {address.Host}, Port: {address.Port}, Path: {address.PathAndQuery}");
        }

        /// <summary>
        /// Generic helper method to make authenticated GET requests to the Identity API
        /// </summary>
        private async Task<T> GetAuthenticatedJsonAsync<T>(TokenResponse token, string relativePath, string operationName = null)
        {
            var client = GetHttpClient();
            var address_uri = new Uri(IdentityApiURL, relativePath);
            
            if (!string.IsNullOrEmpty(operationName))
            {
                LogApiRequest(operationName, address_uri);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, address_uri);
            request.SetBearerToken(token.AccessToken);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<T>(json, JsonOptions);
            
            if (!string.IsNullOrEmpty(operationName))
            {
                Trace.WriteLine($"[IdentityApiHelper] Retrieved {GetResultCount(result)} items for {operationName}");
            }

            return result;
        }

        private int GetResultCount<T>(T result)
        {
            if (result == null) return 0;
            if (result is System.Collections.ICollection collection)
                return collection.Count;
            if (result is System.Collections.Generic.IDictionary<long, object> dict)
                return dict.Count;
            return 1;
        }

        /// <summary>
        /// Determine which scopes/rights are available to the provided user_token
        /// </summary>
        /// <param name="user_token"></param>
        /// <param name="VolumeName"></param>
        /// <returns></returns>
        public async Task<string[]> RetrieveUserVolumePermissions(TokenResponse user_token, string VolumeName)
        {
            var permissions = await GetAuthenticatedJsonAsync<string[]>(user_token, $"Permissions/resource/{VolumeName}");
            Trace.WriteLine($"[IdentityApiHelper] Retrieved permissions: {string.Join(", ", permissions ?? Array.Empty<string>())}");
            return permissions ?? Array.Empty<string>();
        }

        /// <summary>
        /// Retrieves all volumes accessible to the authenticated user.
        /// </summary>
        /// <param name="user_token">The user's bearer token</param>
        /// <returns>Dictionary mapping volume IDs to volume metadata objects</returns>
        public async Task<System.Collections.Generic.Dictionary<long, object>> RetrieveUserAccessibleVolumes(TokenResponse user_token)
        {
            return await GetAuthenticatedJsonAsync<System.Collections.Generic.Dictionary<long, object>>(user_token, "Permissions/AccessibleVolumes", "UserAccessibleVolumes");
        }

        /// <summary>
        /// Retrieves all segmentation services accessible to the authenticated user.
        /// </summary>
        /// <param name="user_token">The user's bearer token</param>
        /// <returns>Dictionary mapping segmentation service IDs to metadata objects</returns>
        public async Task<System.Collections.Generic.Dictionary<long, object>> RetrieveUserAccessibleSegmentationServices(TokenResponse user_token)
        {
            return await GetAuthenticatedJsonAsync<System.Collections.Generic.Dictionary<long, object>>(user_token, "Permissions/AccessibleSegmentationServices", "UserAccessibleSegmentationServices");
        }

        /// <summary>
        /// Retrieves the hierarchical volume tree accessible to the authenticated user.
        /// </summary>
        /// <param name="user_token">The user's bearer token</param>
        /// <returns>List of root VolumeTreeNode objects representing the organizational tree structure</returns>
        public async Task<List<ApiVolumeTreeNode>> RetrieveUserAccessibleVolumeTree(TokenResponse user_token)
        {
            var result = await GetAuthenticatedJsonAsync<List<ApiVolumeTreeNode>>(user_token, "Permissions/UserAccessibleVolumeTree", "UserAccessibleVolumeTree");
            return result ?? new List<ApiVolumeTreeNode>();
        }
    } 
}
