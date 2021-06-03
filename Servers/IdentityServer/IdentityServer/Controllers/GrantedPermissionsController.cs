using IdentityServer.Authorization;
using IdentityServer.Data;
using IdentityServer.Extensions;
using IdentityServer.Models;
using IdentityServer.Models.UserViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServer.Controllers
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

            ResourcePermissionsEditGridViewModel model = new ResourcePermissionsEditGridViewModel
            {
                AvailablePermissions = applicationDbContext.AvailablePermissions.Select(p => p.PermissionId).ToList(),
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
                return RedirectToAction(nameof(ResourcesController.Details), "Groups", new { id = id });
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
                    .Include(r => r.UsersWithPermissions)
                    .Include(r => r.ResourceType).ThenInclude(r => r.Permissions)
                    .Include(r => r.GroupsWithPermissions).ThenInclude(gwp => gwp.PermittedGroup)
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
            return resource.ResourceTypeId switch
            {
                nameof(OrganizationalUnit) => (await _authorization.AuthorizeAsync(HttpContext.User, resource, Operations.OrgUnitAdmin)).Succeeded,
                nameof(Group) => (await _authorization.AuthorizeAsync(HttpContext.User, resource, Operations.GroupAccessManager)).Succeeded ||
                                 (await _authorization.AuthorizeAsync(HttpContext.User, resource.Parent, Operations.OrgUnitAdmin)).Succeeded,
                _ => (await _authorization.AuthorizeAsync(HttpContext.User, resource.Parent, Operations.OrgUnitAdmin)).Succeeded
            };
        }
    }
}
