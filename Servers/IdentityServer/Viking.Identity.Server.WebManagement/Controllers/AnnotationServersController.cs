using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Authorization;
using Viking.Identity.Server.Extensions.Services;
using Viking.Identity.Server.WebManagement.Helpers;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    /// <summary>
    /// Annotation databases. Rows are created by the VikingXML catalog sync; this controller lists them,
    /// edits their name and owner, and runs the sync and grant copy.
    /// </summary>
    [Authorize]
    public class AnnotationServersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorization;
        private readonly AnnotationServerCatalogSync _catalogSync;
        private readonly AnnotationServerGrantCopy _grantCopy;

        public AnnotationServersController(
            ApplicationDbContext context,
            IAuthorizationService authorization,
            AnnotationServerCatalogSync catalogSync,
            AnnotationServerGrantCopy grantCopy)
        {
            _context = context;
            _authorization = authorization;
            _catalogSync = catalogSync;
            _grantCopy = grantCopy;
        }

        // GET: AnnotationServers
        public async Task<IActionResult> Index()
        {
            var servers = await _context.AnnotationServers
                .Include(s => s.Parent)
                .Include(s => s.AnnotationContexts)
                .Include(s => s.UsersWithPermissions)
                .Include(s => s.GroupsWithPermissions)
                .ToListAsync();

            var accessible = await _authorization.FilterAccessibleResourcesAsync(
                _context, HttpContext.User, servers, nameof(AnnotationServer));

            return View(accessible);
        }

        // GET: AnnotationServers/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
                return NotFound();

            var server = await _context.AnnotationServers
                .Include(s => s.Parent)
                .Include(s => s.AnnotationContexts)
                    .ThenInclude(v => v.Volume)
                        .ThenInclude(i => i.Mirrors)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (server == null)
                return NotFound();

            if (false == await _authorization.CanViewResourceAsync(_context, HttpContext.User, server))
                return Forbid();

            return View(server);
        }

        // GET: AnnotationServers/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
                return NotFound();

            var server = await _context.AnnotationServers.Include(s => s.Parent).FirstOrDefaultAsync(s => s.Id == id);
            if (server == null)
                return NotFound();

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, server))
                return Forbid();

            ViewBag.AvailableParents = OrgUnitSelectListHelper.AvailableParents(_context, server.ParentID);
            return View(server);
        }

        // POST: AnnotationServers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Id,Name,Description,ParentID")] AnnotationServer input)
        {
            if (id != input.Id)
                return NotFound();

            var existing = await _context.AnnotationServers
                .Include(s => s.Parent)
                .Include(s => s.AnnotationContexts)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (existing == null)
                return NotFound();

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, existing))
                return Unauthorized();

            if (input.ParentID != existing.ParentID)
            {
                var reparentProbe = new AnnotationServer { ParentID = input.ParentID, ResourceTypeId = nameof(AnnotationServer) };
                if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, reparentProbe))
                    return Forbid();
            }

            if (_context.IsResourceNameTaken(input.Name, nameof(AnnotationServer), input.Id))
                ModelState.AddModelError(nameof(input.Name), $"An annotation server named {input.Name} already exists");

            // A name shared with an unrelated volume would make {Name}.Annotate scopes ambiguous.
            var linkedVolumeIds = existing.AnnotationContexts.Select(v => v.Id).ToList();
            if (await _context.AnnotationContexts.AnyAsync(v => v.Name == input.Name && !linkedVolumeIds.Contains(v.Id)))
                ModelState.AddModelError(nameof(input.Name), $"A volume named {input.Name} uses a different annotation database");

            if (!ModelState.IsValid)
            {
                ViewBag.AvailableParents = OrgUnitSelectListHelper.AvailableParents(_context, input.ParentID);
                input.AnnotationEndpoint = existing.AnnotationEndpoint;
                return View(input);
            }

            existing.Name = input.Name;
            existing.Description = input.Description;
            existing.ParentID = input.ParentID;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Saved annotation server {existing.Name}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Re-reads the VikingXML of every volume linked to this server and checks their mirrors.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resync(long id, CancellationToken cancellationToken)
        {
            var server = await _context.AnnotationServers.Include(s => s.Parent).Include(s => s.AnnotationContexts).FirstOrDefaultAsync(s => s.Id == id);
            if (server == null)
                return NotFound();

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, server))
                return Forbid();

            var volumeIds = server.AnnotationContexts.Select(v => v.Id).ToList();
            var results = new System.Collections.Generic.List<VolumeCatalogSyncResult>();
            foreach (var volumeId in volumeIds)
            {
                var result = await _catalogSync.SyncVolumeAsync(volumeId, cancellationToken);
                results.Add(result);

                var imageSetId = await _context.AnnotationContexts.Where(v => v.Id == volumeId).Select(v => v.VolumeId).FirstOrDefaultAsync(cancellationToken);
                if (imageSetId.HasValue)
                    await _catalogSync.CheckMirrorsAsync(imageSetId.Value, cancellationToken);
            }

            ViewData["Title"] = $"Resync {server.Name}";
            return View("SyncResults", results);
        }

        // GET: AnnotationServers/Catalog
        [Authorize(Roles = Special.Roles.Admin)]
        public async Task<IActionResult> Catalog(CancellationToken cancellationToken)
        {
            return View(await _catalogSync.BuildReportAsync(cancellationToken));
        }

        /// <summary>
        /// Reads every volume's VikingXML, creating annotation servers and image sets as needed.
        /// Run once after all Identity services are on the version that knows the AnnotationServer type.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Special.Roles.Admin)]
        public async Task<IActionResult> SyncAll(CancellationToken cancellationToken)
        {
            var results = await _catalogSync.SyncAllVolumesAsync(cancellationToken);
            ViewData["Title"] = "Volume catalog sync";
            return View("SyncResults", results);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Special.Roles.Admin)]
        public async Task<IActionResult> CopyGrants(CancellationToken cancellationToken)
        {
            return View("GrantCopyReport", await _grantCopy.CopyVolumeGrantsAsync(cancellationToken));
        }
    }
}
