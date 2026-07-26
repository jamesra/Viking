using System;
using ConnectomeDataModel;
using Microsoft.AspNet.OData.Batch;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.OData.Edm;
using System.Web.Http;

namespace ConnectomeODataV4
{
    public static class WebApiConfig
    {
        public const int PageSize = 16384;

        public static void Register(HttpConfiguration config)
        {

            // Web API configuration and services
            // Modern JSON serialization configuration
            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            json.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
            json.SerializerSettings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.Utc;

            // CORS configuration (managed via Web.config for IIS hosting)
            // For development, consider enabling:
            // var cors = new System.Web.Http.Cors.EnableCorsAttribute("*", "*", "*");
            // config.EnableCors(cors);

            // Enable detailed error messages in development only
            // config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always;

            // Web API routes 
            config.MapHttpAttributeRoutes();

            // Configure OData query options
            config.Count().Filter().OrderBy().Expand().Select().MaxTop(null);

            IEdmModel edmModel = GetModel();

            // Configure OData batch handler
            ODataBatchHandler odataBatchHandler = new DefaultODataBatchHandler(GlobalConfiguration.DefaultServer)
            {
                ODataRouteName = "odata"
            };

            // Map OData service route
            config.MapODataServiceRoute(
                routeName: "odata",
                routePrefix: null,
                model: edmModel,
                batchHandler: odataBatchHandler);

            // Fallback Web API route
            config.Routes.MapHttpRoute(
                name: "api",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }

        // NOTE: This method is not used and should be removed. Controllers should manage their own DbContext lifecycle.
        [Obsolete("This method creates a DbContext without proper disposal. Use dependency injection in controllers instead.")]
        public static System.Linq.IQueryable<LocationLink> StructureLocationLinks(long ID)
        {
            // This creates a memory leak - DbContext is never disposed
            ConnectomeEntities db = new();
            return db.StructureLocationLinks(ID);
        }


        public static Microsoft.OData.Edm.IEdmModel GetModel()
        {
            ODataConventionModelBuilder builder = new()
            {
                Namespace = "ConnectomeODataV4"
            };

            builder.EntitySet<StructureType>("StructureTypes");
            builder.EntitySet<Structure>("Structures");
            builder.EntitySet<Location>("Locations");

            AddStructureSpatialView(builder);

            AddScaleType(builder);
            AddStructureLinks(builder);
            AddPermittedStructureLinks(builder);
            AddLocationLinks(builder);
            AddNetworkFunctions(builder);
            AddDistinctLabelFunctions(builder);

            var edmModel = builder.GetEdmModel();
            AddStructureLocationLinks(builder, edmModel);
            AddLocation(edmModel);

            return edmModel;
        }

        private static void AddStructureSpatialView(ODataConventionModelBuilder builder)
        {
            var entitySet = builder.EntitySet<StructureSpatialCache>("StructureSpatialCaches");
            entitySet.EntityType.HasKey(entity => entity.ID);
        }

        private static void AddScaleType(ODataConventionModelBuilder builder)
        {
            builder.ComplexType<UnitsAndScale.Scale>().ComplexProperty<UnitsAndScale.IAxisUnits>(c => c.X);
            builder.ComplexType<UnitsAndScale.Scale>().ComplexProperty<UnitsAndScale.IAxisUnits>(c => c.Y);
            builder.ComplexType<UnitsAndScale.Scale>().ComplexProperty<UnitsAndScale.IAxisUnits>(c => c.Z);

            builder.Function("Scale").Returns<UnitsAndScale.Scale>();
        }

        private static Microsoft.OData.Edm.IEdmModel AddStructureLocationLinks(ODataConventionModelBuilder builder, IEdmModel edmModel)
        {
            EdmEntitySet structures = edmModel.EntityContainer.FindEntitySet("Structures") as EdmEntitySet;
            EdmEntitySet locationLinks = edmModel.EntityContainer.FindEntitySet("LocationLinks") as EdmEntitySet;
            EdmEntityType structType = structures.EntityType() as EdmEntityType;
            EdmEntityType locLinksType = locationLinks.EntityType() as EdmEntityType;

            EdmNavigationPropertyInfo structLocLinksProperty = new()
            {
                TargetMultiplicity = Microsoft.OData.Edm.EdmMultiplicity.Many,
                Target = locLinksType,
                ContainsTarget = true,
                OnDelete = Microsoft.OData.Edm.EdmOnDeleteAction.None,
                Name = "LocationLinks"
            };

            var navigationProperty = structType.AddUnidirectionalNavigation(structLocLinksProperty);
            structures.AddNavigationTarget(navigationProperty, locationLinks);

            return edmModel;
        }


        private static Microsoft.OData.Edm.IEdmModel AddLocation(IEdmModel edmModel)
        {
            EdmEntitySet locations = edmModel.EntityContainer.FindEntitySet("Locations") as EdmEntitySet;
            EdmEntitySet locationLinks = edmModel.EntityContainer.FindEntitySet("LocationLinks") as EdmEntitySet;
            EdmEntityType locationType = locations.EntityType() as EdmEntityType;
            EdmEntityType locLinksType = locationLinks.EntityType() as EdmEntityType;

            EdmNavigationPropertyInfo LocLinksProperty = new()
            {
                TargetMultiplicity = Microsoft.OData.Edm.EdmMultiplicity.Many,
                Target = locLinksType,
                ContainsTarget = true,
                OnDelete = Microsoft.OData.Edm.EdmOnDeleteAction.None,
                Name = "LocationLinks"
            };

            var navigationProperty = locationType.AddUnidirectionalNavigation(LocLinksProperty);
            locations.AddNavigationTarget(navigationProperty, locationLinks);

            return edmModel;
        }

        public static void AddStructureLinks(ODataModelBuilder builder)
        {
            var type = builder.EntityType<StructureLink>();
            type.HasKey(sl => new { sl.SourceID, sl.TargetID, sl.Bidirectional });
            builder.EntitySet<StructureLink>("StructureLinks");
        }

        public static void AddPermittedStructureLinks(ODataModelBuilder builder)
        {
            var type = builder.EntityType<PermittedStructureLink>();
            // Composite key must be defined in a single HasKey call
            type.HasKey(sl => new { sl.SourceTypeID, sl.TargetTypeID });
            builder.EntitySet<PermittedStructureLink>("PermittedStructureLinks");
        }

        public static void AddLocationLinks(ODataModelBuilder builder)
        {
            var type = builder.EntityType<LocationLink>();
            // Composite key must be defined in a single HasKey call
            type.HasKey(sl => new { sl.A, sl.B });
            builder.EntitySet<LocationLink>("LocationLinks");
        }

        public static void AddDistinctLabelFunctions(ODataModelBuilder builder)
        {

            var Distinct = builder.EntityType<Structure>().Collection.Function("DistinctLabels");

            Distinct.ReturnsCollection<string>();
        }


        public static void AddNetworkFunctions(ODataModelBuilder builder)
        {
            //builder.EntitySet<Structure>("Structures");

            var NetworkIDsFuncConfig = builder.Function("Network");
            NetworkIDsFuncConfig.CollectionParameter<long>("IDs");
            NetworkIDsFuncConfig.Parameter<int>("Hops");
            NetworkIDsFuncConfig.ReturnsCollectionFromEntitySet<Structure>("Structures");
            NetworkIDsFuncConfig.Namespace = null;
            /*            
                        var NetworkCellsFuncConfig = builder.Function("NetworkCells");
                        NetworkCellsFuncConfig.CollectionParameter<long>("IDs");
                        NetworkCellsFuncConfig.Parameter<int>("Hops");
                        NetworkCellsFuncConfig.ReturnsCollectionFromEntitySet<Structure>("Structures");
                        NetworkCellsFuncConfig.Namespace = null;
            */
            /*
            var StructuresNetworkFuncConfig = builder.EntityType<Structure>().Collection.Function("Network");
            StructuresNetworkFuncConfig.CollectionParameter<long>("IDs");
            StructuresNetworkFuncConfig.Parameter<int>("Hops");
            StructuresNetworkFuncConfig.ReturnsCollectionFromEntitySet<Structure>("Structures");
            StructuresNetworkFuncConfig.Namespace = null;
            */

            var NetworkChildStructuresFuncConfig = builder.Function("NetworkChildStructures");
            NetworkChildStructuresFuncConfig.CollectionParameter<long>("IDs");
            NetworkChildStructuresFuncConfig.Parameter<int>("Hops");
            NetworkChildStructuresFuncConfig.ReturnsCollectionFromEntitySet<Structure>("Structures");
            NetworkChildStructuresFuncConfig.Namespace = null;

            var NetworkStructureLinksFuncConfig = builder.Function("NetworkLinks");
            NetworkStructureLinksFuncConfig.CollectionParameter<long>("IDs");
            NetworkStructureLinksFuncConfig.Parameter<int>("Hops");
            NetworkStructureLinksFuncConfig.ReturnsCollectionFromEntitySet<StructureLink>("StructureLinks");
            NetworkStructureLinksFuncConfig.Namespace = null;

            var StructuresLocationLinkFuncConfig = builder.Function("StructureLocationLinks");
            StructuresLocationLinkFuncConfig.Parameter<long>("StructureID");
            StructuresLocationLinkFuncConfig.ReturnsCollectionFromEntitySet<LocationLink>("LocationLinks");
            StructuresLocationLinkFuncConfig.Namespace = null;

            var NetworkChildStructuresSpatialDataFuncConfig = builder.Function("NetworkEdgeSpatialData");
            NetworkChildStructuresSpatialDataFuncConfig.CollectionParameter<long>("IDs");
            NetworkChildStructuresSpatialDataFuncConfig.Parameter<int>("Hops");
            NetworkChildStructuresSpatialDataFuncConfig.ReturnsCollectionFromEntitySet<StructureSpatialCache>("StructureSpatialCaches");
            NetworkChildStructuresSpatialDataFuncConfig.Namespace = null;

            var NetworkStructuresSpatialDataFuncConfig = builder.Function("NetworkSpatialData");
            NetworkStructuresSpatialDataFuncConfig.CollectionParameter<long>("IDs");
            NetworkStructuresSpatialDataFuncConfig.Parameter<int>("Hops");
            NetworkStructuresSpatialDataFuncConfig.ReturnsCollectionFromEntitySet<StructureSpatialCache>("StructureSpatialCaches");
            NetworkStructuresSpatialDataFuncConfig.Namespace = null;
        }
    }
}
