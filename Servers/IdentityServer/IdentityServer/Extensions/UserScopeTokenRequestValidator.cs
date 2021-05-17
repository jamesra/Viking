using System;
using System.Diagnostics;
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
using IdentityServer4.Validation;


namespace IdentityServer.Extensions
{
    public class UserScopeTokenRequestValidator : ICustomTokenRequestValidator
    {
        ApplicationDbContext _context;

        public UserScopeTokenRequestValidator(ApplicationDbContext context)
        {
            _context = context; 
        }

        public async Task ValidateAsync(CustomTokenRequestValidationContext context)
        {
            foreach (var s in context.Result.ValidatedRequest.ValidatedResources.Resources.ApiScopes)
            {
                Trace.WriteLine(s.Name);
                var parts = s.Name.Split('.');
                string ResourceName;
                string ScopeName;
                if (parts.Length != 2)
                {
                    continue;
                }

                ResourceName = parts[0];
                ScopeName = parts[1];

                //The scope name should match a permissionId 
                var resource = await _context.Resource.FirstOrDefaultAsync(r => r.Name == ResourceName);
                if (resource == null)
                    continue;

                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == context.Result.ValidatedRequest.UserName);
                if (user == null)
                {
                    //Not sure how this happens because User should be authenticated first
                    context.Result.IsError = true;
                    context.Result.Error = $"{user.UserName} not found";
                }

                if(false == await _context.IsUserPermitted(resource.Id, user.Id, ScopeName))
                {
                    context.Result.IsError = true;
                    context.Result.Error = $"{user.UserName} does not have access to scope {s.Name}";
                    context.Result.ErrorDescription = "Most likely the user is lacking a permission on the resource that was requested";
                    return; 
                }
            }
        }
    }
}
