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
    public class GrantedUserPermissionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GrantedUserPermissionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: GrantedUserPermissions
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.GrantedUserPermissions.Include(g => g.Resource);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: GrantedUserPermissions/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var grantedUserPermission = await _context.GrantedUserPermissions
                .Include(g => g.Resource)
                .FirstOrDefaultAsync(m => m.ResourceId == id);
            if (grantedUserPermission == null)
            {
                return NotFound();
            }

            return View(grantedUserPermission);
        }

        // GET: GrantedUserPermissions/Create
        public IActionResult Create()
        {
            ViewData["ResourceId"] = new SelectList(_context.Group, "Id", "Name");
            return View();
        }

        // POST: GrantedUserPermissions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ResourceId,PermissionId,UserId")] GrantedUserPermission grantedUserPermission)
        {
            if (ModelState.IsValid)
            {
                _context.Add(grantedUserPermission);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ResourceId"] = new SelectList(_context.Group, "Id", "Name", grantedUserPermission.ResourceId);
            return View(grantedUserPermission);
        }

        // GET: GrantedUserPermissions/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var grantedUserPermission = await _context.GrantedUserPermissions.FindAsync(id);
            if (grantedUserPermission == null)
            {
                return NotFound();
            }
            ViewData["ResourceId"] = new SelectList(_context.Group, "Id", "Name", grantedUserPermission.ResourceId);
            return View(grantedUserPermission);
        }

        // POST: GrantedUserPermissions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("ResourceId,PermissionId,UserId")] GrantedUserPermission grantedUserPermission)
        {
            if (id != grantedUserPermission.ResourceId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(grantedUserPermission);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GrantedUserPermissionExists(grantedUserPermission.ResourceId))
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
            ViewData["ResourceId"] = new SelectList(_context.Group, "Id", "Name", grantedUserPermission.ResourceId);
            return View(grantedUserPermission);
        }

        // GET: GrantedUserPermissions/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var grantedUserPermission = await _context.GrantedUserPermissions
                .Include(g => g.Resource)
                .FirstOrDefaultAsync(m => m.ResourceId == id);
            if (grantedUserPermission == null)
            {
                return NotFound();
            }

            return View(grantedUserPermission);
        }

        // POST: GrantedUserPermissions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var grantedUserPermission = await _context.GrantedUserPermissions.FindAsync(id);
            _context.GrantedUserPermissions.Remove(grantedUserPermission);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool GrantedUserPermissionExists(long id)
        {
            return _context.GrantedUserPermissions.Any(e => e.ResourceId == id);
        }
    }
}
