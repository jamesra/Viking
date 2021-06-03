﻿using IdentityServer.Data;
using IdentityServer.Models;
using IdentityServer.Models.UserViewModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

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

        public static string GetUsername(this System.Security.Principal.IIdentity identity)
        {
            if (identity.IsAuthenticated == false)
                return null;

            if (identity.Name != null)
                return identity.Name;

            if (identity is System.Security.Claims.ClaimsIdentity principal)
            {
                var result =  principal.Claims.FirstOrDefault(c => c.Type.Equals("name", StringComparison.OrdinalIgnoreCase))?.Value;
                return result;
            }

            return null;
        } 
    }


}
