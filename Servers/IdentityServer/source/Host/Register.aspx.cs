using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Net.Mail;
using IdentityManager.Host;
using Microsoft.AspNet.Identity;



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
            IdentityManager.Host.CustomUserManager manager = new IdentityManager.Host.CustomUserManager(userStore);

            var user = new IdentityManager.Host.CustomUser()
            {
                UserName = UserName.Text,
                LockoutEnabled = false
            };
            user.FirstName = TextFirstName.Text;
            user.LastName = TextLastName.Text;
            user.Email = TextEmail.Text;
            user.EmailConfirmed = false;
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

            ConfirmUserEmailAsync(user.Id);
        }

        protected async Task<bool> ConfirmUserEmailAsync(Guid Userid)
        {
            // Default UserStore constructor uses the default connection string named: DefaultConnection
            var userStore = new IdentityManager.Host.CustomUserStore(new IdentityManager.Host.CustomContext("IdentityDB"));
            IdentityManager.Host.CustomUserManager manager = new IdentityManager.Host.CustomUserManager(userStore);
            
            string EmailConfirmationCode = manager.GenerateEmailConfirmationToken(Userid);
            
            CustomUser user = manager.Users.Single(u => u.Id == Userid);

            IdentityResult EmailResult = await manager.ConfirmEmailAsync(user.Id, EmailConfirmationCode);
            if(EmailResult.Succeeded)
            {
                user = manager.Users.Single(u => u.Id == Userid);
                user.EmailConfirmed = true;
                IdentityResult result = await manager.UpdateAsync(user);
                return result.Succeeded;
            }

            return false;
        }
    }
}