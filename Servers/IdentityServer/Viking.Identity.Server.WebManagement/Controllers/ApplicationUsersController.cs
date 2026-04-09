using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Authorization;
using Viking.Identity.Server.WebManagement.Models.UserViewModels;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    [Route("[controller]/[action]")]
    public class ApplicationUsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public ApplicationUsersController(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        // GET: ApplicationUsers
        public async Task<IActionResult> Index()
        {
            return View(await _context.ApplicationUser.Include("GroupAssignments").ToListAsync());
        }

        public ActionResult ReturnChallengeOrForbidOnFailedAuthorization()
        {
            return User.Identity.IsAuthenticated ? new ForbidResult() : (ActionResult)Challenge();
        }

        // GET: ApplicationUsers/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var applicationUser = await _context.ApplicationUser
                .Include(u => u.GroupAssignments)
                    .ThenInclude(ga => ga.Group)
                .Include(u => u.PermissionsHeld)
                    .ThenInclude(p => p.Resource)
                .SingleOrDefaultAsync(m => m.Id == id);
            
            if (applicationUser == null)
            {
                return NotFound();
            }

            // Get user's groups (recursive)
            var recursiveGroups = await _context.RecursiveMemberOfGroups(id);
            
            // Get volumes user has access to
            var userVolumeIds = await _context.GrantedUserPermissions
                .Where(gup => gup.UserId == id)
                .Select(gup => gup.ResourceId)
                .Distinct()
                .ToListAsync();

            var volumes = await _context.Volume
                .Where(v => userVolumeIds.Contains(v.Id))
                .Include(v => v.Parent)
                .Include(v => v.UsersWithPermissions.Where(gup => gup.UserId == id))
                .ToListAsync();

            ViewBag.RecursiveGroups = recursiveGroups;
            ViewBag.UserGroups = applicationUser.GroupAssignments?.Select(ga => ga.Group).ToList() ?? new List<Group>();
            ViewBag.Volumes = volumes;

            return View(applicationUser);
        }

        // GET: ApplicationUsers/Create
        public IActionResult Create()
        {
            return RedirectToAction("Register", "Account");
        }

        // POST: ApplicationUsers/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        //public async Task<IActionResult> Create([Bind("Id,UserName,NormalizedUserName,Email,NormalizedEmail,EmailConfirmed,PasswordHash,SecurityStamp,ConcurrencyStamp,PhoneNumber,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnd,LockoutEnabled,AccessFailedCount")] ApplicationUser applicationUser)
        public async Task<IActionResult> Create([Bind("Id,UserName,NormalizedUserName,Email,NormalizedEmail,EmailConfirmed,PhoneNumber,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnd,LockoutEnabled,AccessFailedCount")] ApplicationUser applicationUser)
        {
            if (ModelState.IsValid)
            {
                _context.Add(applicationUser);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            return View(applicationUser);
        }

        // GET: ApplicationUsers/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var applicationUser = await _context.ApplicationUser.Include("GroupAssignments.Group").SingleOrDefaultAsync(m => m.Id == id);
            if (applicationUser == null)
            {
                return NotFound();
            }
            return View(applicationUser);
        }

        private bool IsUserAnAdminOrSelf(string UserId)
        {
            if (!this.User.IsInRole(Special.Roles.Admin))
            {
                var originalUsername = _context.ApplicationUser.Where(u => u.Id == UserId).Select(u => u.Email).FirstOrDefault();
                if (!(this.User.Identity.Name == originalUsername))
                {
                    return false;
                }
            }

            return true;
        }

        // POST: ApplicationUsers/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Id,FamilyName,GivenName,UserName,NormalizedUserName,Email,NormalizedEmail,EmailConfirmed,PasswordHash,SecurityStamp,ConcurrencyStamp,PhoneNumber,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnd,LockoutEnabled,AccessFailedCount")] ApplicationUser applicationUser)
        {
            //Ensure the user is in the Access manager role or the owner of the account
            if(!this.User.Identity.IsAuthenticated)
            {
                return Unauthorized();
            }
              
            if (id != applicationUser.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                //Ensure that only admins can edit, and that users can edit their own page
                if(!IsUserAnAdminOrSelf(id))
                {
                    return Unauthorized();
                }

                // Always reload the user from database to get the latest concurrency stamp
                var currentUser = await _context.ApplicationUser
                    .Include("GroupAssignments.Group")
                    .SingleOrDefaultAsync(u => u.Id == applicationUser.Id);
                
                if (currentUser == null)
                {
                    return NotFound();
                }
                
                // Update the current user's properties with the new values from the form
                currentUser.FamilyName = applicationUser.FamilyName;
                currentUser.GivenName = applicationUser.GivenName;
                currentUser.UserName = applicationUser.UserName;
                currentUser.NormalizedUserName = applicationUser.NormalizedUserName;
                currentUser.Email = applicationUser.Email;
                currentUser.NormalizedEmail = applicationUser.NormalizedEmail;
                currentUser.EmailConfirmed = applicationUser.EmailConfirmed;
                currentUser.PhoneNumber = applicationUser.PhoneNumber;
                currentUser.PhoneNumberConfirmed = applicationUser.PhoneNumberConfirmed;
                currentUser.TwoFactorEnabled = applicationUser.TwoFactorEnabled;
                currentUser.LockoutEnd = applicationUser.LockoutEnd;
                currentUser.LockoutEnabled = applicationUser.LockoutEnabled;
                currentUser.AccessFailedCount = applicationUser.AccessFailedCount;
                
                try
                {
                    _context.Update(currentUser);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // If we still get a concurrency exception, show error to user
                    ModelState.AddModelError("", "The record you attempted to edit was modified by another user after you got the original value. Please refresh and try again.");
                    return View(currentUser);
                }
                return RedirectToAction(nameof(Index));
            }
            return View(applicationUser);
        }

        public async Task<IActionResult> EditOrganizations(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var applicationUser = await _context.ApplicationUser.Include("GroupAssignments").SingleOrDefaultAsync(m => m.Id == id);
            if (applicationUser == null)
            {
                return NotFound();
            }
             
             
            var groups = await _context.Group.Include("GroupAssignments").ToListAsync();

            var groupEditDetails = groups.Select(org => new GroupSelectedViewModel
            {
                Name = org.Name,
                Id = org.Id,
                Selected = applicationUser.GroupAssignments.Any(oa => oa.GroupId == org.Id)
            }).ToList();

            var UserOrganizations = new UserGroupsViewModel { Id = id, Name = applicationUser.UserName, Organizations = groupEditDetails };

            if (groupEditDetails == null)
            {
                return NotFound();
            }

            return View(UserOrganizations);
        }

        // POST: ApplicationUsers/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Special.Roles.Admin)]
        public async Task<IActionResult> EditOrganizations(string id, [Bind("Id, Name")] UserGroupsViewModel applicationUser, [Bind] IEnumerable<GroupSelectedViewModel> UserOrganizations)
        {
            if (id != applicationUser.Id)
            {
                return NotFound();
            }

            ///Check that the user has the right to alter group membership in every affected group
            var groups = _context.Group.Where(g => UserOrganizations.Any(uo => uo.Id == g.Id));
            foreach(var group in groups)
            {
                var result = await _authorizationService.AuthorizeAsync(User, group, Operations.GroupAccessManager);
                if (result.Succeeded)
                {
                    continue;
                }
                else
                    return ReturnChallengeOrForbidOnFailedAuthorization();
            }
            /////////////////////////////////////////////////////////////////////////////////////
    

            var user = await _context.ApplicationUser.Include("GroupAssignments").SingleOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    foreach(var org in UserOrganizations)
                    {
                        user.UpdateGroupMembership(org); 
                    } 

                    _context.Update(user);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Handle concurrency conflicts by reloading the entity and retrying
                    var currentUser = await _context.ApplicationUser
                        .Include("GroupAssignments")
                        .SingleOrDefaultAsync(u => u.Id == applicationUser.Id);
                    
                    if (currentUser == null)
                    {
                        return NotFound();
                    }
                    
                    try
                    {
                        // Apply the group membership changes to the current user
                        foreach(var org in UserOrganizations)
                        {
                            currentUser.UpdateGroupMembership(org); 
                        } 

                        _context.Update(currentUser);
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        // If still failing after reload, show error to user
                        ModelState.AddModelError("", "The record you attempted to edit was modified by another user after you got the original value. Please refresh and try again.");
                        return View(currentUser);
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        // GET: ApplicationUsers/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var applicationUser = await _context.ApplicationUser
                .SingleOrDefaultAsync(m => m.Id == id);
            if (applicationUser == null)
            {
                return NotFound();
            }

            return View(applicationUser);
        }

        // POST: ApplicationUsers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Special.Roles.Admin)]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var applicationUser = await _context.ApplicationUser.SingleOrDefaultAsync(m => m.Id == id);
            _context.ApplicationUser.Remove(applicationUser);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ApplicationUserExists(string id)
        {
            return _context.ApplicationUser.Any(e => e.Id == id);
        }
    }
}
