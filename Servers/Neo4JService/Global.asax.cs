using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace Neo4JService
{
    public class WebApiApplication : System.Web.HttpApplication
    { 
        protected void Application_Start()
        {
            new System.Diagnostics.TraceSource("Neo4JService", System.Diagnostics.SourceLevels.Information)
                .TraceInformation(Viking.ProductVersioning.ProductVersion.Describe(typeof(WebApiApplication).Assembly));

            var json = GlobalConfiguration.Configuration.Formatters.JsonFormatter;
            json.UseDataContractJsonSerializer = true; 
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }
}
