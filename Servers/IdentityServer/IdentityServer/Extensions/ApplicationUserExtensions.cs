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
        public static UserClaimRequestViewModel CreateUserClaimsRequest(this ApplicationUser user, ApplicationDbContext _context)
        {
            var applicationUser = _context.ApplicationUser
                                    .Include("OrganizationAssignments")
                                    .SingleOrDefault(m => m.Id == user.Id);

            if (applicationUser == null)
            {
                return null;
            }

            //Populate the user's organizations
            var orgs = _context.Organization.Include("OrganizationAssignments").Select(org => new OrganizationSelectedViewModel
            {
                Name = org.Name,
                Id = org.Id,
                Selected = applicationUser.OrganizationAssignments.Any(oa => oa.OrganizationId == org.Id)
            }).OrderBy(o => o.Name).ToListAsync();

            //Populate the user's roles
            var roles = _context.Roles.Select(role => new RoleSelectedViewModel
            {
                Name = role.Name,
                Id = role.Id,
                Selected = _context.UserRoles.Any(ur => ur.RoleId == role.Id && ur.UserId == user.Id)
            }).OrderBy(r => r.Name).ToListAsync();

            return new UserClaimRequestViewModel() { UserId = user.Id, AvailableOrganizations = orgs.Result, AvailableRoles = roles.Result };
        }

       
    }
}
