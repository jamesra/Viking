using System.Web.Http;

namespace ConnectomeODataV4
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            //SqlServerTypes.Utilities.LoadNativeAssemblies(Server.MapPath("~/bin"));
            ConnectomeDataModel.Configuration.LoadNativeAssemblies(Server.MapPath("~/bin"));
             
            GlobalConfiguration.Configure(WebApiConfig.Register);
            GlobalConfiguration.DefaultServer.Configuration.EnsureInitialized();
        }
    }
}
