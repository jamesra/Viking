using System.Linq;
using System.Security.Principal;
using System.ServiceModel;
using System.Threading;

namespace Annotation.Identity
{
    public class RoleAuthorizationManager : ServiceAuthorizationManager
    {
        private readonly JwtMessageInspector _jwtInspector;
        
        public RoleAuthorizationManager()
        {
            _jwtInspector = new JwtMessageInspector();
        }
        
        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            System.Diagnostics.Debug.WriteLine($"===== RoleAuthorizationManager.CheckAccessCore() CALLED =====");
            System.Diagnostics.Debug.WriteLine($"IncomingMessageProperties contains 'Principal': {operationContext.IncomingMessageProperties.ContainsKey("Principal")}");
            
            //Assign roles to the Principal property for runtime to match with PrincipalPermissionAttributes decorated on the service operation.
            IPrincipal principal;
            
            if (!operationContext.IncomingMessageProperties.ContainsKey("Principal"))
            {
                System.Diagnostics.Debug.WriteLine($"RoleAuthorizationManager: No Principal in IncomingMessageProperties yet.");
                System.Diagnostics.Debug.WriteLine($"RoleAuthorizationManager: Attempting to extract JWT from message...");
                
                // Extract and validate JWT token BEFORE message inspector runs
                // This ensures the principal with roles is available for [PrincipalPermission] checks
                var message = operationContext.RequestContext.RequestMessage;
                _jwtInspector.AfterReceiveRequest(ref message, null, null);
                
                // Now check if the message inspector set the principal
                if (operationContext.IncomingMessageProperties.ContainsKey("Principal"))
                {
                    principal = operationContext.IncomingMessageProperties["Principal"] as IPrincipal;
                    System.Diagnostics.Debug.WriteLine($"RoleAuthorizationManager: JWT extraction successful, principal retrieved.");
                }
                else
                {
                    // Fallback: Create a temporary principal if JWT extraction failed
                    System.Diagnostics.Debug.WriteLine($"RoleAuthorizationManager: JWT extraction failed, creating temporary principal.");
                    principal = new GenericPrincipal(
                        operationContext.ServiceSecurityContext.PrimaryIdentity, 
                        new string[] { }); // Empty roles for now
                    
                    // Set in authorization context (required by WCF)
                    operationContext.ServiceSecurityContext.AuthorizationContext.Properties["Principal"] = principal;
                    return true;
                }
            }
            else
            {
                principal = operationContext.IncomingMessageProperties["Principal"] as IPrincipal;
            }
            
            // Set the principal in multiple places to ensure [PrincipalPermission] works
            if (principal != null)
            {
                // 1. Set in ServiceSecurityContext for WCF authorization
                operationContext.ServiceSecurityContext.AuthorizationContext.Properties["Principal"] = principal;
                
                // 2. Set Thread.CurrentPrincipal for [PrincipalPermission] attribute to work
                Thread.CurrentPrincipal = principal;
                
#if DEBUG
                // 3. Detailed logging for debugging
                var identity = principal.Identity;
                System.Diagnostics.Debug.WriteLine($"===== RoleAuthorizationManager =====");
                System.Diagnostics.Debug.WriteLine($"Principal set for user: '{identity?.Name}'");
                System.Diagnostics.Debug.WriteLine($"IsAuthenticated: {identity?.IsAuthenticated}");
                System.Diagnostics.Debug.WriteLine($"AuthenticationType: {identity?.AuthenticationType}");
                
                if (principal is GenericPrincipal genericPrincipal)
                {
                    // Test common roles
                    var testRoles = new[] { "Read", "Write", "Annotate", "Review", "Delete" };
                    System.Diagnostics.Debug.WriteLine($"Role checks:");
                    foreach (var role in testRoles)
                    {
                        var hasRole = genericPrincipal.IsInRole(role);
                        System.Diagnostics.Debug.WriteLine($"  IsInRole('{role}'): {hasRole}");
                    }
                }
                System.Diagnostics.Debug.WriteLine($"====================================");
#endif
            }
            
            return true;
        }
    }
}