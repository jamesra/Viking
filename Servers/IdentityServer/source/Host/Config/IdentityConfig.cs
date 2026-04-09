using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework; 
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace IdentityManager.Host
{
    // This is useful if you do not want to tear down the database each time you run the application.
    // You want to create a new database if the Model changes
    // public class MyDbInitializer : DropCreateDatabaseIfModelChanges<MyDbContext>
    public class MyDbInitializer : DropCreateDatabaseIfModelChanges<CustomContext>
    {
        protected override void Seed(CustomContext context)
        {
            InitializeIdentityForEF(context);
            base.Seed(context);
        }

        private void InitializeIdentityForEF(CustomContext context)
        {
            var UserManager = new CustomUserManager(new CustomUserStore(context));
            var RoleManager = new CustomRoleManager(new CustomRoleStore(context));
            

            const string AdminRoleName = "Admin";
            string name = "Admin";
            string password = "administrator"; 
             
            //Create Role Admin if it does not exist
            if (!RoleManager.RoleExists(AdminRoleName))
            {
                var roleresult = RoleManager.Create(new CustomRole { Name = AdminRoleName });
            }

            //Create Admin user
            var user = new CustomUser();
            user.UserName = name;
            user.FirstName = "Administrator";
            user.LastName = "Administrator";
            user.TwoFactorEnabled = false;
            user.LockoutEnabled = false;

            var adminresult = UserManager.Create(user, password);
            System.Diagnostics.Debug.Assert(adminresult.Succeeded == true);

            //Add User Admin to Role Admin
            if (adminresult.Succeeded)
            {
                var result = UserManager.AddToRole(user.Id, name);
                System.Diagnostics.Debug.Assert(result.Succeeded == true);

                result = UserManager.AddClaim(user.Id, new System.Security.Claims.Claim("role", "Admin"));
                System.Diagnostics.Debug.Assert(result.Succeeded == true);
            }
        }
    }
}