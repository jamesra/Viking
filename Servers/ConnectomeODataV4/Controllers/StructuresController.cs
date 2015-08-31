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
    builder.EntitySet<Structure>("Structures");
    builder.EntitySet<Location>("Locations"); 
    builder.EntitySet<StructureType>("StructureTypes"); 
    builder.EntitySet<StructureLink>("StructureLinks"); 
    config.Routes.MapODataServiceRoute("odata", "odata", builder.GetEdmModel());
    */
    public class StructuresController : ODataController
    {
        private ConnectomeEntities db = new ConnectomeEntities();

        // GET: odata/Structures
        [EnableQuery(PageSize = 2048)]
        public IQueryable<Structure> GetStructures()
        {
            return db.Structures;
        }

        // GET: odata/Structures(5)
        [EnableQuery]
        public SingleResult<Structure> GetStructure([FromODataUri] long key)
        {
            return SingleResult.Create(db.Structures.Where(structure => structure.ID == key));
        }

        /*
        // PUT: odata/Structures(5)
        public async Task<IHttpActionResult> Put([FromODataUri] long key, Delta<Structure> patch)
        {
            Validate(patch.GetEntity());

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Structure structure = await db.Structures.FindAsync(key);
            if (structure == null)
            {
                return NotFound();
            }

            patch.Put(structure);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StructureExists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Updated(structure);
        }

        // POST: odata/Structures
        public async Task<IHttpActionResult> Post(Structure structure)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            db.Structures.Add(structure);
            await db.SaveChangesAsync();

            return Created(structure);
        }

        // PATCH: odata/Structures(5)
        [AcceptVerbs("PATCH", "MERGE")]
        public async Task<IHttpActionResult> Patch([FromODataUri] long key, Delta<Structure> patch)
        {
            Validate(patch.GetEntity());

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Structure structure = await db.Structures.FindAsync(key);
            if (structure == null)
            {
                return NotFound();
            }

            patch.Patch(structure);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StructureExists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Updated(structure);
        }

        // DELETE: odata/Structures(5)
        public async Task<IHttpActionResult> Delete([FromODataUri] long key)
        {
            Structure structure = await db.Structures.FindAsync(key);
            if (structure == null)
            {
                return NotFound();
            }

            db.Structures.Remove(structure);
            await db.SaveChangesAsync();

            return StatusCode(HttpStatusCode.NoContent);
        }
        */

        // GET: odata/Structures(5)/Locations
        [EnableQuery]
        public IQueryable<Location> GetLocations([FromODataUri] long key)
        {
            return db.Structures.Where(m => m.ID == key).SelectMany(m => m.Locations);
        }

        // GET: odata/Structures(5)/Children
        [EnableQuery]
        public IQueryable<Structure> GetChildren([FromODataUri] long key)
        {
            return db.Structures.Where(m => m.ID == key).SelectMany(m => m.Children);
        }

        // GET: odata/Structures(5)/Parent
        [EnableQuery]
        public SingleResult<Structure> GetParent([FromODataUri] long key)
        {
            return SingleResult.Create(db.Structures.Where(m => m.ID == key).Select(m => m.Parent));
        }

        // GET: odata/Structures(5)/Type
        [EnableQuery]
        public SingleResult<StructureType> GetType([FromODataUri] long key)
        {
            return SingleResult.Create(db.Structures.Where(m => m.ID == key).Select(m => m.Type));
        }

        // GET: odata/Structures(5)/SourceOfLinks
        [EnableQuery]
        public IQueryable<StructureLink> GetSourceOfLinks([FromODataUri] long key)
        {
            return db.Structures.Where(m => m.ID == key).SelectMany(m => m.SourceOfLinks);
        }

        // GET: odata/Structures(5)/TargetOfLinks
        [EnableQuery]
        public IQueryable<StructureLink> GetTargetOfLinks([FromODataUri] long key)
        {
            return db.Structures.Where(m => m.ID == key).SelectMany(m => m.TargetOfLinks);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private bool StructureExists(long key)
        {
            return db.Structures.Count(e => e.ID == key) > 0;
        }
    }
}
