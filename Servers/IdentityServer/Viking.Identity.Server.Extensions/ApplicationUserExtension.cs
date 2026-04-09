using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viking.Identity.Server
{
    public static class ApplicationUserExtension
    {
        public static string GetUsername(this System.Security.Principal.IIdentity identity)
        {
            if (identity.IsAuthenticated == false)
                return null;

            if (identity.Name != null)
                return identity.Name;

            if (identity is System.Security.Claims.ClaimsIdentity principal)
            {
                var result =  principal.Claims.FirstOrDefault(c => c.Type.Equals("name", StringComparison.OrdinalIgnoreCase))?.Value;
                return result;
            }

            return null;
        } 
    }
}
