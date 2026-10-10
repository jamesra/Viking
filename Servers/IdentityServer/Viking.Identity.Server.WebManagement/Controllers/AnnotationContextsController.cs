using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Authorization;
using Viking.Identity.Server.Extensions.Services;
using Viking.Identity.Server.WebManagement.Helpers;
using Viking.Identity.Server.WebManagement.Models.UserViewModels;

namespace Viking.Identity.Server.WebManagement.Controllers
{ 
    [Authorize]
    public class AnnotationContextsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorization;
        private readonly ResourceProvisioningService _provisioning;
        private readonly CollaboratorOnboardingService _onboarding;
        private readonly AnnotationServerCatalogSync _catalogSync;

        public AnnotationContextsController(
            ApplicationDbContext context,
            IAuthorizationService authorization,
            ResourceProvisioningService provisioning,
            CollaboratorOnboardingService onboarding,
            AnnotationServerCatalogSync catalogSync)
        {
            _context = context;
            _authorization = authorization;
            _provisioning = provisioning;
            _onboarding = onboarding;
            _catalogSync = catalogSync;
        }

        // GET: Volumes
        public async Task<IActionResult> Index()
        {
            var allVolumes = await _context.AnnotationContexts
                .Include(v => v.Parent)
                .Include(v => v.AnnotationServer)
                .Include(v => v.UsersWithPermissions)
                .Include(v => v.GroupsWithPermissions)
                .ToListAsync();

            var accessibleVolumes = await _authorization.FilterAccessibleResourcesAsync(
                _context, HttpContext.User, allVolumes, nameof(AnnotationContext));

            return View(accessibleVolumes);
        }

        // GET: Volumes/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var volume = await _context.WithCatalog()
                .Include(v => v.Parent)
                .Include(v => v.ResourceType)
                .Include(v => v.UsersWithPermissions)
                .Include(v => v.GroupsWithPermissions)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (volume == null)
            {
                return NotFound();
            }

            if (false == await _authorization.CanViewResourceAsync(_context, HttpContext.User, volume))
            {
                return Forbid();
            }

            return View(volume);
        }

        // GET: Volumes/Create
        public IActionResult Create(long? parentOrgId = null)
        {
            ViewBag.AvailableParents = OrgUnitSelectListHelper.AvailableParents(_context, parentOrgId);
            var viewModel = new CreateAnnotationContextViewModel();
            if (parentOrgId.HasValue && parentOrgId.Value > 0)
            {
                viewModel.ParentId = parentOrgId.Value;
            }
            return View(viewModel);
        }

        [HttpGet]
        public IActionResult CreateContinue([Bind("Id,Name,Description,ParentId")] CreateResourceViewModel model)
        {
            //Continues creation after user selects a resource type
            model.ResourceTypeId = nameof(AnnotationContext);
            ViewBag.AvailableParents = OrgUnitSelectListHelper.AvailableParents(_context, model.ParentId);
            return View(nameof(Create), new CreateAnnotationContextViewModel(model));
        }

        // POST: Volumes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Endpoint,Name,Description,ParentId,URL")] CreateAnnotationContextViewModel model)
        {
            if (_context.IsResourceNameTaken(model.Name, nameof(AnnotationContext)))
            {
                ModelState.AddModelError(nameof(model.Name), $"A volume named {model.Name} already exists");
            }

            if (ModelState.IsValid)
            {
                var authProbe = new AnnotationContext
                {
                    Name = model.Name,
                    ParentID = model.ParentId == 0 ? null : model.ParentId,
                    Description = model.Description,
                    Endpoint = model.URL
                };

                if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, authProbe))
                {
                    return Unauthorized();
                }

                var volume = await _provisioning.CreateAnnotationContextAsync(model.Name, model.Description, model.ParentId, model.URL);
                var sync = await _catalogSync.SyncVolumeAsync(volume.Id, HttpContext.RequestAborted);
                TempData["SuccessMessage"] = $"Created volume {volume.Name}. {sync.Message}";
                return RedirectToAction(nameof(Details), new { id = volume.Id });
            }

            ViewBag.AvailableParents = OrgUnitSelectListHelper.AvailableParents(_context, model.ParentId);
            return View(model);
        }

        // GET: Volumes/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var volume = await _context.WithCatalog().FirstOrDefaultAsync(v => v.Id == id);
            if (volume == null)
            {
                return NotFound();
            }
            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, volume))
            {
                return Forbid();
            }
            ViewBag.AvailableParents = OrgUnitSelectListHelper.AvailableParents(_context, volume.ParentID);
            await LoadAnnotationServerChoicesAsync();
            return View(volume);
        }

        private async Task LoadAnnotationServerChoicesAsync()
        {
            ViewBag.AvailableAnnotationServers = await _context.AnnotationServers
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        // POST: Volumes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            long id,
            [Bind("Endpoint,Id,Name,Description,ParentID,RegistrationName,AnnotationServerId")] AnnotationContext context,
            long[] linkedAnnotationServerIds)
        {
            if (id != context.Id)
            {
                return NotFound();
            }

            var existing = await _context.WithCatalog().FirstOrDefaultAsync(v => v.Id == id);
            if (existing == null)
            {
                return NotFound();
            }

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, existing))
            {
                return Unauthorized();
            }

            if (context.ParentID != existing.ParentID)
            {
                var reparentProbe = new AnnotationContext
                {
                    ParentID = context.ParentID,
                    ResourceTypeId = nameof(AnnotationContext)
                };
                if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, reparentProbe))
                {
                    return Forbid();
                }
            }

            if (_context.IsResourceNameTaken(context.Name, nameof(AnnotationContext), context.Id))
            {
                ModelState.AddModelError(nameof(context.Name), $"A context named {context.Name} already exists");
            }

            if (context.AnnotationServerId.HasValue
                && !await _context.AnnotationServers.AnyAsync(s => s.Id == context.AnnotationServerId.Value))
            {
                ModelState.AddModelError(nameof(context.AnnotationServerId), "The selected annotation server does not exist");
            }

            linkedAnnotationServerIds ??= Array.Empty<long>();
            var unknownServers = linkedAnnotationServerIds.Except(
                await _context.AnnotationServers.Where(x => linkedAnnotationServerIds.Contains(x.Id)).Select(x => x.Id).ToListAsync());
            if (unknownServers.Any())
            {
                ModelState.AddModelError(string.Empty, "A selected annotation server does not exist");
            }

            if (ModelState.IsValid)
            {
                var endpointChanged = !AnnotationServerCatalogSync.SameUrl(existing.Endpoint, context.Endpoint);
                // An empty selection returns control to the VikingXML VolumeToEndpoint; a chosen server overrides it.
                var serverChanged = context.AnnotationServerId.HasValue != existing.AnnotationServerPinned
                    || (context.AnnotationServerId.HasValue && context.AnnotationServerId != existing.AnnotationServerId);
                try
                {
                    existing.Name = context.Name;
                    existing.Description = context.Description;
                    existing.ParentID = context.ParentID;
                    existing.Endpoint = context.Endpoint;
                    existing.RegistrationName = string.IsNullOrWhiteSpace(context.RegistrationName) ? null : context.RegistrationName.Trim();

                    // A chosen default is a manual override the catalog sync keeps; "Use VikingXML" hands control back.
                    // The other linked servers are alternates offered to the user at login.
                    var linked = linkedAnnotationServerIds.ToList();
                    if (context.AnnotationServerId.HasValue && !linked.Contains(context.AnnotationServerId.Value))
                        linked.Add(context.AnnotationServerId.Value);

                    existing.AnnotationServerPinned = context.AnnotationServerId.HasValue;
                    await AnnotationContextServerLinks.ReplaceLinksAsync(
                        _context,
                        existing,
                        linked,
                        context.AnnotationServerId,
                        HttpContext.RequestAborted);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VolumeExists(context.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                if (endpointChanged || (serverChanged && !existing.AnnotationServerPinned))
                {
                    var sync = await _catalogSync.SyncVolumeAsync(existing.Id, HttpContext.RequestAborted);
                    TempData["SuccessMessage"] = $"Saved {existing.Name}. {sync.Message}";
                }
                else
                {
                    TempData["SuccessMessage"] = $"Saved {existing.Name}.";
                }
                return RedirectToAction(nameof(Details), new { id = existing.Id });
            }
            ViewBag.AvailableParents = OrgUnitSelectListHelper.AvailableParents(_context, context.ParentID);
            await LoadAnnotationServerChoicesAsync();
            context.AnnotationServer = existing.AnnotationServer;
            context.AnnotationServerPinned = existing.AnnotationServerPinned;
            context.AnnotationServerLinks.Clear();
            foreach (var link in existing.AnnotationServerLinks)
                context.AnnotationServerLinks.Add(link);
            context.Volume = existing.Volume;
            return View(context);
        }

        /// <summary>
        /// Re-reads the context's VikingXML (linking it to its annotation server) and checks every mirror.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resync(long id)
        {
            var context = await _context.AnnotationContexts.Include(v => v.Parent).FirstOrDefaultAsync(v => v.Id == id);
            if (context == null)
                return NotFound();

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, context))
                return Forbid();

            var sync = await _catalogSync.SyncVolumeAsync(id, HttpContext.RequestAborted);
            var message = sync.Message;
            if (context.VolumeId.HasValue)
            {
                var mirrors = await _catalogSync.CheckMirrorsAsync(context.VolumeId.Value, HttpContext.RequestAborted);
                var differing = mirrors.Count(m => !m.MatchesVolume);
                message += $" Checked {mirrors.Count} mirror(s); {differing} did not match.";
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Volumes/Mirrors/5
        public async Task<IActionResult> Mirrors(long? id)
        {
            if (id == null)
                return NotFound();

            var context = await _context.WithCatalog().Include(v => v.Parent).FirstOrDefaultAsync(v => v.Id == id);
            if (context == null)
                return NotFound();

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, context))
                return Forbid();

            if (context.Volume != null)
            {
                ViewBag.SharedWith = await _context.AnnotationContexts
                    .Where(v => v.VolumeId == context.VolumeId && v.Id != context.Id)
                    .Select(v => v.Name)
                    .ToListAsync();
            }

            return View(context);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateVolume(long id, string name, string versionLabel, VolumePixelSpace pixelSpace)
        {
            var (context, denied) = await LoadForMirrorEditAsync(id);
            if (denied != null)
                return denied;

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Image set name is required.";
                return RedirectToAction(nameof(Mirrors), new { id });
            }

            context.Volume.Name = name.Trim();
            context.Volume.VersionLabel = string.IsNullOrWhiteSpace(versionLabel) ? null : versionLabel.Trim();
            context.Volume.PixelSpace = pixelSpace;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Image set updated.";
            return RedirectToAction(nameof(Mirrors), new { id });
        }

        /// <summary>
        /// Adds a host that serves an exact copy of the image set. A different build belongs in a new context.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMirror(long id, string url, string region, int priority)
        {
            var (context, denied) = await LoadForMirrorEditAsync(id);
            if (denied != null)
                return denied;

            if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var mirrorUrl)
                || (mirrorUrl.Scheme != Uri.UriSchemeHttp && mirrorUrl.Scheme != Uri.UriSchemeHttps))
            {
                TempData["ErrorMessage"] = "Mirror URL must be an absolute http or https URL to a VikingXML file.";
                return RedirectToAction(nameof(Mirrors), new { id });
            }

            if (context.Volume.Mirrors.Any(m => AnnotationServerCatalogSync.SameUrl(m.VikingXmlUrl, mirrorUrl)))
            {
                TempData["ErrorMessage"] = "That URL is already a mirror of this image set.";
                return RedirectToAction(nameof(Mirrors), new { id });
            }

            var mirror = new VolumeMirror
            {
                VikingXmlUrl = mirrorUrl,
                RegionLabel = string.IsNullOrWhiteSpace(region) ? null : region.Trim(),
                Priority = priority,
                Enabled = true
            };
            context.Volume.Mirrors.Add(mirror);
            await _catalogSync.ApplyPrimaryMirrorAsync(context.Volume, HttpContext.RequestAborted);
            await _context.SaveChangesAsync();

            var checks = await _catalogSync.CheckMirrorsAsync(context.Volume.Id, HttpContext.RequestAborted);
            var check = checks.FirstOrDefault(c => c.MirrorId == mirror.Id);
            TempData["SuccessMessage"] = $"Added mirror. Check: {check?.Status ?? "not run"}";
            return RedirectToAction(nameof(Mirrors), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMirror(long id, long mirrorId, string region, int priority, bool enabled)
        {
            var (context, denied) = await LoadForMirrorEditAsync(id);
            if (denied != null)
                return denied;

            var mirror = context.Volume.Mirrors.FirstOrDefault(m => m.Id == mirrorId);
            if (mirror == null)
                return NotFound();

            if (!enabled && !context.Volume.Mirrors.Any(m => m.Id != mirrorId && m.Enabled))
            {
                TempData["ErrorMessage"] = "At least one mirror must stay enabled.";
                return RedirectToAction(nameof(Mirrors), new { id });
            }

            mirror.RegionLabel = string.IsNullOrWhiteSpace(region) ? null : region.Trim();
            mirror.Priority = priority;
            mirror.Enabled = enabled;
            await _catalogSync.ApplyPrimaryMirrorAsync(context.Volume, HttpContext.RequestAborted);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Mirror updated.";
            return RedirectToAction(nameof(Mirrors), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMirror(long id, long mirrorId)
        {
            var (context, denied) = await LoadForMirrorEditAsync(id);
            if (denied != null)
                return denied;

            var mirror = context.Volume.Mirrors.FirstOrDefault(m => m.Id == mirrorId);
            if (mirror == null)
                return NotFound();

            if (!context.Volume.Mirrors.Any(m => m.Id != mirrorId && m.Enabled))
            {
                TempData["ErrorMessage"] = "The last enabled mirror cannot be removed.";
                return RedirectToAction(nameof(Mirrors), new { id });
            }

            context.Volume.Mirrors.Remove(mirror);
            _context.VolumeMirrors.Remove(mirror);
            await _catalogSync.ApplyPrimaryMirrorAsync(context.Volume, HttpContext.RequestAborted);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Mirror removed.";
            return RedirectToAction(nameof(Mirrors), new { id });
        }

        private async Task<(AnnotationContext Volume, IActionResult Denied)> LoadForMirrorEditAsync(long id)
        {
            var context = await _context.WithCatalog().Include(v => v.Parent).FirstOrDefaultAsync(v => v.Id == id);
            if (context == null)
                return (null, NotFound());

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, context))
                return (null, Forbid());

            if (context.Volume == null)
            {
                TempData["ErrorMessage"] = "This context has no image set yet. Run Resync to create it from the VikingXML.";
                return (null, RedirectToAction(nameof(Mirrors), new { id }));
            }

            return (context, null);
        }

        // GET: Volumes/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var volume = await _context.AnnotationContexts
                .Include(v => v.Parent)
                .Include(v => v.ResourceType)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (volume == null)
            {
                return NotFound();
            }

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, volume))
            {
                return Forbid();
            }

            return View(volume);
        }

        // POST: Volumes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var volume = await _context.AnnotationContexts.FindAsync(id);
            if(volume == null)
            {
                return NotFound();
            }

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, volume))
            {
                return Unauthorized();
            }

            await _onboarding.DeleteInvitesForAnnotationContextAsync(id);
            var imageSetId = volume.VolumeId;
            _context.AnnotationContexts.Remove(volume);
            await _context.SaveChangesAsync();

            // Image sets are not shared resources; drop one once no volume displays it.
            if (imageSetId.HasValue && false == await _context.AnnotationContexts.AnyAsync(v => v.VolumeId == imageSetId))
            {
                var orphan = await _context.Volumes.FindAsync(imageSetId.Value);
                if (orphan != null)
                {
                    _context.Volumes.Remove(orphan);
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToAction(nameof(Index));
        }

        private bool VolumeExists(long id)
        {
            return _context.AnnotationContexts.Any(e => e.Id == id);
        } 
    }
}
