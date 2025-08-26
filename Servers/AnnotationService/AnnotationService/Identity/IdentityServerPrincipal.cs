using System.Collections.Generic;
using System.Security.Principal;

namespace Annotation.Identity
{
    public class IdentityServerPrincipal : IPrincipal
    {
        public IIdentity Identity {get;}

        /// <summary>
        /// Token associated with identity
        /// </summary>
        public string Token { get; }

        private readonly List<string> ValidatedClaims = new List<string>();

        public bool IsInRole(string role)
        {
            if (ValidatedClaims.Contains(role))
                return true; 

            string VolumeName = VikingWebAppSettings.AppSettings.GetApplicationSetting("VolumeName");
            string ClaimRequired = GetClaimRequired(VolumeName, role);

            var validated = IdentityServerHelper.CheckClaims(Token, ClaimRequired).Result;
            if(validated)
            {
                ValidatedClaims.Add(role);
            }

            return validated;
        }

        private string GetClaimRequired(string VolumeName, string permission)
        {
            return $"{VolumeName}.{permission}";
        }
    }
}