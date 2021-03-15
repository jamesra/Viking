using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using IdentityServer.Data;
using IdentityServer.Models;

namespace IdentityServer.Controllers
{
    public class GrantedGroupPermissionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GrantedGroupPermissionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: GrantedGroupPermissions
        public async Task<IActionResult> Index(long? ResourceId)
        {
            var applicationDbContext = _context.GrantedGroupPermissions.Include(g => g.PermittedGroup).Include(g => g.Resource);

            if (ResourceId.HasValue)
                applicationDbContext.Where(g => g.ResourceId == ResourceId.Value);

            return View(await applicationDbContext.ToListAsync());
        }

        // GET: GrantedGroupPermissions/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var grantedGroupPermission = await _context.GrantedGroupPermissions
                .Include(g => g.PermittedGroup)
                .Include(g => g.Resource)
                .FirstOrDefaultAsync(m => m.ResourceId == id);
            if (grantedGroupPermission == null)
            {
                return NotFound();
            }

            return View(grantedGroupPermission);
        }

        // GET: GrantedGroupPermissions/Create
        public IActionResult Create(long ResourceId)
        {
            ViewData["GroupId"] = new SelectList(_context.Group, "Id", "Name");
            ViewData["ResourceId"] = new SelectList(_context.Group, "Id", "Name");
            return View();
        }

        // POST: GrantedGroupPermissions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ResourceId,PermissionId,GroupId")] GrantedGroupPermission grantedGroupPermission)
        {
            if (ModelState.IsValid)
            {
                _context.Add(grantedGroupPermission);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["GroupId"] = new SelectList(_context.Group, "Id", "Name", grantedGroupPermission.GroupId);
            ViewData["ResourceId"] = new SelectList(_context.Group, "Id", "Name", grantedGroupPermission.ResourceId);
            return View(grantedGroupPermission);
        }

        // GET: GrantedGroupPermissions/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var grantedGroupPermission = await _context.GrantedGroupPermissions.FindAsync(id);
            if (grantedGroupPermission == null)
            {
                return NotFound();
            }
            ViewData["GroupId"] = new SelectList(_context.Group, "Id", "Name", grantedGroupPermission.GroupId);
            ViewData["ResourceId"] = new SelectList(_context.Group, "Id", "Name", grantedGroupPermission.ResourceId);
            return View(grantedGroupPermission);
        }

        // POST: GrantedGroupPermissions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("ResourceId,PermissionId,GroupId")] GrantedGroupPermission grantedGroupPermission)
        {
            if (id != grantedGroupPermission.ResourceId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(grantedGroupPermission);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GrantedGroupPermissionExists(grantedGroupPermission.ResourceId))
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
            ViewData["GroupId"] = new SelectList(_context.Group, "Id", "Name", grantedGroupPermission.GroupId);
            ViewData["ResourceId"] = new SelectList(_context.Group, "Id", "Name", grantedGroupPermission.ResourceId);
            return View(grantedGroupPermission);
        }

        // GET: GrantedGroupPermissions/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var grantedGroupPermission = await _context.GrantedGroupPermissions
                .Include(g => g.PermittedGroup)
                .Include(g => g.Resource)
                .FirstOrDefaultAsync(m => m.ResourceId == id);
            if (grantedGroupPermission == null)
            {
                return NotFound();
            }

            return View(grantedGroupPermission);
        }

        // POST: GrantedGroupPermissions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var grantedGroupPermission = await _context.GrantedGroupPermissions.FindAsync(id);
            _context.GrantedGroupPermissions.Remove(grantedGroupPermission);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool GrantedGroupPermissionExists(long id)
        {
            return _context.GrantedGroupPermissions.Any(e => e.ResourceId == id);
        }
    }
}
