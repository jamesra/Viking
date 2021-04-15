using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using IdentityServer.Data;
using IdentityServer.Models;
using IdentityServer.Models.UserViewModels;

namespace IdentityServer.Controllers
{
    public class ResourcesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ResourcesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult VerifyUniqueName(string Name, long? Id)
        {
            if (Id.HasValue)
            {
                if (_context.Resource.Where(r => r.Id != Id.Value).Any(r => r.Name == Name))
                    return Json($"A resource named {Name} already exists");
            }
            else
            {
                if (_context.Resource.Any(g => g.Name == Name))
                    return Json($"A resource named {Name} already exists");
            }
            return Json(true);
        }

        // GET: Resources
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Resource.Include(r => r.Parent).Include(r => r.ResourceType);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Resources/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var resource = await _context.Resource
                .FirstOrDefaultAsync(m => m.Id == id);

            if (resource == null)
            {
                return NotFound();
            }

            var actionResult = TryRedirectByResourceType("Details", resource.ResourceTypeId, id, () => View(resource));
            return actionResult;
        }

        [HttpGet]
        // GET: Resources/Create
        public IActionResult Create()
        {
            CreateResourceViewModel viewmodel = new CreateResourceViewModel()
            {
                Name = "",
                Description = "",
                ResourceTypeId = null
            };

            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name));
            ViewBag.AvailableResourceTypes = new SelectList(_context.ResourceTypes, nameof(ResourceType.Id), nameof(ResourceType.Id));
            return View(viewmodel);
        }

        [HttpGet]
        // GET: Resources/Create
        public IActionResult CreateChild(long ParentId)
        {
            CreateResourceViewModel viewmodel = new CreateResourceViewModel()
            {
                Name = "",
                Description = "",
                ResourceTypeId = null,
                ParentId = ParentId
            };

            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), viewmodel.ParentId.Value);
            ViewBag.AvailableResourceTypes = new SelectList(_context.ResourceTypes, nameof(ResourceType.Id), nameof(ResourceType.Id));
            return View(nameof(Create), viewmodel);
        }

        /// <summary>
        /// Derived classes of resources can call this method to preserve state if the users wishes to step back and update
        /// the resource type
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpGet]
        // GET: Resources/Create
        public IActionResult CreatePrevious([Bind("Id,Name,Description,ParentId,ResourceTypeId")] CreateResourceViewModel model)
        { 
            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), model.ParentId);
            ViewBag.AvailableResourceTypes = new SelectList(_context.ResourceTypes, nameof(ResourceType.Id), nameof(ResourceType.Id));
            return View(nameof(Create), model);
        }

        // POST: Resources/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Description,ParentId,ResourceTypeId")] CreateResourceViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = TryRedirectByResourceType("CreateContinue", model.ResourceTypeId, model, () => {

                    Resource obj = new Resource() { Name = model.Name, Description = model.Description, ParentID = model.ParentId, ResourceTypeId = model.ResourceTypeId };
                    _context.Resource.Add(obj);
                    _context.SaveChanges();
                    return View(obj);
                });

                return result;
            }

            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), model.ParentId);
            ViewBag.AvailableResourceTypes = new SelectList(_context.ResourceTypes, nameof(ResourceType.Id), nameof(ResourceType.Id));

            return View(model);
        }

        // GET: Resources/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var resource = await _context.Resource.FindAsync(id);
            if (resource == null)
            {
                return NotFound();
            }
            ViewData["ParentID"] = new SelectList(_context.Group, "Id", "Name", resource.ParentID);
            ViewData["ResourceTypeId"] = new SelectList(_context.ResourceTypes, "Id", "Id", resource.ResourceTypeId);
            return View(resource);
        }

        // POST: Resources/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Id,Name,Description,ParentID,ResourceTypeId")] Resource resource)
        {
            if (id != resource.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(resource);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ResourceExists(resource.Id))
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
            ViewData["ParentID"] = new SelectList(_context.Group, "Id", "Name", resource.ParentID);
            ViewData["ResourceTypeId"] = new SelectList(_context.ResourceTypes, "Id", "Id", resource.ResourceTypeId);
            return View(resource);
        }

        // GET: Resources/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var resource = await _context.Resource
                .Include(r => r.Parent)
                .Include(r => r.ResourceType)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (resource == null)
            {
                return NotFound();
            }

            return View(resource);
        }

        // POST: Resources/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var resource = await _context.Resource.FindAsync(id);
            _context.Resource.Remove(resource);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ResourceExists(long id)
        {
            return _context.Resource.Any(e => e.Id == id);
        }

        /// <summary>
        /// Returns a redirect action if it exists, returns null if the type is resource, throws not implemented otherwise.
        /// </summary>
        /// <param name="ActionName"></param>
        /// <param name="ResourceTypeId"></param>
        /// <param name="RouteValues"></param>
        /// <param name="IsResourceAction">The action to take if the ResourceTypeId == Resource (belongs in this controller)</param>
        /// <returns></returns>
        private IActionResult TryRedirectByResourceType(string ActionName, string ResourceTypeId, object RouteValues, Func<IActionResult> IsResourceAction = null)
        {
            switch (ResourceTypeId)
            {
                case nameof(Models.OrganizationalUnit):
                    return RedirectToAction("CreateContinue", "OrganizationalUnits", RouteValues);
                case nameof(Models.Volume):
                    return RedirectToAction("CreateContinue", "Volumes", RouteValues);
                case nameof(Models.Group):
                    return RedirectToAction("CreateContinue", "Groups", RouteValues);
                case nameof(Models.Resource):
                    if (IsResourceAction != null)
                        return IsResourceAction(); 
                    return null; 
                default:
                    throw new NotImplementedException($"Unknown resource type {ResourceTypeId}");
            }
        }
    }
}
