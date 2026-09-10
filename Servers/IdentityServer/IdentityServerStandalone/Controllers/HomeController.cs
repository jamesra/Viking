using System.Threading.Tasks;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Viking.Identity.Server.Standalone.Controllers
{
    /// <summary>
    /// Issuer error page for Duende UserInteraction.ErrorUrl and ASP.NET exception handler.
    /// </summary>
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly IIdentityServerInteractionService _interaction;

        public HomeController(IIdentityServerInteractionService interaction)
        {
            _interaction = interaction;
        }

        [HttpGet]
        public async Task<IActionResult> Error(string errorId)
        {
            var vm = new ErrorViewModel();
            if (!string.IsNullOrWhiteSpace(errorId))
            {
                var message = await _interaction.GetErrorContextAsync(errorId);
                if (message != null)
                {
                    vm.Error = message;
                }
            }

            return View(vm);
        }
    }

    public class ErrorViewModel
    {
        public ErrorMessage Error { get; set; }
    }
}
