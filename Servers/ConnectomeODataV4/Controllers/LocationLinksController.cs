using ConnectomeDataModel;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
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
    builder.EntitySet<LocationLink>("LocationLinks");
    builder.EntitySet<Location>("Locations"); 
    config.Routes.MapODataServiceRoute("odata", "odata", builder.GetEdmModel());
    */
    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public class LocationLinksController(ConnectomeEntities db, ILogger<LocationLinksController> logger) : ODataController
    {
        private readonly ConnectomeEntities _db = db ?? throw new ArgumentNullException(nameof(db));
        private readonly ILogger<LocationLinksController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // GET: odata/LocationLinks
        [EnableQuery(PageSize = WebApiConfig.PageSize)]
        public IQueryable<LocationLink> GetLocationLinks()
        {
            try
            {
                _logger.LogInformation("Fetching location links");
                _db.ConfigureAsReadOnly();
                return _db.LocationLinks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching location links");
                throw;
            }
        }

        // GET: odata/LocationLinks(5)
        [EnableQuery]
        public SingleResult<LocationLink> GetLocationLink([FromODataUri] long key)
        {
            try
            {
                _logger.LogInformation("Fetching location link with ID {LocationLinkId}", key);
                _db.ConfigureAsReadOnly();
                return SingleResult.Create(_db.LocationLinks.Where(locationLink => locationLink.A == key));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching location link with ID {LocationLinkId}", key);
                throw;
            }
        }

        /*

        // PUT: odata/LocationLinks(5)
        public async Task<IHttpActionResult> Put([FromODataUri] long key, Delta<LocationLink> patch)
        {
            Validate(patch.GetEntity());

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            LocationLink locationLink = await db.LocationLinks.FindAsync(key);
            if (locationLink is null)
            {
                return NotFound();
            }

            patch.Put(locationLink);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LocationLinkExists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Updated(locationLink);
        }

        // POST: odata/LocationLinks
        public async Task<IHttpActionResult> Post(LocationLink locationLink)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            db.LocationLinks.Add(locationLink);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (LocationLinkExists(locationLink.A))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }

            return Created(locationLink);
        }

        // PATCH: odata/LocationLinks(5)
        [AcceptVerbs("PATCH", "MERGE")]
        public async Task<IHttpActionResult> Patch([FromODataUri] long key, Delta<LocationLink> patch)
        {
            Validate(patch.GetEntity());

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            LocationLink locationLink = await db.LocationLinks.FindAsync(key);
            if (locationLink is null)
            {
                return NotFound();
            }

            patch.Patch(locationLink);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LocationLinkExists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Updated(locationLink);
        }

        // DELETE: odata/LocationLinks(5)
        public async Task<IHttpActionResult> Delete([FromODataUri] long key)
        {
            LocationLink locationLink = await db.LocationLinks.FindAsync(key);
            if (locationLink is null)
            {
                return NotFound();
            }

            db.LocationLinks.Remove(locationLink);
            await db.SaveChangesAsync();

            return StatusCode(HttpStatusCode.NoContent);
        }
        */

        // GET: odata/LocationLinks(5)/LocationA
        [EnableQuery]
        public SingleResult<Location> GetLocationA([FromODataUri] long key)
        {
            _db.ConfigureAsReadOnly();
            return SingleResult.Create(_db.LocationLinks.Where(m => m.A == key).Select(m => m.LocationA));
        }

        // GET: odata/LocationLinks(5)/LocationB
        [EnableQuery]
        public SingleResult<Location> GetLocationB([FromODataUri] long key)
        {
            _db.ConfigureAsReadOnly();
            return SingleResult.Create(_db.LocationLinks.Where(m => m.A == key).Select(m => m.LocationB));
        }

        // No need for Dispose override - DI container handles disposal

        private bool LocationLinkExists(long key) => _db.LocationLinks.Count(e => e.A == key) > 0;
    }
}
