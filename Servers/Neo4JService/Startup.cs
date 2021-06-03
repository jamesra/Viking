using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(Neo4JService.Startup))]

namespace Neo4JService
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
