using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using IdentityServer.Data;
using IdentityServer.Models;
using IdentityServer.Models.UserViewModels;

namespace IdentityServer.Controllers
{
    public class ResourceTypePermissionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ResourceTypePermissionsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: GroupPermissions
        public async Task<IActionResult> Index(string ResourceTypeId)
        {
            IEnumerable<ResourceTypePermission> permissions;

            if (ResourceTypeId != null)
                permissions = _context.Permissions
                                               .Where(g => g.ResourceTypeId == ResourceTypeId);
            else
                permissions = _context.Permissions;

            //GroupPermissionsViewModel viewModel = new GroupPermissionsViewModel() { Group = _context.Group.Find(GroupId.Value), AvailablePermissions = await applicationDbContext.ToListAsync() };

            return View(permissions);
        }


        // GET: GroupPermissions/Create
        /// <summary>
        /// 
        /// </summary>
        /// <param name="GroupId">The group ID the form will select by default</param>
        /// <returns></returns>
        [Authorize(Roles = Config.AdminRoleName)]
        public async Task<IActionResult> Create(string ResourceTypeId)
        {
            var userId = _userManager.GetUserId(User);
            var eligibleResourceTypes = await _context.ResourceTypes.ToListAsync(); //.Include(g => g.GroupAssignments).Where(g => g.GroupAssignments.Any(ga => ga.UserId == userId)).ToListAsync();
            var selectGroup = new SelectList(eligibleResourceTypes, "Id", "Name", ResourceTypeId);

            return View();
        }

        // POST: GroupPermissions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ResourceTypeId,PermissionId,Description")] ResourceTypePermission permission)
        {
            if (ModelState.IsValid)
            {
                _context.Add(permission);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { ResourceTypeId = permission.ResourceTypeId });
            }

            return View();
        }

        // GET: GroupPermissions/Edit/5
        public async Task<IActionResult> Edit(ResourceTypePermission permission)
        {
            if (permission == null)
            {
                return NotFound();
            }

            var groupPermission = await _context.Permissions.FindAsync(permission);
            if (groupPermission == null)
            {
                return NotFound();
            }
            ViewData["GroupId"] = new SelectList(_context.ResourceTypes, "Id", "Name", permission.ResourceTypeId);
            return View(groupPermission);
        }

        // POST: GroupPermissions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string resourceTypeId, [Bind("ResourceTypeId,PermissionId,Description")] ResourceTypePermission resourcePermission)
        {
            if (resourceTypeId != resourcePermission.ResourceTypeId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(resourcePermission);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ResourceTypePermissionExists(resourcePermission.ResourceTypeId, resourcePermission.PermissionId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index), new { ResourceTypeId = resourcePermission.ResourceTypeId });
            }
            ViewData["ResourceTypeId"] = new SelectList(_context.ResourceTypes, "Id", "Name", resourcePermission.ResourceTypeId);
            return View(resourcePermission);
        }

        // GET: GroupPermissions/Delete/5
        public async Task<IActionResult> Delete(ResourceTypePermission permission)
        {
            if (permission == null)
            {
                return NotFound();
            }

            var groupPermission = await _context.Permissions 
                .FirstOrDefaultAsync(m => m.PermissionId == permission.PermissionId && m.ResourceTypeId == permission.ResourceTypeId);
            if (groupPermission == null)
            {
                return NotFound();
            }

            return View();
        }

        // POST: GroupPermissions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(ResourceTypePermission permission)
        {
            var groupPermission = await _context.Permissions 
                .FirstOrDefaultAsync(m => m.ResourceTypeId == permission.ResourceTypeId && m.PermissionId == permission.PermissionId);
            _context.Permissions.Remove(groupPermission);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { ResourceTypeId = permission.ResourceTypeId });
        }

        private bool ResourceTypePermissionExists(string ResourceTypeId, string PermissionId)
        {
            return _context.Permissions.Any(e => e.ResourceTypeId == ResourceTypeId && e.PermissionId == PermissionId);
        }
    }
}
