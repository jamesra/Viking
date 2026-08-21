using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Authorization;
using Viking.Identity.Server.WebManagement.Models.UserViewModels;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    [Authorize]
    public class SegmentationServicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorization;

        public SegmentationServicesController(ApplicationDbContext context, IAuthorizationService authorization)
        {
            _context = context;
            _authorization = authorization;
        }

        // GET: SegmentationServices
        public async Task<IActionResult> Index()
        {
            var allServices = await _context.SegmentationServices
                .Include(s => s.Parent)
                .ToListAsync();

            var accessibleServices = await _authorization.FilterAccessibleResourcesAsync(
                _context, HttpContext.User, allServices, nameof(SegmentationService));

            return View(accessibleServices);
        }

        // GET: SegmentationServices/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var segmentationService = await _context.SegmentationServices
                .Include(s => s.Parent)
                .Include(s => s.ResourceType)
                .Include(s => s.UsersWithPermissions)
                .Include(s => s.GroupsWithPermissions)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (segmentationService == null)
            {
                return NotFound();
            }

            if (false == await _authorization.CanViewResourceAsync(_context, HttpContext.User, segmentationService))
            {
                return Forbid();
            }

            return View(segmentationService);
        }

        // GET: SegmentationServices/Create
        public IActionResult Create(long? parentOrgId = null)
        {
            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), parentOrgId);
            var viewModel = new CreateSegmentationServiceViewModel();
            if (parentOrgId.HasValue && parentOrgId.Value > 0)
            {
                viewModel.ParentId = parentOrgId.Value;
            }
            return View(viewModel);
        }

        [HttpGet]
        public IActionResult CreateContinue([Bind("Id,Name,Description,ParentId")] CreateResourceViewModel model)
        {
            model.ResourceTypeId = nameof(SegmentationService);
            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), model.ParentId);
            return View(nameof(Create), new CreateSegmentationServiceViewModel(model));
        }

        // POST: SegmentationServices/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Endpoint,Name,Description,ParentId")] CreateSegmentationServiceViewModel model)
        {
            if (_context.IsResourceNameTaken(model.Name, nameof(SegmentationService)))
            {
                ModelState.AddModelError(nameof(model.Name), $"A segmentation service named {model.Name} already exists");
            }

            if (ModelState.IsValid)
            {
                var segmentationService = new SegmentationService
                {
                    Name = model.Name,
                    ParentID = model.ParentId == 0 ? null : model.ParentId,
                    Description = model.Description,
                    Endpoint = model.Endpoint
                };

                if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, segmentationService))
                {
                    return Unauthorized();
                }

                _context.SegmentationServices.Add(segmentationService);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), model.ParentId);
            return View(model);
        }

        // GET: SegmentationServices/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var segmentationService = await _context.SegmentationServices.FindAsync(id);
            if (segmentationService == null)
            {
                return NotFound();
            }

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, segmentationService))
            {
                return Forbid();
            }

            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), segmentationService.ParentID);
            return View(segmentationService);
        }

        // POST: SegmentationServices/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Endpoint,Id,Name,Description,ParentID")] SegmentationService segmentationService)
        {
            if (id != segmentationService.Id)
            {
                return NotFound();
            }

            var existing = await _context.SegmentationServices.FirstOrDefaultAsync(s => s.Id == id);
            if (existing == null)
            {
                return NotFound();
            }

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, existing))
            {
                return Unauthorized();
            }

            if (segmentationService.ParentID != existing.ParentID)
            {
                var reparentProbe = new SegmentationService
                {
                    ParentID = segmentationService.ParentID,
                    ResourceTypeId = nameof(SegmentationService)
                };
                if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, reparentProbe))
                {
                    return Forbid();
                }
            }

            if (_context.IsResourceNameTaken(segmentationService.Name, nameof(SegmentationService), segmentationService.Id))
            {
                ModelState.AddModelError(nameof(segmentationService.Name), $"A segmentation service named {segmentationService.Name} already exists");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    existing.Name = segmentationService.Name;
                    existing.Description = segmentationService.Description;
                    existing.ParentID = segmentationService.ParentID;
                    existing.Endpoint = segmentationService.Endpoint;
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SegmentationServiceExists(segmentationService.Id))
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
            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), segmentationService.ParentID);
            return View(segmentationService);
        }

        // GET: SegmentationServices/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var segmentationService = await _context.SegmentationServices
                .Include(s => s.Parent)
                .Include(s => s.ResourceType)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (segmentationService == null)
            {
                return NotFound();
            }

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, segmentationService))
            {
                return Forbid();
            }

            return View(segmentationService);
        }

        // POST: SegmentationServices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var segmentationService = await _context.SegmentationServices.FindAsync(id);
            if (segmentationService == null)
            {
                return NotFound();
            }

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, segmentationService))
            {
                return Unauthorized();
            }

            _context.SegmentationServices.Remove(segmentationService);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SegmentationServiceExists(long id)
        {
            return _context.SegmentationServices.Any(e => e.Id == id);
        }
    }
}







