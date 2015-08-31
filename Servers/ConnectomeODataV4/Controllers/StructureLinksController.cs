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
    builder.EntitySet<StructureLink>("StructureLinks");
    builder.EntitySet<Structure>("Structures"); 
    config.Routes.MapODataServiceRoute("odata", "odata", builder.GetEdmModel());
    */
    public class StructureLinksController : ODataController
    {
        private ConnectomeEntities db = new ConnectomeEntities();

        // GET: odata/StructureLinks
        [EnableQuery(PageSize = WebApiConfig.PageSize)]
        public IQueryable<StructureLink> GetStructureLinks()
        {
            StructureLink[] sl = db.StructureLinks.ToArray();
            return db.StructureLinks;
        }

        // GET: odata/StructureLinks(5)
        [EnableQuery]
        public SingleResult<StructureLink> GetStructureLink([FromODataUri] long key)
        {
            return SingleResult.Create(db.StructureLinks.Where(structureLink => structureLink.SourceID == key));
        }

        /*
        // PUT: odata/StructureLinks(5)
        public async Task<IHttpActionResult> Put([FromODataUri] long key, Delta<StructureLink> patch)
        {
            Validate(patch.GetEntity());

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            StructureLink structureLink = await db.StructureLinks.FindAsync(key);
            if (structureLink == null)
            {
                return NotFound();
            }

            patch.Put(structureLink);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StructureLinkExists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Updated(structureLink);
        }

        // POST: odata/StructureLinks
        public async Task<IHttpActionResult> Post(StructureLink structureLink)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            db.StructureLinks.Add(structureLink);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (StructureLinkExists(structureLink.SourceID))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }

            return Created(structureLink);
        }

        // PATCH: odata/StructureLinks(5)
        [AcceptVerbs("PATCH", "MERGE")]
        public async Task<IHttpActionResult> Patch([FromODataUri] long key, Delta<StructureLink> patch)
        {
            Validate(patch.GetEntity());

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            StructureLink structureLink = await db.StructureLinks.FindAsync(key);
            if (structureLink == null)
            {
                return NotFound();
            }

            patch.Patch(structureLink);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StructureLinkExists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Updated(structureLink);
        }

        // DELETE: odata/StructureLinks(5)
        public async Task<IHttpActionResult> Delete([FromODataUri] long key)
        {
            StructureLink structureLink = await db.StructureLinks.FindAsync(key);
            if (structureLink == null)
            {
                return NotFound();
            }

            db.StructureLinks.Remove(structureLink);
            await db.SaveChangesAsync();

            return StatusCode(HttpStatusCode.NoContent);
        }
        */

        // GET: odata/StructureLinks(5)/Source
        [EnableQuery]
        public SingleResult<Structure> GetSource([FromODataUri] long key)
        {
            return SingleResult.Create(db.StructureLinks.Where(m => m.SourceID == key).Select(m => m.Source));
        }

        // GET: odata/StructureLinks(5)/Target
        [EnableQuery]
        public SingleResult<Structure> GetTarget([FromODataUri] long key)
        {
            return SingleResult.Create(db.StructureLinks.Where(m => m.SourceID == key).Select(m => m.Target));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private bool StructureLinkExists(long key)
        {
            return db.StructureLinks.Count(e => e.SourceID == key) > 0;
        }
    }
}
