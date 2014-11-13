using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using System.Web.Security;
using ConnectomeViz.Helpers;

namespace ConnectomeViz.Controllers
{
    [HandleError]
    [RequiresSSL]
    [Authorize(Roles="Admin")]
    public class UserRoleController : Controller
    {
        public ActionResult Create()
        {
            return View();
        }

        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Create(FormCollection formCollection)
        {
            if (has_valid_form_values(formCollection, ViewMode.Create))
            {
                Roles.CreateRole(formCollection["Role"]);
                return RedirectToAction("List");
            }

            return View();
        }

        public ActionResult List(int? page)
        {
            var paginated_userRoles = new PaginatedList<string>(Roles.GetAllRoles().Cast<string>().AsQueryable<string>(), page.HasValue ? page.Value : 0, 10);
            return View(paginated_userRoles);
        }

        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult List(int? page, FormCollection formCollection)
        {
            

            var deleted_user_roles = String.IsNullOrEmpty(formCollection["Delete"]) ? new string[]{} : formCollection["Delete"].Split(',');

            foreach (var role in deleted_user_roles)
            {
                Roles.DeleteRole(role, false);
            }

            var paginated_userRoles = new PaginatedList<string>(Roles.GetAllRoles().Cast<string>().AsQueryable<string>(), page.HasValue ? page.Value : 0, 10);

            return View(paginated_userRoles);
        }

        #region private methods 

        private bool has_valid_form_values(FormCollection formCollection, ViewMode mode)
        {
            if (mode == ViewMode.Create)
            {
                if(!Extensions.StringHasValue(formCollection["Role"]))
                {
                    ModelState.AddModelError("_FORM", "Role is a required field");
                }
                else if (Roles.RoleExists(formCollection["Role"]))
                {
                    ModelState.AddModelError("_FORM", "The role already exists");
                }
            }

            return ModelState.IsValid;
        }

        #endregion
    }
}
