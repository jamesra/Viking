using System.Data;
using System.Linq;
using System.Web.Http;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Routing;
using ConnectomeDataModel;
using System.Collections;
using System.Collections.Generic;

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
    public class StructureSpatialCachesController : ODataController
    {
        private ConnectomeEntities db = new ConnectomeEntities();

        // GET: odata/StructureSpatialCaches
        [EnableQuery(PageSize = 2048)]
        public IQueryable<StructureSpatialCache> GetStructureSpatialCaches()
        {
            return db.StructureSpatialCaches;
        }

        // GET: odata/StructureSpatialCaches(5)
        [EnableQuery]
        public SingleResult<StructureSpatialCache> GetStructureSpatialCache([FromODataUri] long key)
        {
            return SingleResult.Create(db.StructureSpatialCaches.Where(structure => structure.ID == key));
        }

        /// <summary>
        /// Return the ODataPath we need to set on requests when invoking functions that return collections of entities
        /// </summary>
        /// <returns></returns>
        private ODataPath GetRequestPath()
        {
            //return Request.ODataProperties().Path;
            
            return new DefaultODataPathHandler().Parse(System.Web.HttpContext.Current.Request.Url.GetLeftPart(System.UriPartial.Path),
                                                                 "StructureSpatialCaches",
                                                                 Request.GetRequestContainer());
                                                                 
        }
        
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
            Geometry.Scale scale = VikingWebAppSettings.AppSettings.GetScale();
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
        [ODataRoute("NetworkSpatialData(IDs={IDs},Hops={Hops})")]
        public IQueryable<StructureSpatialCache> GetNetwork([FromODataUri] ICollection<long> IDs, [FromODataUri] int Hops)
        {
            db.ConfigureAsReadOnly();
            Request.ODataProperties().Path = GetRequestPath();
            return db.SelectNetworkStructureSpatialData(IDs, Hops);
        }

        [HttpGet]
        [EnableQuery()]
        [ODataRoute("NetworkSpatialData()")]
        public IQueryable<StructureSpatialCache> GetNetwork()
        {
            db.ConfigureAsReadOnly();
            Request.ODataProperties().Path = GetRequestPath();
            long[] IDs = db.GetLinkedStructureParentIDs().ToArray();
            return db.SelectNetworkStructureSpatialData(IDs, 0);
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
        [ODataRoute("NetworkEdgeSpatialData(IDs={IDs},Hops={Hops})")]
        public IQueryable<StructureSpatialCache> GetNetworkChildren([FromODataUri] long[] IDs, [FromODataUri] int Hops)
        {
            db.ConfigureAsReadOnly();
            Request.ODataProperties().Path = GetRequestPath();
            return db.SelectNetworkChildStructureSpatialData(IDs, Hops);

            // https://github.com/OData/WebApi/issues/255 
        }

        [HttpGet]
        [EnableQuery()]
        [ODataRoute("NetworkEdgeSpatialData()")]
        public IQueryable<StructureSpatialCache> GetNetworkChildren()
        {
            db.ConfigureAsReadOnly();
            Request.ODataProperties().Path = GetRequestPath();
            long[] IDs = db.GetLinkedStructureParentIDs().ToArray();
            return db.SelectNetworkChildStructureSpatialData(IDs, 0);
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
