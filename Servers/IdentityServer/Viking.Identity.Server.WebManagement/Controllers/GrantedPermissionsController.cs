using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Authorization;
using Viking.Identity.Server.WebManagement.Extensions;
using Viking.Identity.Server.WebManagement.Models.UserViewModels;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    [Authorize]
    public class GrantedPermissionsController : Controller
    {
        private readonly ApplicationDbContext _context; 
        private readonly IPermissionsViewModelHelper _permissionsHelper;
        private readonly IAuthorizationService _authorization;

        public GrantedPermissionsController(ApplicationDbContext context, IAuthorizationService authorization, IPermissionsViewModelHelper permissionsHelper)
        {
            _context = context;
            _authorization = authorization;
            _permissionsHelper = permissionsHelper;
        }

        // GET: GrantedUserPermissions
        public async Task<IActionResult> Index(long id)
        {
            var applicationDbContext = await GetPermittedForResource(id);

            if(applicationDbContext == null)
            {
                return NotFound("Resource not found");
            }

            if (false == await CanEditResourcePermissions(id))
            {
                return Unauthorized();
            }

            /*
            var authResult = await _authorization.AuthorizeAsync(HttpContext.User, id, IdentityServer.Authorization.Operations.O);
            if (authResult.Succeeded == false)
            {
                return Unauthorized();
            }
            */

            ResourcePermissionsEditGridViewModel model = new ResourcePermissionsEditGridViewModel
            {
                AvailablePermissions = applicationDbContext.AvailablePermissions.Select(p => p.PermissionId).ToList(),
                UserPermissions = _permissionsHelper.ResourcePermissionsByUser(applicationDbContext),
                GroupPermissions = _permissionsHelper.ResourcePermissionsByGroup(applicationDbContext)
            };

            return View(model);
        }

        // GET: GrantedUserPermissions/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            if (false == await CanEditResourcePermissions(id.Value))
            {
                return Unauthorized();
            }

            var grantedUserPermission = await _context.GrantedUserPermissions
                .Include(g => g.PermittedUser)
                .Include(g => g.Resource)
                .FirstOrDefaultAsync(m => m.ResourceId == id);
            if (grantedUserPermission == null)
            {
                return NotFound();
            }

            return View(grantedUserPermission);
        }

        // GET: GrantedUserPermissions/Create
        public async Task<IActionResult> Create(long ?ResourceId)
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
                Users = _context.Users.Select(u => new NamedItemSelectedViewModel<string>() { Id = u.Id, Name = u.UserName, Selected = false }).ToList(),
                //Groups = _context.Group.Select(g => new ItemSelectedViewModel<long>() { Id = g.Id, Name = g.Name, Selected = false }).ToList()
            };

            return View(viewData);
        }

        // POST: GrantedUserPermissions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(new string[] { nameof(CreateGrantedResourcePermissionViewModel.Resource),
                                                                     nameof(CreateGrantedResourcePermissionViewModel.Users), 
                                                                     nameof(CreateGrantedResourcePermissionViewModel.Permissions)})] CreateGrantedResourcePermissionViewModel grantedPermissions)
        {
             
            if (ModelState.IsValid)
            {
                var resource = await _context.Resource
                                             .Include(r => r.UsersWithPermissions)
                                             .Include(r => r.GroupsWithPermissions)
                                             .Include(r => r.Parent)
                                             .FirstAsync(r => r.Id == grantedPermissions.Resource.Id);
                if(resource == null)
                {
                    return NotFound();
                }

                if (false == await CanEditResourcePermissions(resource))
                {
                    return Unauthorized();
                }

                resource.AddGrantedUserPermissions(grantedPermissions.Permissions, grantedPermissions.Users);
                //resource.AddGrantedGroupPermissions(grantedPermissions.Permissions, grantedPermissions.Groups);

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), grantedPermissions.Resource.Id);
            }

            //ViewData["ResourceId"] = new SelectList(_context.Group, "Id", "Name", grantedUserPermission.ResourceId);
            return View(grantedPermissions.Resource); 
        }

        // GET: GrantedUserPermissions/Edit/5
        public async Task<IActionResult> Edit(long id)
        {
            var applicationDbContext = await GetPermittedForResource(id);

            if (applicationDbContext == null)
            {
                return NotFound("Resource not found");
            }

            if (false == await CanEditResourcePermissions(applicationDbContext))
            {
                return Unauthorized();
            }

            // Ensure ResourceType is loaded
            if (applicationDbContext.ResourceType == null)
            {
                await _context.Entry(applicationDbContext)
                    .Reference(r => r.ResourceType)
                    .LoadAsync();
                
                if (applicationDbContext.ResourceType != null)
                {
                    await _context.Entry(applicationDbContext.ResourceType)
                        .Collection(rt => rt.Permissions)
                        .LoadAsync();
                }
            }

            var availablePerms = applicationDbContext.AvailablePermissions?.Select(p => p.PermissionId).ToList() ?? new List<string>();
            
            ResourcePermissionsEditGridViewModel model = new ResourcePermissionsEditGridViewModel
            {
                AvailablePermissions = availablePerms,
                UserPermissions = _permissionsHelper.ResourcePermissionsByUser(applicationDbContext),
                GroupPermissions = _permissionsHelper.ResourcePermissionsByGroup(applicationDbContext)
            };

            return View(model);
        }

        // POST: GrantedUserPermissions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind()] ResourcePermissionsEditGridViewModel grantedPermissions)
        { 
            var resource = await _context.Resource
                .Include(r => r.UsersWithPermissions)
                .Include(r => r.GroupsWithPermissions)
                .Include(r => r.Parent)
                .FirstOrDefaultAsync(r => r.Id == id);

            if(resource == null)
            {
                return NotFound();
            }

            if (false == await CanEditResourcePermissions(resource))
            {
                return Unauthorized();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    resource.UpdateUsersPermissions(grantedPermissions.UserPermissions);
                    resource.UpdateGroupsPermissions(grantedPermissions.GroupPermissions);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GrantedUserPermissionExists(id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                TempData["SuccessMessage"] = "Update submitted";
                return RedirectToAction(nameof(Edit), new { id = id });
            }
//            ViewData["ResourceId"] = new SelectList(_context.Group, "Id", "Name", grantedUserPermission.ResourceId);
            return View(grantedPermissions);
        }

        /*
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
        */

        private Task<Resource> GetPermittedForResource(long ResourceId)
        {
            var applicationDbContext = _context.Resource
                    .Include(r => r.Parent)
                    .Include(r => r.ResourceType)
                        .ThenInclude(rt => rt.Permissions)
                    .Include(r => r.UsersWithPermissions)
                        .ThenInclude(uwp => uwp.PermittedUser)
                    .Include(r => r.GroupsWithPermissions)
                        .ThenInclude(gwp => gwp.PermittedGroup)
                    .FirstOrDefaultAsync(r => r.Id == ResourceId);

            return applicationDbContext;
        } 

        private bool GrantedUserPermissionExists(long id)
        {
            return _context.GrantedUserPermissions.Any(e => e.ResourceId == id);
        }

        private async Task<bool> CanEditResourcePermissions(long id)
        {
            var resource = await _context.Resource.Include(r => r.Parent).FirstAsync(r => r.Id == id);
            return await CanEditResourcePermissions(resource);
        }

        private async Task<bool> CanEditResourcePermissions(Resource resource)
        {
            // Site administrators can always edit resource permissions
            if (User.IsInRole(Special.Roles.Admin))
            {
                return true;
            }

            return resource.ResourceTypeId switch
            {
                nameof(OrganizationalUnit) => (await _authorization.AuthorizeAsync(HttpContext.User, resource, Operations.OrgUnitAdmin)).Succeeded,
                nameof(Group) => (await _authorization.AuthorizeAsync(HttpContext.User, resource, Operations.GroupAccessManager)).Succeeded ||
                                 (await _authorization.AuthorizeAsync(HttpContext.User, resource.Parent, Operations.OrgUnitAdmin)).Succeeded,
                _ => (await _authorization.AuthorizeAsync(HttpContext.User, resource.Parent, Operations.OrgUnitAdmin)).Succeeded
            };
        }

        // GET: GrantedPermissions/BulkEdit
        public async Task<IActionResult> BulkEdit(string volumeIds)
        {
            if (string.IsNullOrEmpty(volumeIds))
            {
                TempData["ErrorMessage"] = "No volumes selected for bulk editing.";
                return RedirectToAction("Index", "Volumes");
            }

            var volumeIdList = volumeIds.Split(',')
                .Select(v => long.TryParse(v, out var id) ? id : (long?)null)
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .ToList();

            if (!volumeIdList.Any())
            {
                TempData["ErrorMessage"] = "Invalid volume IDs provided.";
                return RedirectToAction("Index", "Volumes");
            }

            // Get volumes and check permissions
            var volumes = await _context.Volume
                .Include(v => v.Parent)
                .Include(v => v.ResourceType)
                .Where(v => volumeIdList.Contains(v.Id))
                .ToListAsync();

            // Check that user can edit all selected volumes
            foreach (var volume in volumes)
            {
                if (!await CanEditResourcePermissions(volume))
                {
                    TempData["ErrorMessage"] = $"You do not have permission to edit permissions for volume: {volume.Name}";
                    return RedirectToAction("Index", "Volumes");
                }
            }

            // Get common permissions across all volumes (they should all be the same ResourceType)
            var firstVolume = volumes.First();
            var commonPermissions = await _context.Permissions
                .Where(p => p.ResourceTypeId == firstVolume.ResourceTypeId)
                .Select(p => p.PermissionId)
                .ToListAsync();

            // Get union of all users/groups that have permissions on any of these volumes
            var allUserPermissions = await _context.GrantedUserPermissions
                .Include(gup => gup.PermittedUser)
                .Where(gup => volumeIdList.Contains(gup.ResourceId))
                .ToListAsync();

            var allGroupPermissions = await _context.GrantedGroupPermissions
                .Include(gup => gup.PermittedGroup)
                .Where(gup => volumeIdList.Contains(gup.ResourceId))
                .ToListAsync();

            // Build view model - show all users/groups that appear in any volume
            var userIds = allUserPermissions.Select(gup => gup.UserId).Distinct().ToList();
            var groupIds = allGroupPermissions.Select(gup => gup.GroupId).Distinct().ToList();

            // Materialize users first, then build permissions in memory
            var usersList = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName })
                .ToListAsync();

            var users = usersList.Select(u => new UserResourcePermissionsViewModel
            {
                GranteeId = u.Id,
                Name = u.UserName,
                Permissions = commonPermissions.Select(permissionId => new ItemSelectedViewModel<string>
                {
                    Id = permissionId,
                    Selected = allUserPermissions.Any(gup => gup.UserId == u.Id && gup.ResourceId == volumeIdList.First() && gup.PermissionId == permissionId)
                }).ToList()
            }).ToList();

            // Materialize groups first, then build permissions in memory
            var groupsList = await _context.Group
                .Where(g => groupIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name })
                .ToListAsync();

            var groups = groupsList.Select(g => new GroupResourcePermissionsViewModel
            {
                GranteeId = g.Id,
                Name = g.Name,
                Permissions = commonPermissions.Select(permissionId => new ItemSelectedViewModel<string>
                {
                    Id = permissionId,
                    Selected = allGroupPermissions.Any(gup => gup.GroupId == g.Id && gup.ResourceId == volumeIdList.First() && gup.PermissionId == permissionId)
                }).ToList()
            }).ToList();

            var model = new BulkPermissionsEditViewModel
            {
                SelectedVolumeIds = volumeIdList,
                Volumes = volumes.Select(v => new BulkPermissionsEditViewModel.VolumeInfo
                {
                    Id = v.Id,
                    Name = v.Name,
                    OrganizationName = v.Parent?.Name ?? "No Organization"
                }).ToList(),
                AvailablePermissions = commonPermissions,
                UserPermissions = users,
                GroupPermissions = groups
            };

            return View(model);
        }

        // POST: GrantedPermissions/BulkEdit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkEdit([Bind()] BulkPermissionsEditViewModel model)
        {
            if (!model.SelectedVolumeIds.Any())
            {
                TempData["ErrorMessage"] = "No volumes selected.";
                return RedirectToAction("Index", "Volumes");
            }

            // Verify volumes exist and user has permission
            var volumes = await _context.Volume
                .Include(v => v.UsersWithPermissions)
                .Include(v => v.GroupsWithPermissions)
                .Include(v => v.Parent)
                .Where(v => model.SelectedVolumeIds.Contains(v.Id))
                .ToListAsync();

            foreach (var volume in volumes)
            {
                if (!await CanEditResourcePermissions(volume))
                {
                    TempData["ErrorMessage"] = $"You do not have permission to edit permissions for volume: {volume.Name}";
                    return RedirectToAction("Index", "Volumes");
                }
            }

            // Apply permissions to all selected volumes
            int successCount = 0;
            foreach (var volume in volumes)
            {
                try
                {
                    volume.UpdateUsersPermissions(model.UserPermissions);
                    volume.UpdateGroupsPermissions(model.GroupPermissions);
                    successCount++;
                }
                catch
                {
                    // Log error but continue with other volumes
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Permissions updated successfully for {successCount} volume(s).";
            return RedirectToAction("Index", "Volumes");
        }
    }
}
