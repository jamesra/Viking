using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Viking.Identity.Server.Extensions.Services;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    /// <summary>
    /// Administrator help: a diagram of how Identity resources relate and a glossary of what each one is for.
    /// Limited to site administrators and organizational unit administrators (see <see cref="HelpAccess"/>).
    /// </summary>
    [Authorize]
    public class HelpController : Controller
    {
        readonly HelpAccess _access;

        public HelpController(HelpAccess access)
        {
            _access = access;
        }

        // GET: Help
        public async Task<IActionResult> Index()
        {
            if (!await _access.CanViewAsync(User))
                return Forbid();

            return View();
        }
    }
}
