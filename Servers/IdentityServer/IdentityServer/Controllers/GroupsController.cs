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

            var organization = await _context.Group.Include("GroupAssignments.User")
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
        [Authorize(Roles = "Access Manager")]
        public async Task<IActionResult> Create([Bind("Id,Name")] Group organization)
        {
            if (ModelState.IsValid)
            {
                _context.Add(organization);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(organization);
        }

        // GET: Organizations/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var organizationEditDetails = await _context.Group.Include("GroupAssignments").Select(org => new GroupDetailsViewModel
                {
                    Name = org.Name,
                    Id = org.Id,
                    UserList = _context.Users.Select(u => new UserSelectedViewModel
                    {
                        Id = u.Id,
                        Name = u.UserName,
                        Selected = org.GroupAssignments.Any(oa => oa.UserId == u.Id)
                    }).ToList()
                })
                .SingleOrDefaultAsync(m => m.Id == id);
             
            if (organizationEditDetails == null)
            {
                return NotFound();
            }

            return View(organizationEditDetails);
        }

        // POST: Organizations/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Access Manager")]
        public async Task<IActionResult> Edit(long id, [Bind("Id,Name,ShortName")] GroupDetailsViewModel organizationDetails, [Bind] IEnumerable<UserSelectedViewModel> usersSelected)
        {
            if (id != organizationDetails.Id)
            {
                return NotFound();
            }

            var organization = await _context.Group.Include("GroupAssignments").SingleOrDefaultAsync(m => m.Id == id);

            if (organization == null)
            {
                return NotFound();
            }


            if (ModelState.IsValid)
            {
                try
                {
                    organization.Name = organizationDetails.Name;
                    organization.ShortName = organizationDetails.ShortName;

                    organization.UpdateUserOrganizations(usersSelected);
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
                    
                    _context.Update(organization);
                    await _context.SaveChangesAsync(); 
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OrganizationExists(organization.Id))
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

            return View(organizationDetails);
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
        [Authorize(Roles = "Access Manager")]
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
