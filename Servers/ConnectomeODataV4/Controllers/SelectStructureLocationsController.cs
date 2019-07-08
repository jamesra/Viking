using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using ConnectomeDataModel;

namespace ConnectomeODataV4.Controllers
{
    /*
    The WebApiConfig class may require additional changes to add a route for this controller. Merge these statements into the Register method of the WebApiConfig class as applicable. Note that OData URLs are case sensitive.

    using System.Web.Http.OData.Builder;
    using System.Web.Http.OData.Extensions;
    using ConnectomeODataV4.Models;
    ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
    builder.EntitySet<SelectStructureLocations_Result>("SelectStructureLocations_Result");
    config.Routes.MapODataServiceRoute("odata", "odata", builder.GetEdmModel());
    */
    public class SelectStructureLocationsController : ODataController
    {
        private ConnectomeEntities db = new ConnectomeEntities();
        private static ODataValidationSettings _validationSettings = new ODataValidationSettings();

        // GET: odata/SelectStructureLocations
        [EnableQuery(PageSize = WebApiConfig.PageSize)]
        public IHttpActionResult GetSelectStructureLocations(ODataQueryOptions<SelectStructureLocations_Result> queryOptions)
        {
            // validate the query.
            try
            {
                queryOptions.Validate(_validationSettings);
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }

            return Ok<IList<Location>>(db.SelectAllStructureLocations().ToList());
        }

        // GET: odata/SelectStructureLocations(5)
        public IHttpActionResult GetSelectStructureLocations_Result([FromODataUri] long key, ODataQueryOptions<SelectStructureLocations_Result> queryOptions)
        {
            // validate the query.
            try
            {
                queryOptions.Validate(_validationSettings);
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
            return Ok<IList<Location>>(db.SelectStructureLocations(new long?(key)).ToList());
        }
    }
}
