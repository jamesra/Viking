using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Threading;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Configuration;
using System.Web;
// Use aliases to resolve ambiguous references
using MicrosoftSecurityToken = Microsoft.IdentityModel.Tokens.SecurityToken;
using MicrosoftSecurityTokenExpiredException = Microsoft.IdentityModel.Tokens.SecurityTokenExpiredException;
using MicrosoftSecurityTokenInvalidAudienceException = Microsoft.IdentityModel.Tokens.SecurityTokenInvalidAudienceException;
using MicrosoftSecurityTokenInvalidIssuerException = Microsoft.IdentityModel.Tokens.SecurityTokenInvalidIssuerException;
using MicrosoftSecurityTokenSignatureKeyNotFoundException = Microsoft.IdentityModel.Tokens.SecurityTokenSignatureKeyNotFoundException;
using log4net;

namespace Annotation.Identity
{
    public class JwtMessageInspector : IDispatchMessageInspector
    {
        private readonly string _authority;
        private readonly string _audience;
        private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
        private readonly string _volumeName;

        public JwtMessageInspector()
        {
            _authority = ConfigurationManager.AppSettings["IdentityServer:authority"] ?? "https://identity.codepharm.net:5001/";
            _audience = ConfigurationManager.AppSettings["IdentityServer:audience"] ?? "Viking.Annotation.API";
            
            // Cache volume name at initialization (read once, use many times)
            _volumeName = ConfigurationManager.AppSettings["VolumeName"] ?? ConfigurationManager.AppSettings["DatabaseName"] ?? "Unknown";
            
            // Set up automatic retrieval of signing keys from Identity Server's discovery endpoint
            _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{_authority.TrimEnd('/')}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());
        }

        public JwtMessageInspector(string Authority, 
                                   string Audience, 
                                   bool ValidateIssuerSigningKey = true,
                                   bool ValidateIssuer = true,
                                   bool ValidateLifetime = true)
        {
            _authority = Authority;
            _audience = Audience;

            // Cache volume name at initialization (read once, use many times)
            _volumeName = ConfigurationManager.AppSettings["VolumeName"] ?? ConfigurationManager.AppSettings["DatabaseName"] ?? "Unknown";

            // Set up automatic retrieval of signing keys from Identity Server's discovery endpoint
            _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{_authority.TrimEnd('/')}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());
        }

        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            System.Diagnostics.Debug.WriteLine($"===== JwtMessageInspector.AfterReceiveRequest() CALLED =====");
            try
            {
                // Extract JWT token from the request
                var token = ExtractTokenFromMessage(request);
                
                if (!string.IsNullOrEmpty(token))
                {
                    // Validate the JWT token
                    var principal = ValidateToken(token);
                    
                    if (principal != null)
                    {
                        // Convert ClaimsPrincipal to GenericPrincipal with roles for WCF compatibility
                        var wcfPrincipal = ConvertToGenericPrincipal(principal);
                        
                        // Set the principal in the operation context
                        if (OperationContext.Current != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"JwtMessageInspector: Setting Principal in IncomingMessageProperties");
                            OperationContext.Current.IncomingMessageProperties["Principal"] = wcfPrincipal;
                            System.Diagnostics.Debug.WriteLine($"JwtMessageInspector: Principal set. Key exists: {OperationContext.Current.IncomingMessageProperties.ContainsKey("Principal")}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"JwtMessageInspector: WARNING - OperationContext.Current is NULL!");
                        }
                        
                        // Also set it in the HttpContext if available
                        if (HttpContext.Current != null)
                        {
                            HttpContext.Current.User = wcfPrincipal;
                        }
                        
                        // Set Thread.CurrentPrincipal directly (backup in case RoleAuthorizationManager doesn't run)
                        Thread.CurrentPrincipal = wcfPrincipal;
                        System.Diagnostics.Debug.WriteLine($"JwtMessageInspector: Thread.CurrentPrincipal set to user '{wcfPrincipal.Identity?.Name}'");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"JwtMessageInspector: WARNING - Principal from ValidateToken is NULL!");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception but don't throw it to avoid breaking the service
                // You might want to add proper logging here
                System.Diagnostics.Debug.WriteLine($"JWT validation error: {ex.Message}");
            }

            return null;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            // Clean up if needed
        }

        private string ExtractTokenFromMessage(Message message)
        {
            try
            {
                // Method 1: Try to extract from HTTP headers
                if (message.Properties.ContainsKey("httpRequest"))
                {
                    var httpRequest = message.Properties["httpRequest"] as HttpRequestMessageProperty;
                    if (httpRequest != null)
                    {
                        var authHeader = httpRequest.Headers["Authorization"];
                        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                        {
                            return authHeader.Substring("Bearer ".Length);
                        }
                    }
                }

                // Method 2: Try to extract from custom headers
                if (message.Properties.ContainsKey("httpRequest"))
                {
                    var httpRequest = message.Properties["httpRequest"] as HttpRequestMessageProperty;
                    if (httpRequest != null)
                    {
                        var token = httpRequest.Headers["X-JWT-Token"];
                        if (!string.IsNullOrEmpty(token))
                        {
                            return token;
                        }
                    }
                }

                // Method 3: Try to extract from message headers
                var headerIndex = message.Headers.FindHeader("Authorization", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims");
                if (headerIndex >= 0)
                {
                    var authHeader = message.Headers.GetHeader<string>(headerIndex);
                    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                    {
                        return authHeader.Substring("Bearer ".Length);
                    }
                }

                // Method 4: Try to extract from custom message header
                var tokenHeaderIndex = message.Headers.FindHeader("JWTToken", "http://schemas.microsoft.com/ws/2005/05/identity/claims");
                if (tokenHeaderIndex >= 0)
                {
                    return message.Headers.GetHeader<string>(tokenHeaderIndex);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting token: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Roles for specific volumes are encoded as scope claims with the format: <VolumeName>.<RoleName> 
        /// </summary>
        /// <param name="claimsPrincipal"></param>
        /// <returns></returns>
        private System.Security.Principal.GenericPrincipal ConvertToGenericPrincipal(ClaimsPrincipal claimsPrincipal)
        {
            // Debug: Log ALL claims in the token
            System.Diagnostics.Debug.WriteLine($"===== JWT Token Claims =====");
            System.Diagnostics.Debug.WriteLine($"Volume Name: '{_volumeName}'");
            System.Diagnostics.Debug.WriteLine($"All claims in token:");
            foreach (var claim in claimsPrincipal.Claims)
            {
                System.Diagnostics.Debug.WriteLine($"  Type: '{claim.Type}' = Value: '{claim.Value}'");
            }
            System.Diagnostics.Debug.WriteLine($"===========================");
            
            // Extract role claims from the JWT token
            // Common role claim types used by Identity Server / OpenID Connect:
            // - "role" (standard claim)
            // - "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" (Windows claim)
            var roleClaims = claimsPrincipal.FindAll(c => 
                c.Type == "role" || 
                c.Type == ClaimTypes.Role ||
                c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");

            var scopeClaims = claimsPrincipal.FindAll(c => c.Type == "scope").Where(c => c.Value.StartsWith(this._volumeName)).ToList();
            
            // Debug: Log all scope claims
            System.Diagnostics.Debug.WriteLine($"Found {scopeClaims.Count} scope claims for volume '{_volumeName}':");
            foreach (var claim in scopeClaims)
            {
                System.Diagnostics.Debug.WriteLine($"  - {claim.Value}");
            }
            
            //Remove the name of the volume from the scope and save the name of the role
            int volumeNameLength = _volumeName.Length;
            var volumeRoleClaims = scopeClaims.Select(c => c.Value.Substring(volumeNameLength+1));
             
            var roles = roleClaims.Select(c => c.Value).ToList();
            roles.AddRange(volumeRoleClaims);
            
            // Map JWT role names to service role names
            // "admin" (volume-specific role) → "Review" (service role name)
            for (int i = 0; i < roles.Count; i++)
            {
                if (roles[i].Equals("admin", StringComparison.OrdinalIgnoreCase))
                {
                    var originalRole = roles[i];
                    roles[i] = "Review";
                    System.Diagnostics.Debug.WriteLine($"Mapped role '{originalRole}' -> 'Review'");
                }
            }
             
            // Debug: Log all extracted roles
            System.Diagnostics.Debug.WriteLine($"Total roles extracted: {roles.Count}");
            foreach (var role in roles)
            {
                System.Diagnostics.Debug.WriteLine($"  - Role: '{role}'");
            }
            
            // Preserve the original ClaimsIdentity from the JWT token
            // Create a new ClaimsIdentity with proper NameClaimType so Identity.Name works correctly
            var originalIdentity = claimsPrincipal.Identity as ClaimsIdentity;
            
            // Create new ClaimsIdentity with the name claim type specified
            // This ensures Identity.Name property gets populated from the "name" claim
            var identity = new ClaimsIdentity(
                originalIdentity.Claims,
                originalIdentity.AuthenticationType,
                nameType: "name",  // Tell it to use "name" claim for Identity.Name
                roleType: ClaimTypes.Role
            );
            
            // Add role claims for volume-specific roles (these aren't in the original claims)
            foreach (var role in roles)
            {
                // Only add if not already present to avoid duplicates
                if (!identity.HasClaim(ClaimTypes.Role, role) && !identity.HasClaim("role", role))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
            }
            
            var userName = identity.Name ?? "Unknown";
            System.Diagnostics.Debug.WriteLine($"Extracted username from identity: '{userName}'");
            
            // Create a ClaimsPrincipal (preserves all claims from the token)
            var claimsPrincipalWithRoles = new ClaimsPrincipal(identity);
            
            // For WCF compatibility, we also create a GenericPrincipal wrapper
            var genericPrincipal = new System.Security.Principal.GenericPrincipal(identity, roles.ToArray());
            
            System.Diagnostics.Debug.WriteLine($"JWT Principal created for user '{userName}' with roles: {string.Join(", ", roles)} [Volume: {_volumeName}]");
            
            return genericPrincipal;
        } 

        private ClaimsPrincipal ValidateToken(string token)
        {
            try
            {
                // Retrieve the OpenID Connect configuration (includes signing keys)
                var discoveryDocument = _configurationManager.GetConfigurationAsync(CancellationToken.None).Result;
                
                // Create token validation parameters with the signing keys from Identity Server
                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = discoveryDocument.SigningKeys,
                    ValidateIssuer = true,
                    ValidIssuer = discoveryDocument.Issuer, // Use issuer from discovery document (includes correct trailing slash)
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };
                
                var tokenHandler = new JwtSecurityTokenHandler();
                
                // Validate the token
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out MicrosoftSecurityToken validatedToken);
                
                if (validatedToken != null)
                {
                    // Additional validation if needed
                    var jwtToken = validatedToken as JwtSecurityToken;
                    if (jwtToken != null)
                    {
                        // You can add additional validation here
                        // For example, check specific claims, roles, etc.
                        
                        // Example: Check if token has required role
                        var roleClaim = principal.FindFirst("role") ?? principal.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
                        if (roleClaim == null)
                        {
                            // Log warning or handle as needed
                            System.Diagnostics.Debug.WriteLine("Token does not contain role claim");
                        }
                    }
                    
                    return principal;
                }
            }
            catch (MicrosoftSecurityTokenExpiredException)
            {
                System.Diagnostics.Debug.WriteLine("JWT token has expired");
            }
            catch (MicrosoftSecurityTokenInvalidAudienceException)
            {
                System.Diagnostics.Debug.WriteLine("JWT token has invalid audience");
            }
            catch (MicrosoftSecurityTokenInvalidIssuerException)
            {
                System.Diagnostics.Debug.WriteLine("JWT token has invalid issuer");
            }
            catch (MicrosoftSecurityTokenSignatureKeyNotFoundException ex)
            {
                System.Diagnostics.Debug.WriteLine($"JWT token signature key not found: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"JWT token validation error: {ex.Message}");
            }

            return null;
        }
    }
}