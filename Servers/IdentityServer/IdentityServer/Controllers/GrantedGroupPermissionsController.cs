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
            var applicationDbContext = GetPermittedGroupsForGroup(ResourceId);

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

        // GET: GrantedUserPermissions/Create
        public async Task<IActionResult> Create(long? ResourceId)
        {
            if (ResourceId == null || ResourceId.HasValue == false)
            {
                return NotFound();
            }

            var resource = await _context.Resource.Include(r => r.UsersWithPermissions).Include(r => r.GroupsWithPermissions).FirstAsync(r => r.Id == ResourceId.Value);
            if (resource == null)
            {
                return NotFound();
            }

            var viewData = new CreateGrantedResourcePermissionViewModel()
            {
                Resource = resource,
                Permissions = _context.Permissions.Where(p => p.ResourceTypeId == resource.ResourceTypeId).Select(p => new NamedItemSelectedViewModel<string>() { Id = p.PermissionId, Name = p.PermissionId, Selected = false }).ToList(),
                //Users = _context.Users.Select(u => new ItemSelectedViewModel<string>() { Id = u.Id, Name = u.UserName, Selected = false }).ToList(),
                Groups = _context.Group.Select(g => new NamedItemSelectedViewModel<long>() { Id = g.Id, Name = g.Name, Selected = false }).ToList()
            };

            return View(viewData);
        }

        // POST: GrantedUserPermissions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(new string[] { nameof(CreateGrantedResourcePermissionViewModel.Resource),
                                                                     nameof(CreateGrantedResourcePermissionViewModel.Groups),
                                                                     nameof(CreateGrantedResourcePermissionViewModel.Permissions)})] CreateGrantedResourcePermissionViewModel grantedPermissions)
        {

            if (ModelState.IsValid)
            {
                var resource = await _context.Resource.Include(r => r.UsersWithPermissions).Include(r => r.GroupsWithPermissions).FirstAsync(r => r.Id == grantedPermissions.Resource.Id);
                if (resource == null)
                {
                    return NotFound();
                }

                //resource.AddGrantedUserPermissions(grantedPermissions.Permissions, grantedPermissions.Users);
                resource.AddGrantedGroupPermissions(grantedPermissions.Permissions, grantedPermissions.Groups);

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), grantedPermissions.Resource.Id);
            }

            //ViewData["ResourceId"] = new SelectList(_context.Group, "Id", "Name", grantedUserPermission.ResourceId);
            return View(grantedPermissions.Resource);
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

        private IQueryable<GrantedGroupPermission> GetPermittedGroupsForGroup(long? ResourceId)
        {
            IQueryable<GrantedGroupPermission> applicationDbContext;
            if (ResourceId.HasValue)
                applicationDbContext = _context.GrantedGroupPermissions.Include(g => g.Resource).Include(g => g.PermittedGroup).Where(gup => gup.ResourceId == ResourceId);
            else
                applicationDbContext = _context.GrantedGroupPermissions.Include(g => g.Resource).Include(g => g.PermittedGroup);

            return applicationDbContext;
        }

        private bool GrantedGroupPermissionExists(long id)
        {
            return _context.GrantedGroupPermissions.Any(e => e.ResourceId == id);
        }
    }
}
