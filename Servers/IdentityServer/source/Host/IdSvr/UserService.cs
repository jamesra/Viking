using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Thinktecture.IdentityServer.AspNetIdentity;
using Thinktecture.IdentityServer.Core.Configuration;
using Thinktecture.IdentityServer.Core.Services;
using IdentityManager.Host;

namespace VikingIdentityServer
{  
    public static class UserServiceExtensions
    {
        public static void ConfigureUserService(this IdentityServerServiceFactory factory, string connString)
        {
            factory.UserService = new Registration<IUserService, UserService>();
            factory.Register(new Registration<CustomUserManager>());
            factory.Register(new Registration<CustomUserStore>());
            factory.Register(new Registration<CustomContext>(resolver => new CustomContext(connString)));
        }
    }

    public class UserService : AspNetIdentityUserService<CustomUser, Guid >
    {
        public UserService(CustomUserManager userMgr)
            : base(userMgr)
        {
        }
    } 
}