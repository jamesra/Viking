using System;
using System.Collections.Generic;
using System.Security.Principal;
using Viking.Tokens;

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
        private static BearerTokenHelper _tokenHelper;

        private static BearerTokenHelper GetTokenHelper()
        {
            if (_tokenHelper == null)
            {
                _tokenHelper = BearerTokenHelper.CreateFromAppSettings();
                if (_tokenHelper == null)
                {
                    // Fallback: create from settings directly
                    string IdentityServerEndpoint = VikingWebAppSettings.AppSettings.GetIdentityServerURLString();
                    if (System.Uri.TryCreate(IdentityServerEndpoint, UriKind.Absolute, out Uri identityServerUrl))
                    {
                        _tokenHelper = new BearerTokenHelper
                        {
                            IdentityServerURL = identityServerUrl,
                            ClientSecret = "CorrectHorseBatteryStaple"
                        };
                    }
                }
            }
            return _tokenHelper;
        }

        public bool IsInRole(string role)
        {
            if (ValidatedClaims.Contains(role))
                return true; 

            string VolumeName = VikingWebAppSettings.AppSettings.GetApplicationSetting("VolumeName");
            string ClaimRequired = GetClaimRequired(VolumeName, role);

            var helper = GetTokenHelper();
            if (helper == null)
                return false;

            var validated = helper.CheckClaims(Token, ClaimRequired).Result;
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