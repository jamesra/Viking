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
using System.Web.Http.ModelBinding;
using System.Web.Http.OData;
using System.Web.Http.OData.Routing;
using ConnectomeDataModel;

namespace ConnectomeODataV3.Controllers
{
    /*
    The WebApiConfig class may require additional changes to add a route for this controller. Merge these statements into the Register method of the WebApiConfig class as applicable. Note that OData URLs are case sensitive.

    using System.Web.Http.OData.Builder;
    using System.Web.Http.OData.Extensions;
    using ConnectomeODataV3.Models;
    ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
    builder.EntitySet<StructureType>("StructureTypes");
    builder.EntitySet<Structure>("Structures"); 
    config.Routes.MapODataServiceRoute("odata", "odata", builder.GetEdmModel());
    */
    public class StructureTypesController : ODataController
    {
        private ConnectomeEntities db = new ConnectomeEntities();

        // GET: odata/StructureTypes
        [EnableQuery]
        public IQueryable<StructureType> GetStructureTypes()
        {
            return db.StructureTypes;
        }

        // GET: odata/StructureTypes(5)
        [EnableQuery]
        public SingleResult<StructureType> GetStructureType([FromODataUri] long key)
        {
            return SingleResult.Create(db.StructureTypes.Where(structureType => structureType.ID == key));
        }

        /*
        // PUT: odata/StructureTypes(5)
        public async Task<IHttpActionResult> Put([FromODataUri] long key, Delta<StructureType> patch)
        {
            Validate(patch.GetEntity());

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            StructureType structureType = await db.StructureTypes.FindAsync(key);
            if (structureType == null)
            {
                return NotFound();
            }

            patch.Put(structureType);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StructureTypeExists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Updated(structureType);
        }

        // POST: odata/StructureTypes
        public async Task<IHttpActionResult> Post(StructureType structureType)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            db.StructureTypes.Add(structureType);
            await db.SaveChangesAsync();

            return Created(structureType);
        }

        // PATCH: odata/StructureTypes(5)
        [AcceptVerbs("PATCH", "MERGE")]
        public async Task<IHttpActionResult> Patch([FromODataUri] long key, Delta<StructureType> patch)
        {
            Validate(patch.GetEntity());

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            StructureType structureType = await db.StructureTypes.FindAsync(key);
            if (structureType == null)
            {
                return NotFound();
            }

            patch.Patch(structureType);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StructureTypeExists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Updated(structureType);
        }

        // DELETE: odata/StructureTypes(5)
        public async Task<IHttpActionResult> Delete([FromODataUri] long key)
        {
            StructureType structureType = await db.StructureTypes.FindAsync(key);
            if (structureType == null)
            {
                return NotFound();
            }

            db.StructureTypes.Remove(structureType);
            await db.SaveChangesAsync();

            return StatusCode(HttpStatusCode.NoContent);
        }
        */

        // GET: odata/StructureTypes(5)/Structures
        [EnableQuery]
        public IQueryable<Structure> GetStructures([FromODataUri] long key)
        {
            return db.StructureTypes.Where(m => m.ID == key).SelectMany(m => m.Structures);
        }

        // GET: odata/StructureTypes(5)/Children
        [EnableQuery]
        public IQueryable<StructureType> GetChildren([FromODataUri] long key)
        {
            return db.StructureTypes.Where(m => m.ID == key).SelectMany(m => m.Children);
        }

        // GET: odata/StructureTypes(5)/Parent
        [EnableQuery]
        public SingleResult<StructureType> GetParent([FromODataUri] long key)
        {
            return SingleResult.Create(db.StructureTypes.Where(m => m.ID == key).Select(m => m.Parent));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private bool StructureTypeExists(long key)
        {
            return db.StructureTypes.Count(e => e.ID == key) > 0;
        }
    }
}
