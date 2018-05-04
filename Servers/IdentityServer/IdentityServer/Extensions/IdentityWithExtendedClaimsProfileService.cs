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
                    case "Affiliation":
                    {
                        var OrgAssignments = _Dbcontext.OrganizationAssignments.Include("Organization").Where(oa => oa.UserId == user.Id).ToList();
                        foreach(OrganizationAssignment oa in OrgAssignments)
                        {
                            claims.Add(new Claim("Affiliation", oa.Organization.ShortName));
                        }
                    }
                    break;
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
