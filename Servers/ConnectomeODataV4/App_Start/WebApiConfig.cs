﻿//using System.Web.Http.Batch;
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
            var json = GlobalConfiguration.Configuration.Formatters.JsonFormatter;
            json.UseDataContractJsonSerializer = true;
            //json.SerializerSettings.PreserveReferencesHandling = Newtonsoft.Json.PreserveReferencesHandling.All;

            //var cors = new System.Web.Http.Cors.EnableCorsAttribute("*", "*", "*");
            //config.EnableCors(cors);

            //config.EnableSystemDiagnosticsTracing();

            //config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always;

            // Web API routes 
            //config.EnableUnqualifiedNameCall(true);

            config.MapHttpAttributeRoutes();
            config.Count().Filter().OrderBy().Expand().Select().MaxTop(null);

            Microsoft.OData.Edm.IEdmModel edmModel = GetModel();

            ODataBatchHandler odataBatchHandler = new DefaultODataBatchHandler(GlobalConfiguration.DefaultServer)
            {
                ODataRouteName = "odata"
            };

            config.MapODataServiceRoute(routeName: "odata",
                routePrefix: null,
                model: edmModel,
                batchHandler: odataBatchHandler);
                         
            config.Routes.MapHttpRoute(
                name: "api",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            
        }

        public static System.Linq.IQueryable<LocationLink> StructureLocationLinks(long ID)
        {
            ConnectomeEntities db = new ConnectomeEntities();
            return db.StructureLocationLinks(ID);
        }


        public static Microsoft.OData.Edm.IEdmModel GetModel()
        {
            ODataConventionModelBuilder builder = new ODataConventionModelBuilder
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
            builder.ComplexType<UnitsAndScale.AxisUnits>().Property(c => c.Units);
            builder.ComplexType<UnitsAndScale.AxisUnits>().Property<double>(c => c.Value);
            builder.ComplexType<UnitsAndScale.Scale>().ComplexProperty<UnitsAndScale.IAxisUnits>(c => c.X);
            builder.ComplexType<UnitsAndScale.Scale>().ComplexProperty<UnitsAndScale.IAxisUnits>(c => c.Y);
            builder.ComplexType<UnitsAndScale.Scale>().ComplexProperty<UnitsAndScale.IAxisUnits>(c => c.Z);

            builder.Function("Scale").Returns<UnitsAndScale.Scale>();
        }
         
        private static Microsoft.OData.Edm.IEdmModel AddStructureLocationLinks(ODataConventionModelBuilder builder, IEdmModel edmModel)
        {  
            var structures = edmModel.EntityContainer.FindEntitySet("Structures") as EdmEntitySet;
            var locationLinks = edmModel.EntityContainer.FindEntitySet("LocationLinks") as EdmEntitySet;
            var structType = structures.EntityType() as EdmEntityType;
            var locLinksType = locationLinks.EntityType() as EdmEntityType;

            var structLocLinksProperty = new EdmNavigationPropertyInfo
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
            var locations = edmModel.EntityContainer.FindEntitySet("Locations") as EdmEntitySet;
            var locationLinks = edmModel.EntityContainer.FindEntitySet("LocationLinks") as EdmEntitySet;
            var locationType = locations.EntityType() as EdmEntityType;
            var locLinksType = locationLinks.EntityType() as EdmEntityType;

            var LocLinksProperty = new EdmNavigationPropertyInfo
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
            type.HasKey(sl => sl.SourceTypeID);
            type.HasKey(sl => sl.TargetTypeID);
            builder.EntitySet<PermittedStructureLink>("PermittedStructureLinks");
        }

        public static void AddLocationLinks(ODataModelBuilder builder)
        {
            var type = builder.EntityType<LocationLink>();
            type.HasKey(sl => sl.A);
            type.HasKey(sl => sl.B);
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
