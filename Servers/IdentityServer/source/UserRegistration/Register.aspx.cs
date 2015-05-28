using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using IdentityManager.Host;

namespace UserRegistration
{
    public partial class Register : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {

        }

        protected void CreateUser_Click(object sender, EventArgs e)
        {
            // Default UserStore constructor uses the default connection string named: DefaultConnection
            var userStore = new IdentityManager.Host.CustomUserStore(new IdentityManager.Host.CustomContext("IdentityDB"));
            var manager = new IdentityManager.Host.CustomUserManager(userStore);

            var user = new CustomUser() { UserName = UserName.Text,
                                          LockoutEnabled = false
                                        };
            user.FirstName = "Administrator";
            user.LastName = "Administrator";
            user.TwoFactorEnabled = false;
            
            IdentityResult result = manager.Create(user);

            if (result.Succeeded)
            {
                StatusMessage.Text = string.Format("User {0} was created successfully!", user.UserName);
            }
            else
            {
                StatusMessage.Text = result.Errors.FirstOrDefault();
            }
        }
    }
}