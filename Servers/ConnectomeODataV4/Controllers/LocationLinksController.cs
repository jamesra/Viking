using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.ModelBinding;
using System.Web.OData;
using System.Web.OData.Routing;
using ConnectomeODataV4.Models;

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
    public class LocationLinksController : ODataController
    {
        private ConnectomeEntities db = new ConnectomeEntities();

        // GET: odata/LocationLinks
        [EnableQuery(PageSize = 2048)]
        public IQueryable<LocationLink> GetLocationLinks()
        {
            return db.LocationLinks;
        }

        // GET: odata/LocationLinks(5)
        [EnableQuery]
        public SingleResult<LocationLink> GetLocationLink([FromODataUri] long key)
        {
            return SingleResult.Create(db.LocationLinks.Where(locationLink => locationLink.A == key));
        }

        // PUT: odata/LocationLinks(5)
        public async Task<IHttpActionResult> Put([FromODataUri] long key, Delta<LocationLink> patch)
        {
            Validate(patch.GetEntity());

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            LocationLink locationLink = await db.LocationLinks.FindAsync(key);
            if (locationLink == null)
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
            if (locationLink == null)
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
            if (locationLink == null)
            {
                return NotFound();
            }

            db.LocationLinks.Remove(locationLink);
            await db.SaveChangesAsync();

            return StatusCode(HttpStatusCode.NoContent);
        }

        // GET: odata/LocationLinks(5)/LocationA
        [EnableQuery]
        public SingleResult<Location> GetLocationA([FromODataUri] long key)
        {
            return SingleResult.Create(db.LocationLinks.Where(m => m.A == key).Select(m => m.LocationA));
        }

        // GET: odata/LocationLinks(5)/LocationB
        [EnableQuery]
        public SingleResult<Location> GetLocationB([FromODataUri] long key)
        {
            return SingleResult.Create(db.LocationLinks.Where(m => m.A == key).Select(m => m.LocationB));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private bool LocationLinkExists(long key)
        {
            return db.LocationLinks.Count(e => e.A == key) > 0;
        }
    }
}
