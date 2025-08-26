using System.Security.Principal;
using System.ServiceModel;

namespace Annotation.Identity
{
    public class RoleAuthorizationManager : ServiceAuthorizationManager
    {
        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            //Assign roles to the Principal property for runtime to match with PrincipalPermissionAttributes decorated on the service operation.
            if (!operationContext.IncomingMessageProperties.ContainsKey("Principal"))
            { 
#if DEBUG
                string[] roles = new string[] { "Admin", "Read", "Write" }; 
#else
                string[] roles = new string[] { "Read" }; 
#endif
                operationContext.ServiceSecurityContext.AuthorizationContext.Properties["Principal"] = new GenericPrincipal(operationContext.ServiceSecurityContext.PrimaryIdentity, roles);
                return true;
            }
            else
            {
                operationContext.ServiceSecurityContext.AuthorizationContext.Properties["Principal"] = operationContext.IncomingMessageProperties["Principal"];
            }
            return true;
        } 
    }
}