using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using System.Web.Security;
using ConnectomeViz.Helpers;
using TweetSharp;


namespace ConnectomeViz.Controllers
{
    [RequiresSSL]
    public class UserController : Controller
    {
        [Authorize(Roles="Admin")]
        public ActionResult List(int? page)
        {
            var paged_users = new PaginatedList<MembershipUser>(Membership.GetAllUsers().Cast<MembershipUser>().AsQueryable<MembershipUser>(), page.HasValue ? page.Value : 0, 10);
            return View(paged_users);
        }

        [Authorize(Roles = "Admin")]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult List(int? pageIndex, FormCollection formCollection)
        {
            var deleted_users = (!Extensions.StringHasValue(formCollection["Delete"]))?(new string[]{}):(formCollection["Delete"].Split(','));

            var locked_users = (!Extensions.StringHasValue(formCollection["IsLockedOut"]))?(new string[]{}):(formCollection["IsLockedOut"].Split(','));

            // Check if users need to be deleted
            if (deleted_users.Count() > 0)
            {
                foreach (var username in deleted_users)
                {
                    Membership.DeleteUser(username, true);
                }
            }

            // Unlocking the users
            if (locked_users.Count() > 0)
            {
                foreach (string username in locked_users)
                {
                    // Check if the user has been marked for delete...
                    if (!deleted_users.Contains(username))
                    {
                        Membership.GetUser(username).UnlockUser();
                    }
                }
            }

            // More options... I really hate this multi-looping code
            // Need to look for a better way of doing this...

            return RedirectToAction("List", new { pageIndex = pageIndex });
        }

        [Authorize(Roles = "Admin")]
        public ActionResult Create()
        {
            return View(new UserFormViewModel(Roles.GetAllRoles(), null, null, ViewMode.Create));
        }

        [Authorize]
        public ActionResult Error()
        {
            return View();
        }

        [Authorize]
        public ActionResult Details(string userName)
        {
            if (String.Compare(userName,HttpContext.User.Identity.Name) !=0 )
            {
                string[] roles = Roles.GetRolesForUser(HttpContext.User.Identity.Name);
                if (roles.Count() > 0 && roles[0] != "Admin")
                {
                    ViewData["errorMessage"] = "Sorry, you are not authorized to view this page";
                    return RedirectToAction("Error");
                }

            }
            return View(new UserFormViewModel(Roles.GetAllRoles(), Roles.GetRolesForUser(userName), Membership.GetUser(userName), ViewMode.Details));
        }

        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Details(string userName, FormCollection formCollection)
        {
            //var is_Approved = formCollection["isApproved"];

            MembershipUser user = Membership.GetUser(userName);

                  
            var is_Deleted = formCollection["Delete"];

            string[] roles = Roles.GetRolesForUser(HttpContext.User.Identity.Name);

            Boolean isUserAdmin = roles.Contains("Admin");
            

            if (!String.IsNullOrEmpty(is_Deleted) && String.Compare(is_Deleted, userName, true) == 0)
            {
                Membership.DeleteUser(userName, true);
                if (isUserAdmin)
                    return RedirectToAction("List");
                else
                {
                    return RedirectToAction("LogOff", "Account");

                }
            }
            else if (roles != null && roles.Count() > 0 && !isUserAdmin )
            {
                reset_password(userName, formCollection);

                return View(new UserFormViewModel(Roles.GetAllRoles(), Roles.GetRolesForUser(userName), Membership.GetUser(userName), ViewMode.Details));
            }

            else
            {
                // If the user is deleted, then we don't have to execute this code...

                // Resetting the password
                reset_password(userName, formCollection);


                string[] user_roles = String.IsNullOrEmpty(formCollection["UserRoles"]) ? new string[] { } : formCollection["UserRoles"].Split(',');

                reset_user_roles(userName, user_roles);


                // Unlocking the user
                var unlock = String.IsNullOrEmpty(formCollection["IsLockedOut"]) ? String.Empty : formCollection["IsLockedOut"];
                if (String.IsNullOrEmpty(unlock))
                {

                    user.UnlockUser();
                }


                var is_Approved = formCollection["isApproved"];

                if (!String.IsNullOrEmpty(is_Approved) && String.Compare(is_Approved, userName, true) == 0)
                {
                    user.IsApproved = true;

                    Membership.UpdateUser(user);

                    return View(new UserFormViewModel(Roles.GetAllRoles(), Roles.GetRolesForUser(userName), Membership.GetUser(userName), ViewMode.Details));

                }
                else
                {
                    user.IsApproved = false;

                    Membership.UpdateUser(user);

                    return View(new UserFormViewModel(Roles.GetAllRoles(), Roles.GetRolesForUser(userName), Membership.GetUser(userName), ViewMode.Details));
                }
            }
            
           
            
           
        }

        public ActionResult Edit(string username)
        {
            return View();
        }

        #region private methods 

        private void reset_password(string userName, FormCollection formCollection)
        {
            var old_password = formCollection["OldPassword"];
            var new_password = formCollection["NewPassword"];
            var confirm_password = formCollection["ConfirmPassword"];

            if (Extensions.StringHasValue(old_password) && Extensions.StringHasValue(new_password) && Extensions.StringHasValue(confirm_password))
            {
                if (String.Compare(new_password, confirm_password, false) == 0)
                {
                    var user = Membership.GetUser(userName);
                    if (user != null)
                    {
                        if (!user.ChangePassword(old_password, new_password))
                        {
                            ModelState.AddModelError("_FORM", "The password was not changed");
                        }
                    }
                }
                else
                {
                    ModelState.AddModelError("NewPassword", "The new password & confirm password don't match");
                }
            }

        }

        private bool has_valid_form_values(FormCollection formCollection, ViewMode mode)
        {
            var user_name = formCollection["UserName"];
            var email = formCollection["Email"];

            if (!Extensions.StringHasValue(user_name))
            {
                ModelState.AddModelError("UserName", "User name is required");
            }
            else if (mode == ViewMode.Create && Membership.FindUsersByName(user_name).Count > 0)
            {
                ModelState.AddModelError("UserName", "User name already exists");
            }

            return ModelState.IsValid;
        }

        private void reset_user_roles(string userName, string[] newUserRoles)
        {
            // Need to clear all the existing roles
            var existingUserRoles = Roles.GetRolesForUser(userName);
            foreach (var role in existingUserRoles)
            {
                Roles.RemoveUserFromRole(userName, role);
            }

            // Set the new roles
            foreach (var role in newUserRoles)
            {
                Roles.AddUserToRole(userName, role);
            }
        }

        #endregion

    }
}
