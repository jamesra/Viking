using System;
using Microsoft.AspNetCore.Mvc;
using Viking.Identity.Server.WebManagement.Controllers;

namespace Viking.Identity.Server.WebManagement.Extensions
{
    public static class UrlHelperExtensions
    {
        /// <summary>Build email confirmation URL using request scheme (e.g. behind proxy).</summary>
        public static string EmailConfirmationLink(this IUrlHelper urlHelper, string userId, string code, string scheme)
        {
            return urlHelper.Action(
                action: nameof(AccountController.ConfirmEmail),
                controller: "Account",
                values: new { userId, code },
                protocol: scheme);
        }

        /// <summary>Build email confirmation URL using configured authority (preferred behind TLS-terminating proxy).</summary>
        public static string EmailConfirmationLink(this IUrlHelper urlHelper, string userId, string code, string scheme, string baseAuthority)
        {
            if (!string.IsNullOrWhiteSpace(baseAuthority))
            {
                var path = urlHelper.Action(
                    action: nameof(AccountController.ConfirmEmail),
                    controller: "Account",
                    values: new { userId, code },
                    protocol: null);
                if (!string.IsNullOrEmpty(path))
                    return new Uri(new Uri(baseAuthority.TrimEnd('/')), path).ToString();
            }
            return urlHelper.Action(
                action: nameof(AccountController.ConfirmEmail),
                controller: "Account",
                values: new { userId, code },
                protocol: scheme);
        }

        /// <summary>Build password reset URL using request scheme.</summary>
        public static string ResetPasswordCallbackLink(this IUrlHelper urlHelper, string userId, string code, string scheme)
        {
            return urlHelper.Action(
                action: nameof(AccountController.ResetPassword),
                controller: "Account",
                values: new { userId, code },
                protocol: scheme);
        }

        /// <summary>Build password reset URL using configured authority (preferred behind TLS-terminating proxy).</summary>
        public static string ResetPasswordCallbackLink(this IUrlHelper urlHelper, string userId, string code, string scheme, string baseAuthority)
        {
            if (!string.IsNullOrWhiteSpace(baseAuthority))
            {
                var path = urlHelper.Action(
                    action: nameof(AccountController.ResetPassword),
                    controller: "Account",
                    values: new { userId, code },
                    protocol: null);
                if (!string.IsNullOrEmpty(path))
                    return new Uri(new Uri(baseAuthority.TrimEnd('/')), path).ToString();
            }
            return urlHelper.Action(
                action: nameof(AccountController.ResetPassword),
                controller: "Account",
                values: new { userId, code },
                protocol: scheme);
        }
    }
}
