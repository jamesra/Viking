using System.Data;
using System.Linq;
using System.Web.Http;
using System.Web.OData;
using ConnectomeODataV4.Models;

namespace ConnectomeODataV4.Controllers
{
    /*
    The WebApiConfig class may require additional changes to add a route for this controller. Merge these statements into the Register method of the WebApiConfig class as applicable. Note that OData URLs are case sensitive.

    using System.Web.Http.OData.Builder;
    using System.Web.Http.OData.Extensions;
    using ConnectomeODataV4.Models;
    ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
    builder.EntitySet<Location>("Locations");
    builder.EntitySet<Structure>("Structures"); 
    builder.EntitySet<LocationLink>("LocationLinks"); 
    config.Routes.MapODataServiceRoute("odata", "odata", builder.GetEdmModel());
    */
    public class LocationsController : ODataController
    {
        private ConnectomeEntities db = new ConnectomeEntities();

        // GET: odata/Locations
        [EnableQuery(PageSize = WebApiConfig.PageSize)]
        public IQueryable<Location> GetLocations()
        {
            return db.Locations;
        }

        // GET: odata/Locations(5)
        [EnableQuery]
        public SingleResult<Location> GetLocation([FromODataUri] long key)
        {
            return SingleResult.Create(db.Locations.Where(location => location.ID == key));
        }

        /*
        // PUT: odata/Locations(5)
        public async Task<IHttpActionResult> Put([FromODataUri] long key, Delta<Location> patch)
        {
            Validate(patch.GetEntity());

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Location location = await db.Locations.FindAsync(key);
            if (location == null)
            {
                return NotFound();
            }

            patch.Put(location);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LocationExists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Updated(location);
        }

        // POST: odata/Locations
        public async Task<IHttpActionResult> Post(Location location)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            db.Locations.Add(location);
            await db.SaveChangesAsync();

            return Created(location);
        }

        // PATCH: odata/Locations(5)
        [AcceptVerbs("PATCH", "MERGE")]
        public async Task<IHttpActionResult> Patch([FromODataUri] long key, Delta<Location> patch)
        {
            Validate(patch.GetEntity());

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Location location = await db.Locations.FindAsync(key);
            if (location == null)
            {
                return NotFound();
            }

            patch.Patch(location);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LocationExists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Updated(location);
        }

        // DELETE: odata/Locations(5)
        public async Task<IHttpActionResult> Delete([FromODataUri] long key)
        {
            Location location = await db.Locations.FindAsync(key);
            if (location == null)
            {
                return NotFound();
            }

            db.Locations.Remove(location);
            await db.SaveChangesAsync();

            return StatusCode(HttpStatusCode.NoContent);
        }

    */


        // GET: odata/Locations(5)/Structure
        [EnableQuery]
        public SingleResult<Structure> GetStructure([FromODataUri] long key)
        {
            return SingleResult.Create(db.Locations.Where(m => m.ID == key).Select(m => m.Structure));
        }

        // GET: odata/Locations(5)/LocationLinksA
        [EnableQuery]
        public IQueryable<LocationLink> GetLocationLinksA([FromODataUri] long key)
        {
            return db.Locations.Where(m => m.ID == key).SelectMany(m => m.LocationLinksA);
        }

        // GET: odata/Locations(5)/LocationLinksB
        [EnableQuery]
        public IQueryable<LocationLink> GetLocationLinksB([FromODataUri] long key)
        {
            return db.Locations.Where(m => m.ID == key).SelectMany(m => m.LocationLinksB);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private bool LocationExists(long key)
        {
            return db.Locations.Count(e => e.ID == key) > 0;
        }
    }
}
