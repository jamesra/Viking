using System.Web.Http;
using Viking.ProductVersioning;

namespace Neo4JService.Controllers
{
    /// <summary>
    /// GET /version. No <c>[Authorize]</c>, so operators can read the build
    /// without a bearer token. The host authentication filter still runs but
    /// does not require a token unless an authorize attribute is present.
    /// </summary>
    public class VersionController : ApiController
    {
        /// <summary>
        /// Returns the assembly name and version of this service.
        /// </summary>
        [Route("version")]
        [HttpGet]
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
