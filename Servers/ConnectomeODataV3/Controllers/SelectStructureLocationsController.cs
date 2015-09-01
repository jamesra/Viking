using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.ModelBinding;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using System.Web.Http.OData.Routing;
using ConnectomeDataModel;
using Microsoft.Data.OData;

namespace ConnectomeODataV3.Controllers
{
    /*
    The WebApiConfig class may require additional changes to add a route for this controller. Merge these statements into the Register method of the WebApiConfig class as applicable. Note that OData URLs are case sensitive.

    using System.Web.Http.OData.Builder;
    using System.Web.Http.OData.Extensions;
    using ConnectomeODataV3.Models;
    ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
    builder.EntitySet<SelectStructureLocations_Result>("SelectStructureLocations");
    config.Routes.MapODataServiceRoute("odata", "odata", builder.GetEdmModel());
    */
    public class SelectStructureLocationsController : ODataController
    {
        private ConnectomeEntities db = new ConnectomeEntities();
        private static ODataValidationSettings _validationSettings = new ODataValidationSettings();

        // GET: odata/SelectStructureLocations
        [System.Web.Http.Cors.EnableCors("*", "*", "*")]
        public IHttpActionResult GetSelectStructureLocations(ODataQueryOptions<SelectStructureLocations_Result> queryOptions)
        {
            // validate the query.
            try
            {
                queryOptions.Validate(_validationSettings);
            }
            catch (ODataException ex)
            {
                return BadRequest(ex.Message);
            }
            
            return Ok<IEnumerable<SelectStructureLocations_Result>>((IEnumerable<SelectStructureLocations_Result>)db.SelectAllStructureLocations().ToList());
        }

        // GET: odata/SelectStructureLocations(5)
        [System.Web.Http.Cors.EnableCors("*", "*", "*")]
        public IHttpActionResult GetSelectStructureLocations_Result([FromODataUri] long key, ODataQueryOptions<SelectStructureLocations_Result> queryOptions)
        {
            // validate the query.
            try
            {
                queryOptions.Validate(_validationSettings);
            }
            catch (ODataException ex)
            {
                return BadRequest(ex.Message);
            }


            return Ok<IEnumerable<SelectStructureLocations_Result>>((IEnumerable<SelectStructureLocations_Result>)db.SelectStructureLocations(new long?(key)).ToList());
        }
    }
}
