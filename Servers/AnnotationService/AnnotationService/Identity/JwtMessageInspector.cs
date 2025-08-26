using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Configuration;
using System.Web;
// Use aliases to resolve ambiguous references
using MicrosoftSecurityToken = Microsoft.IdentityModel.Tokens.SecurityToken;
using MicrosoftSecurityTokenExpiredException = Microsoft.IdentityModel.Tokens.SecurityTokenExpiredException;
using MicrosoftSecurityTokenInvalidAudienceException = Microsoft.IdentityModel.Tokens.SecurityTokenInvalidAudienceException;
using MicrosoftSecurityTokenInvalidIssuerException = Microsoft.IdentityModel.Tokens.SecurityTokenInvalidIssuerException;
using MicrosoftSecurityTokenSignatureKeyNotFoundException = Microsoft.IdentityModel.Tokens.SecurityTokenSignatureKeyNotFoundException;

namespace Annotation.Identity
{
    public class JwtMessageInspector : IDispatchMessageInspector
    {
        private readonly string _authority;
        private readonly string _audience;
        private readonly TokenValidationParameters _tokenValidationParameters;

        public JwtMessageInspector()
        {
            _authority = ConfigurationManager.AppSettings["IdentityServer:Authority"];
            _audience = ConfigurationManager.AppSettings["IdentityServer:Audience"];
            
            _tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = bool.Parse(ConfigurationManager.AppSettings["JWT:TokenValidationParameters:ValidateIssuerSigningKey"] ?? "true"),
                ValidateIssuer = bool.Parse(ConfigurationManager.AppSettings["JWT:TokenValidationParameters:ValidateIssuer"] ?? "true"),
                ValidateAudience = bool.Parse(ConfigurationManager.AppSettings["JWT:TokenValidationParameters:ValidateAudience"] ?? "true"),
                ValidateLifetime = bool.Parse(ConfigurationManager.AppSettings["JWT:TokenValidationParameters:ValidateLifetime"] ?? "true"),
                ClockSkew = TimeSpan.Parse(ConfigurationManager.AppSettings["JWT:TokenValidationParameters:ClockSkew"] ?? "00:05:00"),
                ValidIssuer = _authority,
                ValidAudience = _audience,
                // You'll need to configure the signing key based on your IdentityServer setup
                // IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ConfigurationManager.AppSettings["IdentityServer:IssuerSigningKey"]))
            };
        }

        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
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
                        // Set the principal in the operation context
                        if (OperationContext.Current != null)
                        {
                            OperationContext.Current.IncomingMessageProperties["Principal"] = principal;
                        }
                        
                        // Also set it in the HttpContext if available
                        if (HttpContext.Current != null)
                        {
                            HttpContext.Current.User = principal;
                        }
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

        private ClaimsPrincipal ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                
                // Validate the token
                var principal = tokenHandler.ValidateToken(token, _tokenValidationParameters, out MicrosoftSecurityToken validatedToken);
                
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
            catch (MicrosoftSecurityTokenSignatureKeyNotFoundException)
            {
                System.Diagnostics.Debug.WriteLine("JWT token signature key not found");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"JWT token validation error: {ex.Message}");
            }

            return null;
        }
    }
}