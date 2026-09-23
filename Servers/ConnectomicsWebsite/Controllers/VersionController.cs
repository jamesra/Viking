using System.Web.Mvc;
using Viking.ProductVersioning;

namespace ConnectomicsWebsite.Controllers
{
    /// <summary>
    /// GET /version. Plain text so agents can read the site build without a view.
    /// </summary>
    public class VersionController : Controller
    {
        /// <summary>
        /// Returns the assembly name and version of this website.
        /// </summary>
        public ActionResult Index()
        {
            return Content(ProductVersion.Describe(typeof(VersionController).Assembly), "text/plain");
        }
    }
}
