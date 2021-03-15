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
using Microsoft.AspNetCore.Authorization;


namespace IdentityServer.Controllers
{
    public class GroupsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GroupsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Organizations
        public async Task<IActionResult> Index()
        {
            return View(await _context.Group.Include("GroupAssignments").ToListAsync());
        }

        // GET: Organizations/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var organization = await _context.Group
                .Include("GroupAssignments.User")
                .Include(g => g.UsersWithPermissions)
                .Include(g => g.ResourceType)
                .Include(g => g.GroupsWithPermissions)
               .SingleOrDefaultAsync(m => m.Id == id);

            if (organization == null)
            {
                return NotFound();
            }

            return View(organization);
        }

        // GET: Organizations/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Organizations/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Config.AdminRoleName)]
        public async Task<IActionResult> Create([Bind("Name,ShortName")] Group group)
        {
            if (ModelState.IsValid)
            {
                _context.Add(group);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(group);
        }

        public IActionResult VerifyUniqueGroupName(string groupName)
        {
            if (_context.Group.Any(g => g.Name == groupName))
                return Json($"A group named {groupName} already exists");

            return Json(true); 
        }

        // GET: Organizations/Edit/5
        public async Task<IActionResult> ViewGroupPermissions(long id)
        {   
            return RedirectToAction("Index", "GroupPermissions", new { GroupId = id });
        }

        // GET: Organizations/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var groupEditDetails = await _context.Group
                .Include("GroupAssignments")
                .Select(org => new GroupDetailsViewModel
            {
                Name = org.Name,
                Id = org.Id,
                UserList = _context.Users.Select(u => new UserSelectedViewModel
                {
                    Id = u.Id,
                    Name = u.UserName,
                    Selected = org.Users.Any(oa => oa.Id == u.Id)
                }).ToList(),
                Children = org.Children.Select(g => new GroupDetailsViewModel
                {
                    Name = g.Name,
                    Id = g.Id
                }
                ).ToList()
            })
            .SingleOrDefaultAsync(m => m.Id == id);
             
            if (groupEditDetails == null)
            {
                return NotFound();
            }

            return View(groupEditDetails);
        }

        // POST: Organizations/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Config.AdminRoleName)]
        public async Task<IActionResult> Edit(long id, [Bind("Id,Name,Description")] GroupDetailsViewModel groupDetails, [Bind] IEnumerable<UserSelectedViewModel> usersSelected)
        {
            if (id != groupDetails.Id)
            {
                return NotFound();
            }

            var group = await _context.Group.Include("GroupAssignments").SingleOrDefaultAsync(m => m.Id == id);

            if (group == null)
            {
                return NotFound();
            }


            if (ModelState.IsValid)
            {
                try
                {
                    group.Name = groupDetails.Name;
                    group.Description = groupDetails.Description;

                    group.UpdateUserOrganizations(usersSelected);
                    /*
                    foreach(UserSelectedViewModel user in usersSelected)
                    {
                        var ExistingMapping = organization.OrganizationAssignments.FirstOrDefault(u => u.UserId == user.Id);

                        if (user.Selected)
                        {
                            if (ExistingMapping == null)
                            {
                                //Create the mapping
                                OrganizationAssignment oa = new OrganizationAssignment() { OrganizationId = organization.Id, UserId = user.Id };
                                organization.OrganizationAssignments.Add(oa);
                            }
                        }
                        else
                        {
                            if(ExistingMapping != null)
                            {
                                //Remove the mapping
                                organization.OrganizationAssignments.Remove(ExistingMapping);
                            }
                        }
                    }*/
                    
                    _context.Update(group);
                    await _context.SaveChangesAsync(); 
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OrganizationExists(group.Id))
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

            return View(groupDetails);
        }

        // GET: Organizations/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var organization = await _context.Group
                .SingleOrDefaultAsync(m => m.Id == id);
            if (organization == null)
            {
                return NotFound();
            }

            return View(organization);
        }

        // POST: Organizations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Config.AdminRoleName)]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var organization = await _context.Group.SingleOrDefaultAsync(m => m.Id == id);
            _context.Group.Remove(organization);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool OrganizationExists(long id)
        {
            return _context.Group.Any(e => e.Id == id);
        }


    }
}
