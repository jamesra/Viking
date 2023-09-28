﻿using ConnectomeDataModel;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Routing;
using System.Collections.Generic;
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
    builder.EntitySet<Structure>("Structures");
    builder.EntitySet<Location>("Locations"); 
    builder.EntitySet<StructureType>("StructureTypes"); 
    builder.EntitySet<StructureLink>("StructureLinks"); 
    config.Routes.MapODataServiceRoute("odata", "odata", builder.GetEdmModel());
    */
    public class StructuresController : ODataController
    {
        private readonly ConnectomeEntities db = new ConnectomeEntities();

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

        /// <summary>
        /// Return the ODataPath we need to set on requests when invoking functions that return collections of entities
        /// </summary>
        /// <returns></returns>
        private ODataPath GetRequestPath()
        {
            /*return new DefaultODataPathHandler().Parse(System.Web.HttpContext.Current.Request.Url.GetLeftPart(System.UriPartial.Path),
                                                                 "Structures",
                                                                 Request.ODataProperties().Path);*/
            return new DefaultODataPathHandler().Parse(System.Web.HttpContext.Current.Request.Url.GetLeftPart(System.UriPartial.Path),
                                                                 "Structures",
                                                                 Request.GetRequestContainer());
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
        [HttpGet]
        [EnableQuery]
        public IQueryable<Location> GetLocations([FromODataUri] long key)
        {
            return db.Structures.Where(m => m.ID == key).SelectMany(m => m.Locations);
        }

        // GET: odata/Structures(5)/LocationLinks
        [EnableQuery]
        public IQueryable<LocationLink> GetLocationLinks([FromODataUri] long key)
        {
            return db.StructureLocationLinks(key);
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

        [HttpGet]
        [ODataRoute("Scale()")]
        public IHttpActionResult GetScale()
        {
            UnitsAndScale.Scale scale = VikingWebAppSettings.AppSettings.GetScale();
            return Ok(scale);
        }

        // GET: odata/StructureLocationLinks
        [HttpGet]
        [EnableQuery]
        [ODataRoute("StructureLocationLinks(StructureID={key})")]
        public IQueryable<LocationLink> StructureLocationLinks([FromODataUri] long key)
        {
            db.ConfigureAsReadOnly();
            return db.StructureLocationLinks(key);
        }
        
        /*
        [HttpGet]
        [EnableQuery]
        [ODataRoute("LocationLinks")]
        public IQueryable<LocationLink> LocationLinks([FromODataUri] long key)
        {
            return StructureLocationLinks(key);
        }
        */


        [HttpGet]
        [EnableQuery()]
        [ODataRoute("Network(IDs={IDs},Hops={Hops})")]
        public IQueryable<Structure> GetNetwork([FromODataUri] ICollection<long> IDs, [FromODataUri] int Hops)
        {
            db.ConfigureAsReadOnly();
            Request.ODataProperties().Path = GetRequestPath();
            return db.SelectNetworkStructures(IDs, Hops);
        }

        /*
        [HttpGet]
        [EnableQuery()]
        [ODataRoute("NetworkCells(IDs={IDs},Hops={Hops})")]
        public IQueryable<Structure> GetNetworkCells([FromODataUri] ICollection<long> IDs, [FromODataUri] int Hops)
        {
            Request.ODataProperties().Path = GetRequestPath(); 
            return db.SelectNetworkStructures(IDs, Hops);
        }
        */

        /*
        [HttpGet]
        [EnableQuery(PageSize = 2048)]
        //[ODataRoute("Structures/Network(IDs={IDs},Hops={Hops})")]
        //[ODataRoute("Network(IDs={IDs},Hops={Hops})")]
        public IQueryable<Structure> Network([FromODataUri] long[] IDs, [FromODataUri] int Hops)
        {
            //db.ConfigureAsReadOnly();

            IQueryable<Structure> Structures = db.SelectNetworkStructures(IDs, Hops);

            // https://github.com/OData/WebApi/issues/255
            
            //ODataPath path = new DefaultODataPathHandler().Parse(System.Web.HttpContext.Current.Request.Url.GetLeftPart(System.UriPartial.Path), "Structures");

            //Request.ODataProperties().Path = path;

            return Structures;
        }
        */
    

        [HttpGet]
        [EnableQuery()]
        [ODataRoute("NetworkChildStructures(IDs={IDs},Hops={Hops})")]
        public IQueryable<Structure> GetNetworkChildren([FromODataUri] long[] IDs, [FromODataUri] int Hops)
        {
            db.ConfigureAsReadOnly();
            Request.ODataProperties().Path = GetRequestPath();
            return db.SelectNetworkChildStructures(IDs, Hops);

            // https://github.com/OData/WebApi/issues/255 
             
        }

        [HttpGet]
        [EnableQuery()]
        public IQueryable<string> DistinctLabels(ODataActionParameters parameters)
        {
            db.ConfigureAsReadOnly();
            Request.ODataProperties().Path = GetRequestPath();
            return db.Structures.Select(s => s.Label).Distinct();
            
            // https://github.com/OData/WebApi/issues/255

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
