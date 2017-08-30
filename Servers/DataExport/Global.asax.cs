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
            routes.MapRoute("Network_GetDot", "Network/Dot",
                            new { controller = "Network", action = "GetDot" },
                            new { controller = "Network", httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute("Network_PostDot", "Network/Dot",
                            new { controller = "Network", action = "PostDot" },
                            new { controller = "Network", httpMethod = new HttpMethodConstraint("POST") });

            routes.MapRoute("Network_GetTLP", "Network/TLP",
                            new { controller = "Network", action = "GetTLP" },
                            new { controller = "Network", httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute("Network_PostTLP", "Network/TLP",
                            new { controller = "Network", action = "PostTLP" },
                            new { controller = "Network", httpMethod = new HttpMethodConstraint("POST") });

            routes.MapRoute("Network_GetGML", "Network/GraphML",
                            new { controller = "Network", action = "GetGML" },
                            new { controller = "Network", httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute("Network_PostGML", "Network/GraphML",
                            new { controller = "Network", action = "PostGML" },
                            new { controller = "Network", httpMethod = new HttpMethodConstraint("POST") });

            routes.MapRoute("Network_GetJSON", "Network/JSON",
                            new { controller = "Network", action = "GetJSON" },
                            new { controller = "Network", httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute("Network_PostJSON", "Network/JSON",
                            new { controller = "Network", action = "PostJSON" },
                            new { controller = "Network", httpMethod = new HttpMethodConstraint("POST") });

            routes.MapRoute("Motifs_GetDot", "Motifs/Dot",
                            new { controller = "Motif", action = "GetDot"},
                            new { controller = "Motif"});

            routes.MapRoute("Motifs_GetTLP", "Motifs/TLP",
                            new { controller = "Motif", action = "GetTLP" },
                            new { controller = "Motif" });

            routes.MapRoute("Motifs_GetJSON", "Motifs/JSON",
                            new { controller = "Motif", action = "GetJSON" },
                            new { controller = "Motif" });

            routes.MapRoute("Morphology_GetTLP", "Morphology/TLP",
                            new { controller = "Morphology", action = "GetTLP" },
                            new { controller = "Morphology", httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute("Morphology_PostTLP", "Morphology/TLP",
                            new { controller = "Morphology", action = "PostTLP" },
                            new { controller = "Morphology", httpMethod = new HttpMethodConstraint("POST") });

            routes.MapRoute("Morphology_GetJSON", "Morphology/JSON",
                            new { controller = "Morphology", action = "GetJSON" },
                            new { controller = "Morphology" });

            routes.MapRoute("Morphology_PostJSON", "Morphology/JSON",
                            new { controller = "Morphology", action = "PostJSON" },
                            new { controller = "Morphology", httpMethod = new HttpMethodConstraint("POST") });

            routes.MapRoute("Morphology_GetDAE", "Morphology/DAE",
                            new { controller = "Morphology", action = "GetDAE" },
                            new { controller = "Morphology", httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute("Morphology_PostDAE", "Morphology/DAE",
                            new { controller = "Morphology", action = "PostDAE" },
                            new { controller = "Morphology", httpMethod = new HttpMethodConstraint("POST") });

            routes.IgnoreRoute("Output");

            //Ignore the root level so we can load our static index.html file
            routes.IgnoreRoute("");
        }

        protected void Application_Start()
        {
            SqlServerTypes.Utilities.LoadNativeAssemblies(Server.MapPath("~/bin"));

            RegisterRoutes(RouteTable.Routes);

            AreaRegistration.RegisterAllAreas();

            WebApiConfig.Register(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }
    }
}