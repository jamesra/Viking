using System;
using System.Collections.Generic;
using System.IdentityModel.Claims;
using System.IdentityModel.Policy;
using System.Security.Principal;

namespace Annotation.Identity
{
    public class IdentityServerAuthorizationPolicy : IAuthorizationPolicy
    {
        private string id = Guid.NewGuid().ToString();

        public string Id
        {
            get { return id; }
        }

        public ClaimSet Issuer
        {
            get { return ClaimSet.System; }
        }

        public bool Evaluate(EvaluationContext evaluationContext, ref object state)
        {
            // Get the current principal from the evaluation context
            IPrincipal principal = null;
            if (evaluationContext.Properties.TryGetValue("Principal", out object principalObj))
            {
                principal = principalObj as IPrincipal;
            }

            // If no principal is set, create an anonymous one
            if (principal == null)
            {
                principal = new GenericPrincipal(new GenericIdentity("Anonymous"), new string[] { "Read" });
                evaluationContext.Properties["Principal"] = principal;
            }

            // Add the principal to the evaluation context
            evaluationContext.Properties["Principal"] = principal;

            return true;
        }
    }
}
