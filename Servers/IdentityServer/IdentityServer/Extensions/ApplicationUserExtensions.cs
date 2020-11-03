using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using IdentityServer.Models.UserViewModels;
using IdentityServer.Models;
using IdentityServer.Data;

namespace IdentityServer.Extensions
{
    public static class ApplicationUserExtensions
    {
        public async static Task<UserClaimRequestViewModel> CreateUserClaimsRequest(this ApplicationUser user, ApplicationDbContext _context)
        {
            var applicationUser = _context.ApplicationUser
                                    .Include("GroupAssignments")
                                    .SingleOrDefault(m => m.Id == user.Id);

            if (applicationUser == null)
            {
                return null;
            }

            //Populate the user's organizations
            var orgs = await _context.Group.Include("GroupAssignments").Select(org => new GroupSelectedViewModel
            {
                Name = org.Name,
                Id = org.Id,
                Selected = false// = applicationUser.GroupAssignments.Any(oa => oa.GroupId == org.Id)
            }).OrderBy(o => o.Name).ToListAsync();

            orgs.ForEach(org => org.Selected = applicationUser.GroupAssignments.Any(oa => oa.GroupId == org.Id));

            //Populate the user's roles
            var roles = await _context.Roles.Select(role => new RoleSelectedViewModel
            {
                Name = role.Name,
                Id = role.Id,
                Selected = false
            }).OrderBy(r => r.Name).ToListAsync();
             
            //Select all of the orgs the user is already a member of
             //;  
            roles.ForEach(role => _context.UserRoles.Any(ur => ur.RoleId == role.Id && ur.UserId == user.Id)); 

            return new UserClaimRequestViewModel() { UserId = user.Id, AvailableOrganizations = orgs, AvailableRoles = roles, NewOrganization="" };
        }
    }
}
