using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Neo4JService
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
            /*
            routes.MapRoute(
               name: "Default",
               url: "Query",
               defaults: new { controller = "Query", action="Get", id = UrlParameter.Optional }
           );*/
            /*
            routes.MapRoute("Neo4J_Post", "Neo4J",
                new { controller = "Values", action = "Post" },
                new { controller = "Values", httpMethod = new HttpMethodConstraint("POST") });

            routes.MapRoute("Neo4J_Get", "Neo4J",
                new { controller = "Values", action = "Get" },
                new { controller = "Values", httpMethod = new HttpMethodConstraint("GET") });
                */
        }
    }
}
