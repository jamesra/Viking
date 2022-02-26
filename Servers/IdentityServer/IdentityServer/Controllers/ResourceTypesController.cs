using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Controllers
{
    [Authorize]
    public class ResourceTypesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ResourceTypesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ResourceTypes
        public async Task<IActionResult> Index()
        {
            return View(await _context.ResourceTypes.ToListAsync());
        }

        // GET: ResourceTypes/List 
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var result = await _context.ResourceTypes
                                    .Include(r => r.Permissions)
                                    .Select(r => new
                                        {
                                            Id = r.Id,
                                            Permissions = r.Permissions.Select(p => p.PermissionId)
                                        }).ToListAsync();
            return Json(result);
        }

        // GET: ResourceTypes/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var resourceType = await _context.ResourceTypes
                .Include(m => m.Permissions)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (resourceType == null)
            {
                return NotFound();
            }

            return View(resourceType);
        }

        // GET: ResourceTypes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: ResourceTypes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Special.Roles.Admin)]
        public async Task<IActionResult> Create([Bind("Id")] ResourceType resourceType)
        {
            if (ModelState.IsValid)
            {
                _context.Add(resourceType);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(resourceType);
        }

        // GET: ResourceTypes/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var resourceType = await _context.ResourceTypes.FindAsync(id);
            if (resourceType == null)
            {
                return NotFound();
            }
            return View(resourceType);
        }

        // POST: ResourceTypes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Special.Roles.Admin)]
        public async Task<IActionResult> Edit(string id, [Bind("Id")] ResourceType resourceType)
        {
            if (id != resourceType.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(resourceType);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ResourceTypeExists(resourceType.Id))
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
            return View(resourceType);
        }

        // GET: ResourceTypes/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var resourceType = await _context.ResourceTypes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (resourceType == null)
            {
                return NotFound();
            }

            return View(resourceType);
        }

        // POST: ResourceTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Special.Roles.Admin)]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var resourceType = await _context.ResourceTypes.FindAsync(id);
            _context.ResourceTypes.Remove(resourceType);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ResourceTypeExists(string id)
        {
            return _context.ResourceTypes.Any(e => e.Id == id);
        }
    }
}
