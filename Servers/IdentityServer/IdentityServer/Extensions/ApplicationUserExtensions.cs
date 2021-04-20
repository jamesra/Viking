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
                                    .SingleOrDefault(m => m.Id == user.Id);

            if (applicationUser == null)
            {
                return null;
            }

            //Populate the user's organizations
            /*
            var orgs = await _context.Group.Include("GroupAssignments").Select(org => new GroupSelectedViewModel
            {
                Name = org.Name,
                Id = org.Id,
                Selected = false// = applicationUser.GroupAssignments.Any(oa => oa.GroupId == org.Id)
            }).OrderBy(o => o.Name).ToListAsync();

            orgs.ForEach(org => org.Selected = applicationUser.GroupAssignments.Any(oa => oa.GroupId == org.Id));
            */

            var orgs = await _context.OrgUnit.Select(o => new NamedItemSelectedViewModel<long>() { Id = o.Id, Name = o.Name, Selected = false }).ToListAsync();
            return new UserClaimRequestViewModel() { UserId = user.Id, AvailableOrganizations = orgs, UserComments="", NewOrganization="" };
        }


    }
}
