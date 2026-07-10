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
using Viking.Identity.Server.Extensions.Services;
using Viking.Identity.Server.WebManagement.Helpers;
using Viking.Identity.Server.WebManagement.Models.UserViewModels;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    [Authorize]
    public class OrganizationalUnitsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorization;
        private readonly ResourceProvisioningService _provisioning;

        public OrganizationalUnitsController(
            ApplicationDbContext context,
            IAuthorizationService authorization,
            ResourceProvisioningService provisioning)
        {
            _authorization = authorization;
            _context = context;
            _provisioning = provisioning;
        }

        // GET: OrganizationalUnits
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.OrgUnit.Include(o => o.Parent).Include(o => o.ResourceType);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: OrganizationalUnits/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var organizationalUnit = await _context.OrgUnit
                .Include(o => o.Parent)
                .Include(o => o.ResourceType)
                .Include(o => o.Children)
                    .ThenInclude(c => c.ResourceType)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (organizationalUnit == null)
            {
                return NotFound();
            }

            // Load volumes in this org
            var volumes = await _context.Volume
                .Where(v => v.ParentID == id)
                .Include(v => v.Parent)
                .Include(v => v.UsersWithPermissions)
                .Include(v => v.GroupsWithPermissions)
                .ToListAsync();

            // Load segmentation services in this org
            var segmentationServices = await _context.SegmentationServices
                .Where(s => s.ParentID == id)
                .Include(s => s.Parent)
                .Include(s => s.UsersWithPermissions)
                .Include(s => s.GroupsWithPermissions)
                .ToListAsync();

            // Load child orgs
            var childOrgs = organizationalUnit.Children
                .OfType<OrganizationalUnit>()
                .ToList();

            // Load groups for this org
            var groups = await _context.Group
                .Where(g => g.ParentID == id)
                .Include(g => g.Parent)
                .Include(g => g.MemberUsers)
                .Include(g => g.MemberGroups)
                .ToListAsync();

            ViewData["Volumes"] = volumes;
            ViewData["SegmentationServices"] = segmentationServices;
            ViewData["ChildOrganizations"] = childOrgs;
            ViewData["Groups"] = groups;

            return View(organizationalUnit);
        }

        // GET: OrganizationalUnits/Create
        public IActionResult Create(long? parentOrgId = null)
        {
            CreateOrgUnitViewModel viewmodel = new CreateOrgUnitViewModel();
            if (parentOrgId.HasValue && parentOrgId.Value > 0)
            {
                viewmodel.ParentId = parentOrgId.Value;
            }

            ViewBag.AvailableParents = OrgUnitSelectListHelper.AvailableParents(_context, viewmodel.ParentId);
            return View(viewmodel);
        }

        [HttpGet]
        public IActionResult CreateContinue([Bind("Id,Name,Description,ParentId")] CreateResourceViewModel model)
        {
            //Continues creation after user selects a resource type
            model.ResourceTypeId = nameof(OrganizationalUnit);
            ViewBag.AvailableParents = OrgUnitSelectListHelper.AvailableParents(_context, model.ParentId);
            return View(nameof(Create), new CreateOrgUnitViewModel(model));
        }

        // POST: OrganizationalUnits/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,ParentId")] CreateOrgUnitViewModel model)
        {
            if (ModelState.IsValid)
            {
                var authProbe = new OrganizationalUnit
                {
                    Name = model.Name,
                    Description = model.Description,
                    ResourceTypeId = nameof(OrganizationalUnit),
                    ParentID = model.ParentId == 0 ? null : model.ParentId
                };

                if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, authProbe))
                {
                    return Unauthorized();
                }

                var ou = await _provisioning.CreateOrganizationalUnitAsync(model.Name, model.Description, model.ParentId);
                await _provisioning.GrantSiteAdminsOrgUnitAdminAsync(ou.Id);

                // Redirect to permissions management UI for fine-tuning
                return RedirectToAction(nameof(GrantedPermissionsController.Index), "GrantedPermissions", new { id = ou.Id });
            }

            ViewBag.AvailableParents = OrgUnitSelectListHelper.AvailableParents(_context, model.ParentId);
            return View(model);
        }

        // GET: OrganizationalUnits/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var organizationalUnit = await _context.OrgUnit.FindAsync(id);
            if (organizationalUnit == null)
            {
                return NotFound();
            }

            ViewBag.ParentID = new SelectList(_context.OrgUnit.Where(ou => ou.Id != organizationalUnit.Id), "Id", "Name", organizationalUnit.ParentID);
            ViewBag.AvailableParents = new SelectList(await GetAvailableParents(id.Value), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), organizationalUnit.Id);
            return View(organizationalUnit);
        }

        // POST: OrganizationalUnits/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Id,Name,Description,ParentID,ResourceTypeId")] OrganizationalUnit organizationalUnit)
        {
            if (id != organizationalUnit.Id)
            {
                return NotFound();
            }
              
            if (ModelState.IsValid)
            {
                if (false == await _authorization.IsOrgUnitAdminAsync(HttpContext.User, organizationalUnit))
                {
                    return Unauthorized();
                }

                try
                {
                    _context.Update(organizationalUnit);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OrganizationalUnitExists(organizationalUnit.Id))
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
            ViewBag.ParentID = new SelectList(_context.OrgUnit.Where(ou => ou.Id != organizationalUnit.Id), "Id", "Name", organizationalUnit.ParentID);
            return View(organizationalUnit);
        }

        // GET: OrganizationalUnits/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var organizationalUnit = await _context.OrgUnit
                .Include(o => o.Parent)
                .Include(o => o.ResourceType)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (organizationalUnit == null)
            {
                return NotFound();
            }

            return View(organizationalUnit);
        }

        // POST: OrganizationalUnits/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {   
            var organizationalUnit = await _context.OrgUnit.FindAsync(id);
            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, organizationalUnit))
            {
                return Unauthorized();
            }

            _context.OrgUnit.Remove(organizationalUnit);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool OrganizationalUnitExists(long id)
        {
            return _context.OrgUnit.Any(e => e.Id == id);
        }
         

        private async Task<IEnumerable<OrganizationalUnit>> GetAvailableParents(long Id)
        {
            var children = (await _context.RecursiveChildrenOfOrg(Id)).Select(ou => ou.Id);

            return await _context.OrgUnit.Where(ou => ou.Id != Id && children.Contains(ou.Id) == false).ToListAsync();
        }
    }
}
