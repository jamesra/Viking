using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc; 
using System.Web.Routing;
using System.ComponentModel;
using System.Net;

namespace DataExport
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801


    public class MvcApplication : System.Web.HttpApplication
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute("Network_Dot", "{database}/Network/Dot", 
                            new { controller = "Network", action = "GetDot"},
                            new { controller = "Network"});

            routes.MapRoute("Motifs_Dot", "{database}/Motifs/Dot",
                            new { controller = "Motif", action = "GetDot"},
                            new { controller = "Motif"});

            routes.MapRoute("Motifs_TLP", "{database}/Motifs/TLP",
                            new { controller = "Motif", action = "GetTLP" },
                            new { controller = "Motif" });
        }

        protected void Application_Start()
        {
            
            RegisterRoutes(RouteTable.Routes);

            AreaRegistration.RegisterAllAreas();

            WebApiConfig.Register(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }

    }
}