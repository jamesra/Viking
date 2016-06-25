using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(ConnectomicsWebsite.Startup))]
namespace ConnectomicsWebsite
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
