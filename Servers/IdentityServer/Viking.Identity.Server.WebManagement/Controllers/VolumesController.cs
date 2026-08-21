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

        public VolumesController(
            ApplicationDbContext context,
            IAuthorizationService authorization,
            ResourceProvisioningService provisioning,
            CollaboratorOnboardingService onboarding)
        {
            _context = context;
            _authorization = authorization;
            _provisioning = provisioning;
            _onboarding = onboarding;
        }

        // GET: Volumes
        public async Task<IActionResult> Index()
        {
            var allVolumes = await _context.Volume
                .Include(v => v.Parent)
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

            var volume = await _context.Volume
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

                await _provisioning.CreateVolumeAsync(model.Name, model.Description, model.ParentId, model.URL);
                return RedirectToAction(nameof(Index));
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

            var volume = await _context.Volume.FindAsync(id);
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
        public async Task<IActionResult> Edit(long id, [Bind("Endpoint,Id,Name,Description,ParentID")] Volume volume)
        {
            if (id != volume.Id)
            {
                return NotFound();
            }

            var existing = await _context.Volume.FirstOrDefaultAsync(v => v.Id == id);
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
                try
                {
                    existing.Name = volume.Name;
                    existing.Description = volume.Description;
                    existing.ParentID = volume.ParentID;
                    existing.Endpoint = volume.Endpoint;
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
                return RedirectToAction(nameof(Index));
            }
            ViewBag.AvailableParents = OrgUnitSelectListHelper.AvailableParents(_context, volume.ParentID);
            return View(volume);
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
            _context.Volume.Remove(volume);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool VolumeExists(long id)
        {
            return _context.Volume.Any(e => e.Id == id);
        } 
    }
}
