using System.Collections.Generic;
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
    /// <summary>
    /// Management pages for <see cref="CorrectionService"/> resources. Contexts are assigned to a service on the
    /// annotation context's Edit page; deleting a service unassigns its contexts.
    /// </summary>
    [Authorize]
    public class CorrectionServicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorization;

        public CorrectionServicesController(ApplicationDbContext context, IAuthorizationService authorization)
        {
            _context = context;
            _authorization = authorization;
        }

        // GET: CorrectionServices
        public async Task<IActionResult> Index()
        {
            var allServices = await _context.CorrectionServices
                .Include(s => s.Parent)
                .Include(s => s.AnnotationContexts)
                .Include(s => s.UsersWithPermissions)
                .Include(s => s.GroupsWithPermissions)
                .ToListAsync();

            var accessibleServices = await _authorization.FilterAccessibleResourcesAsync(
                _context, HttpContext.User, allServices, nameof(CorrectionService));

            return View(accessibleServices);
        }

        // GET: CorrectionServices/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var correctionService = await _context.CorrectionServices
                .Include(s => s.Parent)
                .Include(s => s.ResourceType)
                .Include(s => s.AnnotationContexts)
                .Include(s => s.UsersWithPermissions)
                .Include(s => s.GroupsWithPermissions)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (correctionService == null)
            {
                return NotFound();
            }

            if (false == await _authorization.CanViewResourceAsync(_context, HttpContext.User, correctionService))
            {
                return Forbid();
            }

            return View(correctionService);
        }

        // GET: CorrectionServices/Create
        public IActionResult Create(long? parentOrgId = null)
        {
            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), parentOrgId);
            var viewModel = new CreateCorrectionServiceViewModel();
            if (parentOrgId.HasValue && parentOrgId.Value > 0)
            {
                viewModel.ParentId = parentOrgId.Value;
            }
            return View(viewModel);
        }

        [HttpGet]
        public IActionResult CreateContinue([Bind("Id,Name,Description,ParentId")] CreateResourceViewModel model)
        {
            model.ResourceTypeId = nameof(CorrectionService);
            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), model.ParentId);
            return View(nameof(Create), new CreateCorrectionServiceViewModel(model));
        }

        // POST: CorrectionServices/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Endpoint,Name,Description,ParentId")] CreateCorrectionServiceViewModel model)
        {
            if (_context.IsResourceNameTaken(model.Name, nameof(CorrectionService)))
            {
                ModelState.AddModelError(nameof(model.Name), $"A correction service named {model.Name} already exists");
            }

            if (ModelState.IsValid)
            {
                var correctionService = new CorrectionService
                {
                    Name = model.Name,
                    ParentID = model.ParentId == 0 ? null : model.ParentId,
                    Description = model.Description,
                    Endpoint = model.Endpoint
                };

                if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, correctionService))
                {
                    return Unauthorized();
                }

                _context.CorrectionServices.Add(correctionService);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), model.ParentId);
            return View(model);
        }

        // GET: CorrectionServices/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var correctionService = await _context.CorrectionServices.FindAsync(id);
            if (correctionService == null)
            {
                return NotFound();
            }

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, correctionService))
            {
                return Forbid();
            }

            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), correctionService.ParentID);
            ViewBag.ContextRows = await LoadContextRowsAsync(correctionService.Id, null);
            return View(correctionService);
        }

        /// <summary>
        /// One annotation context in the Edit page's assignment list.
        /// </summary>
        /// <param name="Id">Annotation context id.</param>
        /// <param name="Name">Context name.</param>
        /// <param name="Connectome">Name of its connectome, or null.</param>
        /// <param name="Assigned">True when the box is checked: assigned to this service, or chosen in a rejected post.</param>
        /// <param name="OtherService">Name of a different correction service it is assigned to now; checking it moves it here.</param>
        /// <param name="CanChange">False when the user does not administer the context's organizational unit; it is shown but never changed.</param>
        public sealed record ContextAssignmentRow(long Id, string Name, string Connectome, bool Assigned, string OtherService, bool CanChange);

        /// <summary>
        /// Lists every annotation context for the assignment checklist.
        /// </summary>
        /// <param name="serviceId">The service being edited.</param>
        /// <param name="chosen">Ids ticked in a rejected post, or null to reflect the saved assignments.</param>
        async Task<List<ContextAssignmentRow>> LoadContextRowsAsync(long serviceId, ICollection<long> chosen)
        {
            var contexts = await _context.AnnotationContexts
                .AsNoTracking()
                .Include(c => c.Connectome)
                .Include(c => c.CorrectionService)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var rows = new List<ContextAssignmentRow>(contexts.Count);
            foreach (var c in contexts)
            {
                bool assigned = chosen != null ? chosen.Contains(c.Id) : c.CorrectionServiceId == serviceId;
                string other = c.CorrectionServiceId.HasValue && c.CorrectionServiceId != serviceId ? c.CorrectionService?.Name : null;
                bool canChange = await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, c);
                rows.Add(new ContextAssignmentRow(c.Id, c.Name, c.Connectome?.Name, assigned, other, canChange));
            }

            return rows;
        }

        // POST: CorrectionServices/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Endpoint,Id,Name,Description,ParentID")] CorrectionService correctionService, long[] assignedContextIds)
        {
            assignedContextIds ??= System.Array.Empty<long>();
            if (id != correctionService.Id)
            {
                return NotFound();
            }

            var existing = await _context.CorrectionServices.FirstOrDefaultAsync(s => s.Id == id);
            if (existing == null)
            {
                return NotFound();
            }

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, existing))
            {
                return Unauthorized();
            }

            if (correctionService.ParentID != existing.ParentID)
            {
                var reparentProbe = new CorrectionService
                {
                    ParentID = correctionService.ParentID,
                    ResourceTypeId = nameof(CorrectionService)
                };
                if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, reparentProbe))
                {
                    return Forbid();
                }
            }

            if (_context.IsResourceNameTaken(correctionService.Name, nameof(CorrectionService), correctionService.Id))
            {
                ModelState.AddModelError(nameof(correctionService.Name), $"A correction service named {correctionService.Name} already exists");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    existing.Name = correctionService.Name;
                    existing.Description = correctionService.Description;
                    existing.ParentID = correctionService.ParentID;
                    existing.Endpoint = correctionService.Endpoint;
                    await ApplyContextAssignmentsAsync(existing.Id, assignedContextIds);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CorrectionServiceExists(correctionService.Id))
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
            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), correctionService.ParentID);
            ViewBag.ContextRows = await LoadContextRowsAsync(id, assignedContextIds);
            return View(correctionService);
        }

        /// <summary>
        /// Makes the set of contexts assigned to the service equal <paramref name="assignedContextIds"/>. A ticked
        /// context assigned elsewhere moves here; an unticked one assigned here is released. Contexts whose
        /// organizational unit the user does not administer are skipped: their checkboxes are disabled and not posted.
        /// </summary>
        async Task ApplyContextAssignmentsAsync(long serviceId, long[] assignedContextIds)
        {
            var wanted = new HashSet<long>(assignedContextIds);
            var contexts = await _context.AnnotationContexts.Include(c => c.Parent).ToListAsync();
            foreach (var c in contexts)
            {
                bool shouldAssign = wanted.Contains(c.Id);
                bool assignedHere = c.CorrectionServiceId == serviceId;
                if (shouldAssign == assignedHere)
                    continue;
                if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, c))
                    continue;

                c.CorrectionServiceId = shouldAssign ? serviceId : null;
            }
        }

        // GET: CorrectionServices/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var correctionService = await _context.CorrectionServices
                .Include(s => s.Parent)
                .Include(s => s.ResourceType)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (correctionService == null)
            {
                return NotFound();
            }

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, correctionService))
            {
                return Forbid();
            }

            return View(correctionService);
        }

        // POST: CorrectionServices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var correctionService = await _context.CorrectionServices
                .Include(s => s.AnnotationContexts)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (correctionService == null)
            {
                return NotFound();
            }

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, correctionService))
            {
                return Unauthorized();
            }

            // The Resource self-FK cannot cascade or SetNull on SQL Server, so unassign explicitly.
            foreach (var annotationContext in correctionService.AnnotationContexts.ToList())
            {
                annotationContext.CorrectionServiceId = null;
            }

            _context.CorrectionServices.Remove(correctionService);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CorrectionServiceExists(long id)
        {
            return _context.CorrectionServices.Any(e => e.Id == id);
        }
    }
}







