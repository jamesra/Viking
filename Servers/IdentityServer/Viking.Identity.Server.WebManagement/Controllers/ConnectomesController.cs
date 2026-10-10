using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Authorization;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    [Authorize]
    public class ConnectomesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorization;

        public ConnectomesController(ApplicationDbContext context, IAuthorizationService authorization)
        {
            _context = context;
            _authorization = authorization;
        }

        public async Task<IActionResult> Index()
        {
            var all = await _context.Connectomes
                .Include(c => c.Parent)
                .Include(c => c.DefaultAnnotationContext)
                .Include(c => c.AnnotationContexts)
                .Include(c => c.AnnotationServers)
                .Include(c => c.Volumes)
                .ToListAsync();

            var accessible = await _authorization.FilterAccessibleResourcesAsync(
                _context, HttpContext.User, all, nameof(Connectome));
            return View(accessible.OrderBy(c => c.Name).ToList());
        }

        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
                return NotFound();

            var connectome = await _context.Connectomes
                .Include(c => c.Parent)
                .Include(c => c.DefaultAnnotationContext)
                .Include(c => c.AnnotationContexts)
                    .ThenInclude(ac => ac.AnnotationServer)
                .Include(c => c.AnnotationContexts)
                    .ThenInclude(ac => ac.Volume)
                .Include(c => c.AnnotationServers)
                .Include(c => c.Volumes)
                    .ThenInclude(v => v.Mirrors)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (connectome == null)
                return NotFound();

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, connectome)
                && false == await CanSeeAnyContextAsync(connectome))
            {
                return Forbid();
            }

            return View(connectome);
        }

        private async Task<bool> CanSeeAnyContextAsync(Connectome connectome)
        {
            var contexts = connectome.AnnotationContexts?.ToList() ?? new System.Collections.Generic.List<AnnotationContext>();
            if (contexts.Count == 0)
                return false;
            var visible = await _authorization.FilterAccessibleResourcesAsync(
                _context, HttpContext.User, contexts, nameof(AnnotationContext));
            return visible.Count > 0;
        }
    }
}
