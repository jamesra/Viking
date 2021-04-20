using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using IdentityServer.Data;
using IdentityServer.Models;
using Microsoft.AspNetCore.Authorization;
using IdentityServer.Models.UserViewModels;
using IdentityServer.Extensions;

namespace IdentityServer.Controllers
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

            var applicationUser = await _context.ApplicationUser.Include("GroupAssignments.Group")
                .SingleOrDefaultAsync(m => m.Id == id);
            if (applicationUser == null)
            {
                return NotFound();
            }

            ViewBag.RecursiveGroups = await _context.RecursiveMemberOfGroups(id);

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
            if (!this.User.IsInRole(Config.AdminRoleName))
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

                try
                {
                    _context.Update(applicationUser);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ApplicationUserExists(applicationUser.Id))
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
        [Authorize(Roles = Config.AdminRoleName)]
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
                var result = await _authorizationService.AuthorizeAsync(User, group, IdentityServer.Authorization.Operations.GroupAccessManager);
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
                    if (!ApplicationUserExists(applicationUser.Id))
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
        [Authorize(Roles = Config.AdminRoleName)]
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
