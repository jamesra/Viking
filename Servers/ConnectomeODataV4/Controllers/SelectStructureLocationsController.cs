using ConnectomeDataModel;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

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
        private readonly ConnectomeEntities _db;
        private readonly ILogger<SelectStructureLocationsController> _logger;
        private static readonly ODataValidationSettings _validationSettings = new ODataValidationSettings();

        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        public SelectStructureLocationsController(ConnectomeEntities db, ILogger<SelectStructureLocationsController> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: odata/SelectStructureLocations
        [EnableQuery(PageSize = WebApiConfig.PageSize)]
        public IHttpActionResult GetSelectStructureLocations(ODataQueryOptions<SelectStructureLocations_Result> queryOptions)
        {
            try
            {
                // validate the query.
                queryOptions.Validate(_validationSettings);
                
                _logger.LogInformation("Fetching all structure locations");
                _db.ConfigureAsReadOnly();
                return Ok<IList<Location>>(_db.SelectAllStructureLocations().ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all structure locations");
                return BadRequest(ex.Message);
            }
        }

        // GET: odata/SelectStructureLocations(5)
        public IHttpActionResult GetSelectStructureLocations_Result([FromODataUri] long key, ODataQueryOptions<SelectStructureLocations_Result> queryOptions)
        {
            try
            {
                // validate the query.
                queryOptions.Validate(_validationSettings);
                
                _logger.LogInformation("Fetching structure locations for structure ID {StructureId}", key);
                _db.ConfigureAsReadOnly();
                return Ok<IList<Location>>(_db.SelectStructureLocations(new long?(key)).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching structure locations for structure ID {StructureId}", key);
                return BadRequest(ex.Message);
            }
        }

        // No need for Dispose override - DI container handles disposal
    }
}
