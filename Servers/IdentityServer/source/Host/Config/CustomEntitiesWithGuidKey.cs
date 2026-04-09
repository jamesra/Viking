/*
 * Copyright 2014 Dominick Baier, Brock Allen
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Security.Claims;

namespace IdentityManager.Host
{

    public class CustomUser : IdentityUser<Guid, CustomUserLogin, CustomUserRole, CustomUserClaim>
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
    public class CustomUserLogin : IdentityUserLogin<Guid> { }
    public class CustomUserRole : IdentityUserRole<Guid> { }
    public class CustomUserClaim : IdentityUserClaim<Guid> { }

    public class CustomRole : IdentityRole<Guid, CustomUserRole> { }

    public class CustomContext : IdentityDbContext<CustomUser, CustomRole, Guid, CustomUserLogin, CustomUserRole, CustomUserClaim>
    {
        public CustomContext(string connString)
            : base(connString)
        {
        }
    }

    public class CustomUserStore : UserStore<CustomUser, CustomRole, Guid, CustomUserLogin, CustomUserRole, CustomUserClaim>
    {
        public CustomUserStore(CustomContext ctx)
            : base(ctx)
        {
        }

        public override System.Threading.Tasks.Task CreateAsync(CustomUser user)
        {
            user.Id = Guid.NewGuid();

            return base.CreateAsync(user);
        }
    }

    public class CustomUserManager : UserManager<CustomUser, Guid>
    {
        public CustomUserManager(CustomUserStore store)
            : base(store)
        {
            
            this.ClaimsIdentityFactory = new CustomClaimsFactory(); 
        }
    }

    public class CustomRoleStore : RoleStore<CustomRole, Guid, CustomUserRole>
    {
        public CustomRoleStore(CustomContext ctx)
            : base(ctx)
        {
        }

        public override System.Threading.Tasks.Task CreateAsync(CustomRole role)
        {
            role.Id = Guid.NewGuid();
            return base.CreateAsync(role);
        }
    }

    public class CustomRoleManager : RoleManager<CustomRole, Guid>
    {
        public CustomRoleManager(CustomRoleStore store)
            : base(store)
        {
        }
    }

    public class CustomClaimsFactory : ClaimsIdentityFactory<CustomUser, Guid>
    {
        public CustomClaimsFactory()
        {
            this.UserIdClaimType = ClaimTypes.NameIdentifier;
            this.UserNameClaimType = ClaimTypes.Name;
            this.RoleClaimType = "role";
        }

        public override async System.Threading.Tasks.Task<System.Security.Claims.ClaimsIdentity> CreateAsync(UserManager<CustomUser, Guid> manager, CustomUser user, string authenticationType)
        {
            var ci = await base.CreateAsync(manager, user, authenticationType);
            if (!String.IsNullOrWhiteSpace(user.FirstName))
            {
                ci.AddClaim(new Claim(ClaimTypes.GivenName, user.FirstName));
            }
            if (!String.IsNullOrWhiteSpace(user.LastName))
            {
                ci.AddClaim(new Claim(ClaimTypes.Surname, user.LastName));
            }
            return ci;
        }
    }

}