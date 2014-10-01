using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace ConnectomeViz
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute("AdminUserRole", "Admin/UserRole/List/{page}", new { controller = "UserRole", action = "List", page = "0" }, new { controller = "UserRole", page = @"\d+" });
            routes.MapRoute("AdminUserRoleAction", "Admin/UserRole/{action}/{role}", new { controller = "UserRole", action = "List", role = "" }, new { controller = "UserRole" });

            routes.MapRoute("AdminUser", "Admin/User/List/{page}", new { controller = "User", action = "List", page = "0" }, new { controller = "User", page = @"\d+" });
            routes.MapRoute("AdminUserAction", "Admin/User/{action}/{username}", new { controller = "User", action = "", username = "" });

            routes.MapRoute("Admin", "Admin", new { controller = "Admin", action = "Index" });

            //  routes.MapRoute(
            //    "Info",                                              // Route name
            //    "FormRequest/{action}/{param1}/{param2}",                           // URL with parameters
            //    new {controller = "FormRequest", action = "GetTopStructures", param1 = (string)null, param2 = (string)null }  // Parameter defaults
            //);

            routes.MapRoute(
             "Default",                                              // Route name
             "{controller}/{action}/{id}",                           // URL with parameters
             new { controller = "Default", action = "Index", id = (string)null }  // Parameter defaults
         );
            routes.MapRoute(
               "subpath",                                              // Route name
               "Viz/{controller}/{action}/{id}",                           // URL with parameters
               new { controller = "Default", action = "Index", id = (string)null }  // Parameter defaults
           );

            routes.MapRoute(
              "testpath",                                              // Route name
              "Test/{controller}/{action}/{id}",                           // URL with parameters
              new { controller = "Default", action = "Index", id = (string)null }  // Parameter defaults
          );
        }
    }
}