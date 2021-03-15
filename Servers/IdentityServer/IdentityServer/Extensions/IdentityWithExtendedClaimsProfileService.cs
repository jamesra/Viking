using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityModel;
using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using IdentityServer.Models;
using IdentityServer.Data; 

namespace IdentityServer.Extensions
{
    using IdentityServer4;

    public class IdentityWithExtendedClaimsProfileService : IProfileService
    {
        private readonly IUserClaimsPrincipalFactory<ApplicationUser> _claimsFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _Dbcontext;

        public IdentityWithExtendedClaimsProfileService(UserManager<ApplicationUser> userManager,
                                                        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
                                                        ApplicationDbContext Dbcontext )
        {
            _userManager = userManager;
            _claimsFactory = claimsFactory;
            _Dbcontext = Dbcontext;
        }

        public async Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            var sub = context.Subject.GetSubjectId();
            var user = await _userManager.FindByIdAsync(sub);
            var principal = await _claimsFactory.CreateAsync(user);

            var claims = principal.Claims.ToList();
            claims = claims.Where(claim => context.RequestedClaimTypes.Contains(claim.Type)).ToList();

            foreach(string claimType in context.RequestedClaimTypes)
            {
                switch (claimType)
                {
                    case "group":
                    {
                        var OrgAssignments = _Dbcontext.UserToGroupAssignments.Include("Group").Where(oa => oa.UserId == user.Id).ToList();
                        foreach(var oa in OrgAssignments)
                        {
                            //TODO: Add the role name they have under that group
                            claims.Add(new Claim("MemberOf", oa.Group.Id.ToString()));
                        }
                    }
                    break;/*
                    case "group role":
                        {
                            //Add a claim for each group the user had a role within
                            var UserGroupRoles = from u in _Dbcontext.Users
                                                  join ga in _Dbcontext.GroupAssignments on u.Id equals ga.UserId //This is probably paranoid, but it ensures the user is still assigned to the group 
                                                  join ur in _Dbcontext.UserRoles on ga.GroupId equals ur.GroupID
                                                  where u.Id == user.Id
                                                  select new
                                                  {
                                                      GroupRole = ur,
                                                      User = u,
                                                      Group = ga.GroupId
                                                  };

                            foreach (var ugr in UserGroupRoles)
                            {
                                string claim_value = $"{ugr.Group},{ugr.GroupRole.RoleId}";
                                //TODO: Add the role name they have under that group
                                claims.Add(new Claim("group Role", claim_value));
                            }
                        }
                        break;*/
                    case JwtClaimTypes.FamilyName:
                        claims.Add(new Claim(JwtClaimTypes.FamilyName, user.FamilyName));
                        break;
                    case JwtClaimTypes.GivenName:
                        claims.Add(new Claim(JwtClaimTypes.GivenName, user.GivenName));
                        break;
                    case JwtClaimTypes.Email:
                        claims.Add(new Claim(JwtClaimTypes.Email, user.Email));
                        break;
                } 
            }


            context.IssuedClaims = claims;
        }

        public async Task IsActiveAsync(IsActiveContext context)
        {
            var sub = context.Subject.GetSubjectId();
            var user = await _userManager.FindByIdAsync(sub);
            context.IsActive = user != null;
        }
    }
}
