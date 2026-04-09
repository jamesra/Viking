using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    [Authorize]
    public class ResourceTypePermissionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ResourceTypePermissionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ResourceTypePermissions
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Permissions.Include(r => r.ResourceType);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: ResourceTypePermissions/Details/5
        public async Task<IActionResult> Details(string ResourceTypeId, string PermissionId)
        {
            if (ResourceTypeId == null || PermissionId == null)
            {
                return NotFound();
            }

            var resourceTypePermission = await _context.Permissions
                .Include(r => r.ResourceType)
                .FirstOrDefaultAsync(m => m.ResourceTypeId == ResourceTypeId && m.PermissionId == PermissionId);
            if (resourceTypePermission == null)
            {
                return NotFound();
            }

            //return View(resourceTypePermission);
            return RedirectToAction("Details", "ResourceTypes", resourceTypePermission.ResourceType);
            //return View("Details", resourceTypePermission.ResourceType);
        }

        /*
        // GET: ResourceTypePermissions/Create
        public IActionResult Create()
        {
            var selectList = new SelectList(_context.ResourceTypes, "Id", "Id");
            ViewData["ResourceTypeId"] = selectList;
            ViewBag.ResourceTypeId = selectList.First().Value;
            return View();
        }
        */

        public async Task<IActionResult> Create(string ResourceTypeId)
        {
            var rt = await _context.ResourceTypes.FirstOrDefaultAsync(rt => rt.Id == ResourceTypeId);
            //ViewData["ResourceTypeId"] = new SelectList(_context.ResourceTypes.Where(rt => rt.Id == ResourceTypeId), "Id", "Id");
            //ViewBag.ResourceTypeId = ResourceTypeId;
            return View(new ResourceTypePermission() { ResourceTypeId = ResourceTypeId, ResourceType = rt});
        }

        // POST: ResourceTypePermissions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Special.Roles.Admin)]
        public async Task<IActionResult> Create([Bind("ResourceTypeId,PermissionId,Description")] ResourceTypePermission resourceTypePermission)
        {
            if (ModelState.IsValid)
            {
                _context.Add(resourceTypePermission);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            //ViewData["ResourceTypeId"] = new SelectList(_context.ResourceTypes, "Id", "Id", resourceTypePermission.ResourceTypeId);
            //return View("Details", resourceTypePermission.ResourceType);
            return RedirectToAction("Details", "ResourceTypes", resourceTypePermission.ResourceType);
        }

        // GET: ResourceTypePermissions/Edit/5
        public async Task<IActionResult> Edit(string ResourceTypeId, string PermissionId)
        {
            if (ResourceTypeId == null || PermissionId == null)
            {
                return NotFound();
            } 

            var resourceTypePermission = await _context.Permissions
                .FirstOrDefaultAsync(m => m.ResourceTypeId == ResourceTypeId && m.PermissionId == PermissionId);
            if (resourceTypePermission == null)
            {
                return NotFound();
            }

            //ViewData["ResourceTypeId"] = new SelectList(_context.ResourceTypes, "Id", "Id", resourceTypePermission.ResourceTypeId);
            return View(resourceTypePermission);
        }

        // POST: ResourceTypePermissions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Special.Roles.Admin)]
        public async Task<IActionResult> Edit(string id, [Bind("ResourceTypeId,PermissionId,Description")] ResourceTypePermission resourceTypePermission)
        {
            if (id != resourceTypePermission.ResourceTypeId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(resourceTypePermission);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ResourceTypePermissionExists(resourceTypePermission.ResourceTypeId))
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
            ViewData["ResourceTypeId"] = new SelectList(_context.ResourceTypes, "Id", "Id", resourceTypePermission.ResourceTypeId);
            return RedirectToAction("Details", "ResourceTypes", resourceTypePermission.ResourceType);
            //return View(resourceTypePermission.ResourceType);
        }

        // GET: ResourceTypePermissions/Delete/5
        public async Task<IActionResult> Delete(string ResourceTypeId, string PermissionId)
        {
            if (ResourceTypeId == null || PermissionId == null)
            {
                return NotFound();
            }

            var resourceTypePermission = await _context.Permissions
                .Include(r => r.ResourceType)
                .FirstOrDefaultAsync(m => m.ResourceTypeId == ResourceTypeId && m.PermissionId == PermissionId);
            if (resourceTypePermission == null)
            {
                return NotFound();
            }

            return View(resourceTypePermission);
        }

        // POST: ResourceTypePermissions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Special.Roles.Admin)]
        public async Task<IActionResult> DeleteConfirmed([Bind("ResourceTypeId,PermissionId")] ResourceTypePermission key)
        {
            var resourceTypeId = key.ResourceTypeId;
            var resourceTypePermission = await _context.Permissions.FirstAsync(m => m.PermissionId == key.PermissionId && m.ResourceTypeId == key.ResourceTypeId);
            _context.Permissions.Remove(resourceTypePermission);
            await _context.SaveChangesAsync();
            //return RedirectToAction(nameof(Index));
            return RedirectToAction("Details", "ResourceTypes", await _context.ResourceTypes.FirstOrDefaultAsync(rt => rt.Id == resourceTypeId));
        }

        private bool ResourceTypePermissionExists(string id)
        {
            return _context.Permissions.Any(e => e.ResourceTypeId == id);
        }
    }
}
