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

        // GET: GrantedPermissions/BulkEdit (Volumes)
        public Task<IActionResult> BulkEdit(string volumeIds)
        {
            return BulkEditResources<Volume>(
                ParseIds(volumeIds),
                "Volumes",
                "Volumes",
                "bi bi-hdd-stack",
                nameof(BulkEdit));
        }

        // POST: GrantedPermissions/BulkEdit (Volumes)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> BulkEdit([Bind()] BulkPermissionsEditViewModel model)
        {
            return ApplyBulkPermissions<Volume>(
                model,
                "Volumes",
                "Volumes");
        }

        // GET: GrantedPermissions/BulkEditSegmentation
        public Task<IActionResult> BulkEditSegmentation(string segmentationServiceIds)
        {
            return BulkEditResources<SegmentationService>(
                ParseIds(segmentationServiceIds),
                "Segmentation Services",
                "SegmentationServices",
                "bi bi-diagram-3",
                nameof(BulkEditSegmentation));
        }

        // POST: GrantedPermissions/BulkEditSegmentation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> BulkEditSegmentation([Bind()] BulkPermissionsEditViewModel model)
        {
            return ApplyBulkPermissions<SegmentationService>(
                model,
                "Segmentation Services",
                "SegmentationServices");
        }

        private List<long> ParseIds(string ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return new List<long>();
            }

            return ids.Split(',')
                .Select(v => long.TryParse(v, out var id) ? id : (long?)null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
        }

        private async Task<IActionResult> BulkEditResources<TResource>(
            List<long> resourceIds,
            string resourceDisplayName,
            string returnController,
            string resourceIconClass,
            string formActionName)
            where TResource : Resource
        {
            if (!resourceIds.Any())
            {
                TempData["ErrorMessage"] = $"No {resourceDisplayName.ToLower()} selected for bulk editing.";
                return RedirectToAction("Index", returnController);
            }

            var resources = await _context.Set<TResource>()
                .Include(r => r.Parent)
                .Include(r => r.ResourceType)
                .Where(r => resourceIds.Contains(r.Id))
                .ToListAsync();

            if (!resources.Any())
            {
                TempData["ErrorMessage"] = $"Invalid {resourceDisplayName.ToLower()} provided.";
                return RedirectToAction("Index", returnController);
            }

            foreach (var resource in resources)
            {
                if (!await CanEditResourcePermissions(resource))
                {
                    TempData["ErrorMessage"] = $"You do not have permission to edit permissions for {resourceDisplayName.ToLower().TrimEnd('s')}: {resource.Name}";
                    return RedirectToAction("Index", returnController);
                }
            }

            var firstResource = resources.First();
            var commonPermissions = await _context.Permissions
                .Where(p => p.ResourceTypeId == firstResource.ResourceTypeId)
                .Select(p => p.PermissionId)
                .ToListAsync();

            var allUserPermissions = await _context.GrantedUserPermissions
                .Include(gup => gup.PermittedUser)
                .Where(gup => resourceIds.Contains(gup.ResourceId))
                .ToListAsync();

            var allGroupPermissions = await _context.GrantedGroupPermissions
                .Include(gup => gup.PermittedGroup)
                .Where(gup => resourceIds.Contains(gup.ResourceId))
                .ToListAsync();

            var userIds = allUserPermissions.Select(gup => gup.UserId).Distinct().ToList();
            var groupIds = allGroupPermissions.Select(gup => gup.GroupId).Distinct().ToList();

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
                    Selected = allUserPermissions.Any(gup => gup.UserId == u.Id && gup.ResourceId == resourceIds.First() && gup.PermissionId == permissionId)
                }).ToList()
            }).ToList();

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
                    Selected = allGroupPermissions.Any(gup => gup.GroupId == g.Id && gup.ResourceId == resourceIds.First() && gup.PermissionId == permissionId)
                }).ToList()
            }).ToList();

            var model = new BulkPermissionsEditViewModel
            {
                SelectedResourceIds = resourceIds,
                Resources = resources.Select(r => new BulkPermissionsEditViewModel.ResourceInfo
                {
                    Id = r.Id,
                    Name = r.Name,
                    OrganizationName = r.Parent?.Name ?? "No Organization"
                }).ToList(),
                ResourcePluralDisplayName = resourceDisplayName,
                ResourceSingularDisplayName = resourceDisplayName.TrimEnd('s'),
                ResourceIconClass = resourceIconClass,
                ReturnController = returnController,
                AvailablePermissions = commonPermissions,
                UserPermissions = users,
                GroupPermissions = groups
            };

            ViewBag.ResourceController = returnController;
            ViewBag.ResourceListTitle = resourceDisplayName;
            ViewBag.ResourceBreadcrumb = resourceDisplayName;
            ViewBag.FormAction = formActionName;

            return View("BulkEdit", model);
        }

        private async Task<IActionResult> ApplyBulkPermissions<TResource>(
            BulkPermissionsEditViewModel model,
            string resourceDisplayName,
            string returnController)
            where TResource : Resource
        {
            if (!model.SelectedResourceIds.Any())
            {
                TempData["ErrorMessage"] = $"No {resourceDisplayName.ToLower()} selected.";
                return RedirectToAction("Index", returnController);
            }

            var resources = await _context.Set<TResource>()
                .Include(r => r.UsersWithPermissions)
                .Include(r => r.GroupsWithPermissions)
                .Include(r => r.Parent)
                .Where(r => model.SelectedResourceIds.Contains(r.Id))
                .ToListAsync();

            foreach (var resource in resources)
            {
                if (!await CanEditResourcePermissions(resource))
                {
                    TempData["ErrorMessage"] = $"You do not have permission to edit permissions for {resourceDisplayName.ToLower().TrimEnd('s')}: {resource.Name}";
                    return RedirectToAction("Index", returnController);
                }
            }

            int successCount = 0;
            foreach (var resource in resources)
            {
                try
                {
                    resource.UpdateUsersPermissions(model.UserPermissions);
                    resource.UpdateGroupsPermissions(model.GroupPermissions);
                    successCount++;
                }
                catch
                {
                    // TODO: log error
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Permissions updated successfully for {successCount} {resourceDisplayName.ToLower()}.";
            return RedirectToAction("Index", returnController);
        }
    }
}
