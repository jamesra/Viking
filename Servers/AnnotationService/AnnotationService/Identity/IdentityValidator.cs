using System;
using System.IdentityModel.Selectors;
using System.Net;
using System.ServiceModel;

namespace Annotation.Identity
{
    /// <summary>
    /// Custom username/password validator for WCF's UserNameOverTransport security.
    /// This validator provides lenient validation (just checks for non-empty credentials)
    /// because the actual security is enforced by JWT token validation in JwtMessageInspector.
    /// </summary>
    public class IdentityValidator : UserNamePasswordValidator
    {
        public IdentityValidator()
        {
            // Enable TLS 1.2 and TLS 1.3 for all HTTPS connections
            // This is needed for JWT validation when connecting to Identity Server
            // TLS 1.3 requires Windows 10 20H1+ or Windows Server 2022+, fallback to TLS 1.2 if not available
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            }
            catch
            {
                // Fallback to TLS 1.2 if TLS 1.3 is not supported on this system
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }
        }

        public override void Validate(string userName, string password)
        {
            // This validator is primarily here to satisfy WCF's UserNameOverTransport security requirement.
            // The actual security validation is performed by JwtMessageInspector which runs later in the
            // WCF pipeline where OperationContext.Current is available and can access the JWT token.
            //
            // We use lenient validation here - just ensure credentials are present.
            // If the JWT token validation fails in JwtMessageInspector, the request will be rejected there.
            
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
            {
                throw new FaultException("Username and password are required.");
            }

            // Accept any non-empty credentials
            // The real authentication happens via JWT token validation in JwtMessageInspector
            System.Diagnostics.Debug.WriteLine($"Lenient validation passed for user '{userName}' - JWT validation will provide actual security");
        }
    }
}


