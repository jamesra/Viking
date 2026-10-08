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
    public class VolumesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorization;
        private readonly ResourceProvisioningService _provisioning;
        private readonly CollaboratorOnboardingService _onboarding;
        private readonly AnnotationServerCatalogSync _catalogSync;

        public VolumesController(
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
            var allVolumes = await _context.Volume
                .Include(v => v.Parent)
                .Include(v => v.AnnotationServer)
                .Include(v => v.UsersWithPermissions)
                .Include(v => v.GroupsWithPermissions)
                .ToListAsync();

            var accessibleVolumes = await _authorization.FilterAccessibleResourcesAsync(
                _context, HttpContext.User, allVolumes, nameof(Volume));

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
            var viewModel = new CreateVolumeViewModel();
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
            model.ResourceTypeId = nameof(Volume);
            ViewBag.AvailableParents = OrgUnitSelectListHelper.AvailableParents(_context, model.ParentId);
            return View(nameof(Create), new CreateVolumeViewModel(model));
        }

        // POST: Volumes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Endpoint,Name,Description,ParentId,URL")] CreateVolumeViewModel model)
        {
            if (_context.IsResourceNameTaken(model.Name, nameof(Volume)))
            {
                ModelState.AddModelError(nameof(model.Name), $"A volume named {model.Name} already exists");
            }

            if (ModelState.IsValid)
            {
                var authProbe = new Volume
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

                var volume = await _provisioning.CreateVolumeAsync(model.Name, model.Description, model.ParentId, model.URL);
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
            return View(volume);
        }

        // POST: Volumes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Endpoint,Id,Name,Description,ParentID,RegistrationName")] Volume volume)
        {
            if (id != volume.Id)
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

            if (volume.ParentID != existing.ParentID)
            {
                var reparentProbe = new Volume
                {
                    ParentID = volume.ParentID,
                    ResourceTypeId = nameof(Volume)
                };
                if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, reparentProbe))
                {
                    return Forbid();
                }
            }

            if (_context.IsResourceNameTaken(volume.Name, nameof(Volume), volume.Id))
            {
                ModelState.AddModelError(nameof(volume.Name), $"A volume named {volume.Name} already exists");
            }

            if (ModelState.IsValid)
            {
                var endpointChanged = !AnnotationServerCatalogSync.SameUrl(existing.Endpoint, volume.Endpoint);
                try
                {
                    existing.Name = volume.Name;
                    existing.Description = volume.Description;
                    existing.ParentID = volume.ParentID;
                    existing.Endpoint = volume.Endpoint;
                    existing.RegistrationName = string.IsNullOrWhiteSpace(volume.RegistrationName) ? null : volume.RegistrationName.Trim();
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VolumeExists(volume.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                if (endpointChanged)
                {
                    var sync = await _catalogSync.SyncVolumeAsync(existing.Id, HttpContext.RequestAborted);
                    TempData["SuccessMessage"] = $"Saved {existing.Name}. {sync.Message}";
                }
                return RedirectToAction(nameof(Details), new { id = existing.Id });
            }
            ViewBag.AvailableParents = OrgUnitSelectListHelper.AvailableParents(_context, volume.ParentID);
            volume.AnnotationServer = existing.AnnotationServer;
            volume.ImageSet = existing.ImageSet;
            return View(volume);
        }

        /// <summary>
        /// Re-reads the volume's VikingXML (linking it to its annotation server) and checks every mirror.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resync(long id)
        {
            var volume = await _context.Volume.Include(v => v.Parent).FirstOrDefaultAsync(v => v.Id == id);
            if (volume == null)
                return NotFound();

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, volume))
                return Forbid();

            var sync = await _catalogSync.SyncVolumeAsync(id, HttpContext.RequestAborted);
            var message = sync.Message;
            if (volume.ImageSetId.HasValue)
            {
                var mirrors = await _catalogSync.CheckMirrorsAsync(volume.ImageSetId.Value, HttpContext.RequestAborted);
                var differing = mirrors.Count(m => !m.MatchesImageSet);
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

            var volume = await _context.WithCatalog().Include(v => v.Parent).FirstOrDefaultAsync(v => v.Id == id);
            if (volume == null)
                return NotFound();

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, volume))
                return Forbid();

            if (volume.ImageSet != null)
            {
                ViewBag.SharedWith = await _context.Volume
                    .Where(v => v.ImageSetId == volume.ImageSetId && v.Id != volume.Id)
                    .Select(v => v.Name)
                    .ToListAsync();
            }

            return View(volume);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateImageSet(long id, string name, string versionLabel, ImageSetPixelSpace pixelSpace)
        {
            var (volume, denied) = await LoadForMirrorEditAsync(id);
            if (denied != null)
                return denied;

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Image set name is required.";
                return RedirectToAction(nameof(Mirrors), new { id });
            }

            volume.ImageSet.Name = name.Trim();
            volume.ImageSet.VersionLabel = string.IsNullOrWhiteSpace(versionLabel) ? null : versionLabel.Trim();
            volume.ImageSet.PixelSpace = pixelSpace;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Image set updated.";
            return RedirectToAction(nameof(Mirrors), new { id });
        }

        /// <summary>
        /// Adds a host that serves an exact copy of the image set. A different build belongs in a new volume.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMirror(long id, string url, string region, int priority)
        {
            var (volume, denied) = await LoadForMirrorEditAsync(id);
            if (denied != null)
                return denied;

            if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var mirrorUrl)
                || (mirrorUrl.Scheme != Uri.UriSchemeHttp && mirrorUrl.Scheme != Uri.UriSchemeHttps))
            {
                TempData["ErrorMessage"] = "Mirror URL must be an absolute http or https URL to a VikingXML file.";
                return RedirectToAction(nameof(Mirrors), new { id });
            }

            if (volume.ImageSet.Mirrors.Any(m => AnnotationServerCatalogSync.SameUrl(m.VikingXmlUrl, mirrorUrl)))
            {
                TempData["ErrorMessage"] = "That URL is already a mirror of this image set.";
                return RedirectToAction(nameof(Mirrors), new { id });
            }

            var mirror = new ImageSetMirror
            {
                VikingXmlUrl = mirrorUrl,
                RegionLabel = string.IsNullOrWhiteSpace(region) ? null : region.Trim(),
                Priority = priority,
                Enabled = true
            };
            volume.ImageSet.Mirrors.Add(mirror);
            await _catalogSync.ApplyPrimaryMirrorAsync(volume.ImageSet, HttpContext.RequestAborted);
            await _context.SaveChangesAsync();

            var checks = await _catalogSync.CheckMirrorsAsync(volume.ImageSet.Id, HttpContext.RequestAborted);
            var check = checks.FirstOrDefault(c => c.MirrorId == mirror.Id);
            TempData["SuccessMessage"] = $"Added mirror. Check: {check?.Status ?? "not run"}";
            return RedirectToAction(nameof(Mirrors), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMirror(long id, long mirrorId, string region, int priority, bool enabled)
        {
            var (volume, denied) = await LoadForMirrorEditAsync(id);
            if (denied != null)
                return denied;

            var mirror = volume.ImageSet.Mirrors.FirstOrDefault(m => m.Id == mirrorId);
            if (mirror == null)
                return NotFound();

            if (!enabled && !volume.ImageSet.Mirrors.Any(m => m.Id != mirrorId && m.Enabled))
            {
                TempData["ErrorMessage"] = "At least one mirror must stay enabled.";
                return RedirectToAction(nameof(Mirrors), new { id });
            }

            mirror.RegionLabel = string.IsNullOrWhiteSpace(region) ? null : region.Trim();
            mirror.Priority = priority;
            mirror.Enabled = enabled;
            await _catalogSync.ApplyPrimaryMirrorAsync(volume.ImageSet, HttpContext.RequestAborted);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Mirror updated.";
            return RedirectToAction(nameof(Mirrors), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMirror(long id, long mirrorId)
        {
            var (volume, denied) = await LoadForMirrorEditAsync(id);
            if (denied != null)
                return denied;

            var mirror = volume.ImageSet.Mirrors.FirstOrDefault(m => m.Id == mirrorId);
            if (mirror == null)
                return NotFound();

            if (!volume.ImageSet.Mirrors.Any(m => m.Id != mirrorId && m.Enabled))
            {
                TempData["ErrorMessage"] = "The last enabled mirror cannot be removed.";
                return RedirectToAction(nameof(Mirrors), new { id });
            }

            volume.ImageSet.Mirrors.Remove(mirror);
            _context.ImageSetMirrors.Remove(mirror);
            await _catalogSync.ApplyPrimaryMirrorAsync(volume.ImageSet, HttpContext.RequestAborted);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Mirror removed.";
            return RedirectToAction(nameof(Mirrors), new { id });
        }

        private async Task<(Volume Volume, IActionResult Denied)> LoadForMirrorEditAsync(long id)
        {
            var volume = await _context.WithCatalog().Include(v => v.Parent).FirstOrDefaultAsync(v => v.Id == id);
            if (volume == null)
                return (null, NotFound());

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, volume))
                return (null, Forbid());

            if (volume.ImageSet == null)
            {
                TempData["ErrorMessage"] = "This volume has no image set yet. Run Resync to create it from the VikingXML.";
                return (null, RedirectToAction(nameof(Mirrors), new { id }));
            }

            return (volume, null);
        }

        // GET: Volumes/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var volume = await _context.Volume
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
            var volume = await _context.Volume.FindAsync(id);
            if(volume == null)
            {
                return NotFound();
            }

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, volume))
            {
                return Unauthorized();
            }

            await _onboarding.DeleteInvitesForVolumeAsync(id);
            var imageSetId = volume.ImageSetId;
            _context.Volume.Remove(volume);
            await _context.SaveChangesAsync();

            // Image sets are not shared resources; drop one once no volume displays it.
            if (imageSetId.HasValue && false == await _context.Volume.AnyAsync(v => v.ImageSetId == imageSetId))
            {
                var orphan = await _context.ImageSets.FindAsync(imageSetId.Value);
                if (orphan != null)
                {
                    _context.ImageSets.Remove(orphan);
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToAction(nameof(Index));
        }

        private bool VolumeExists(long id)
        {
            return _context.Volume.Any(e => e.Id == id);
        } 
    }
}
