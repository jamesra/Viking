using System.Threading.Tasks;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Viking.Identity.Models;

namespace Viking.Identity.Server.Standalone.Controllers
{
    /// <summary>
    /// Issuer-side logout for Duende end_session. Login UI lives on WebManagement (:4001).
    /// </summary>
    [AllowAnonymous]
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IIdentityServerInteractionService _interaction;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            IIdentityServerInteractionService interaction,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _interaction = interaction;
            _logger = logger;
        }

        /// <summary>
        /// Completes OIDC end_session: signs out the shared Identity cookie and redirects to
        /// the client's post-logout URI when Duende provides one.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Logout(string logoutId)
        {
            return await CompleteLogoutAsync(logoutId);
        }

        [HttpPost]
        [ActionName(nameof(Logout))]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutPost(string logoutId)
        {
            return await CompleteLogoutAsync(logoutId);
        }

        private async Task<IActionResult> CompleteLogoutAsync(string logoutId)
        {
            var context = await _interaction.GetLogoutContextAsync(logoutId);

            if (User?.Identity?.IsAuthenticated == true)
            {
                await _signInManager.SignOutAsync();
                _logger.LogInformation("User signed out on issuer after end_session.");
            }

            if (!string.IsNullOrWhiteSpace(context?.PostLogoutRedirectUri))
            {
                return Redirect(context.PostLogoutRedirectUri);
            }

            return View("LoggedOut");
        }
    }
}
