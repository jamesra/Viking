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
using IdentityServer.Extensions;
using IdentityServer.Authorization;


namespace IdentityServer.Controllers
{
    public class GroupsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermissionsViewModelHelper _permissionsHelper;
        private readonly IAuthorizationService _authorization;

        public GroupsController(ApplicationDbContext context, IAuthorizationService authorization, IPermissionsViewModelHelper permissionsHelper)
        {
            _context = context;
            _authorization = authorization;
            _permissionsHelper = permissionsHelper;
        }

        // GET: Organizations
        public async Task<IActionResult> Index()
        {
            return View(await _context.Group.ToListAsync());
        }

        // GET: Organizations/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }
              
            var organization = await _context.Group
                .Include(g => g.UsersWithPermissions).ThenInclude(g => g.PermittedUser)
                .Include(g => g.GroupsWithPermissions).ThenInclude(g => g.PermittedGroup)
                .Include(g => g.ResourceType.Permissions)
                .Include(g => g.Parent)
                .Include("MemberUsers.User")
                //.Include(g => g.MemberUsers)
                //.Include(g => g.MemberGroups)
                .Include("MemberGroups.Member")
                .Include(g => g.MemberOfGroups)
                .Include(g => g.PermissionsHeld)
                .Include(g => g.MemberOfGroups).ThenInclude(mog => mog.Container)
               .SingleOrDefaultAsync(m => m.Id == id);

            if (organization == null)
            {
                return NotFound();
            }

            ViewBag.AccessManagers = await _context.GetGroupAccessManagers(id.Value).Select(u => u.UserName).ToListAsync();

            return View(organization);
        }

        // GET: Organizations/Create
        public IActionResult Create()
        {
            CreateGroupViewModel groupModel = new CreateGroupViewModel()
            {
                Members = new GroupMembershipViewModel()
                {
                    UserList = _context.Users.OrderBy(u => u.UserName).Select(u => new UserSelectedViewModel
                    {
                        Id = u.Id,
                        Name = u.UserName,
                        Selected = false
                    }).ToList(),
                    GroupList = _context.Group.OrderBy(mg => mg.Name).Select(mg => new GroupSelectedViewModel
                    {
                        Id = mg.Id,
                        Name = mg.Name,
                        Selected = false
                    }).ToList(),
                }
            };

            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name));

            return View(groupModel);
        }

        [HttpGet]
        public IActionResult CreateContinue([Bind("Id,Name,Description,ParentId")] CreateResourceViewModel model)
        {
            //Continues creation after user selects a resource type

            CreateGroupViewModel groupModel = new CreateGroupViewModel()
            {
                Name = model.Name,
                Description = model.Description,
                ParentId = model.ParentId,
                Members = new GroupMembershipViewModel()
                {
                    UserList = _context.Users.OrderBy(u => u.UserName).Select(u => new UserSelectedViewModel
                    {
                        Id = u.Id,
                        Name = u.UserName,
                        Selected = false
                    }).ToList(),
                    GroupList = _context.Group.OrderBy(mg => mg.Name).Select(mg => new GroupSelectedViewModel
                    {
                        Id = mg.Id,
                        Name = mg.Name,
                        Selected = false
                    }).ToList(),
                } 
            };

            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), model.ParentId);

            return View(nameof(Create), groupModel);
        }

        // POST: Organizations/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Config.AdminRoleName)]
        public async Task<IActionResult> Create([Bind("Name,ParentId,Description,Members")] CreateGroupViewModel model)
        {
            long? ParentID = (model.ParentId.Value != 0 ? model.ParentId : default) ?? default;
            
            Group group = new Group()
            {
                Name = model.Name,
                ParentID = ParentID,
                Description = model.Description,
                ResourceTypeId = nameof(Group)
            };

            if (false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, group))
            {
                return Unauthorized();
            }

            if (ModelState.IsValid)
            {
                group.UpdateUserMembership(model.Members.UserList);
                group.UpdateGroupMembership(model.Members.GroupList);
                _context.Group.Add(group);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.AvailableParents = new SelectList(_context.OrgUnit.Where(ou => ou.Id >= 0), nameof(OrganizationalUnit.Id), nameof(OrganizationalUnit.Name), model.ParentId);

            return View(group);
        }

        
          
        // GET: Organizations/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            //Determine which groups we are already a member of, this prevents cycles
            List<Group> AlreadyMemberOf = await _context.RecursiveMemberOfGroups(id.Value,false);

            var groupEditDetails = await _context.Group
                .Include(g => g.GroupsWithPermissions)
                .Include(g => g.UsersWithPermissions)
                .Include(g => g.ResourceType.Permissions)
                .Include(g => g.MemberOfGroups)
                .Include(g => g.PermissionsHeld)
                .Include(g => g.MemberUsers)
                .Include(g => g.MemberGroups)
                .Include(g => g.Parent)
                .Where(g => g.Id == id)
                .Select(g => new GroupEditViewModel
                {
                    Group = g, 
                    Members = new GroupMembershipViewModel() {
                        UserList = _context.Users.OrderBy(u => u.UserName).Select(u => new UserSelectedViewModel
                        {
                            Id = u.Id,
                            Name = u.UserName,
                            Selected = g.MemberUsers.Any(uwp => uwp.UserId == u.Id)
                        }).ToList(),
                        GroupList = _context.Group.Where(mg => mg.Id != g.Id && AlreadyMemberOf.Contains(mg) == false).OrderBy(mg => mg.Name).Select(mg => new GroupSelectedViewModel
                        {
                            Id = mg.Id,
                            Name = mg.Name,
                            Selected = g.MemberGroups.Any(uwp => uwp.MemberGroupId == mg.Id),
                        }).ToList(),
                    },
                    AlreadyMemberOf = AlreadyMemberOf.Select(am => am.Name).ToList()
                    /*
                    Children = g.Children.OrderBy(c => c.Name).Select(g => new GroupDetailsViewModel
                    {
                        Name = g.Name,
                        Id = g.Id,
                        Children = new List<GroupDetailsViewModel>()
                    }).ToList()
                    */
                })
                .SingleOrDefaultAsync(g => g.Group.Id == id);

            var authResult = await _authorization.AuthorizeAsync(HttpContext.User, groupEditDetails.Group, IdentityServer.Authorization.Operations.GroupAccessManager);
            if(authResult.Succeeded == false)
            {
                return Unauthorized();
            }

            ViewBag.AvailableParents = _context.OrgUnit.Select(ou => new SelectListItem(ou.Name, ou.Id.ToString())).ToList();

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
        public async Task<IActionResult> Edit(long id, [Bind("Group", "Members", "Children")] GroupEditViewModel groupDetails)
        {
            if (id != groupDetails.Group.Id)
            {
                return NotFound();
            }
              
            var group = await _context.Group
                .Include(g => g.GroupsWithPermissions)
                .Include(g => g.UsersWithPermissions)
                .Include(g => g.ResourceType.Permissions)
                .Include(g => g.MemberOfGroups)
                .Include(g => g.PermissionsHeld)
                .Include(g => g.MemberUsers)
                .Include(g => g.MemberGroups)
                .SingleOrDefaultAsync(m => m.Id == id);

            if (group == null)
            {
                return NotFound();
            }

            if (false == await _authorization.IsGroupAccessManagerAsync(HttpContext.User, group) &&
                false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, group))
            {
                return Unauthorized();
            }
             
            if (ModelState.IsValid)
            {
                try
                {
                    group.Name = groupDetails.Group.Name;
                    group.Description = groupDetails.Group.Description;
                    group.ParentID = groupDetails.Group.ParentID;

                    group.UpdateUserMembership(groupDetails.Members.UserList);
                    group.UpdateGroupMembership(groupDetails.Members.GroupList);
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

            return RedirectToAction(nameof(Details));
        }

        // GET: Organizations/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var authResult = await _authorization.AuthorizeAsync(HttpContext.User, id.Value, IdentityServer.Authorization.Operations.GroupAccessManager);
            if (authResult.Succeeded == false)
            {
                return Unauthorized();
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
            var group = await _context.Group
                .Include(g => g.MemberGroups)
                .SingleOrDefaultAsync(m => m.Id == id);
            if(group == null)
            {
                return NotFound();
            }

            if (false == await _authorization.IsGroupAccessManagerAsync(HttpContext.User, group) &&
                false == await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, group))
            {
                return Unauthorized();
            }

            foreach (var memberGroupAssignment in group.MemberGroups)
            {
                _context.GroupToGroupAssignments.Remove(memberGroupAssignment);
            }

            _context.Group.Remove(group);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool OrganizationExists(long id)
        {
            return _context.Group.Any(e => e.Id == id);
        }
          
    }
}
