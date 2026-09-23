using System.Web.Http;
using Viking.ProductVersioning;

namespace ConnectomeODataV4.Controllers
{
    /// <summary>
    /// GET /version for operators and other agents.
    /// The route is registered before the OData catch-all in <see cref="WebApiConfig"/>.
    /// </summary>
    public class VersionController : ApiController
    {
        /// <summary>
        /// Returns the assembly name and file version of this OData host.
        /// </summary>
        public IHttpActionResult Get()
        {
            var assembly = typeof(VersionController).Assembly;
            return Ok(new
            {
                name = assembly.GetName().Name,
                version = ProductVersion.VersionOf(assembly)
            });
        }
    }
}
