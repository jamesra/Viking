using System.Web.Http;
using System.Web.Http.Batch;
using System.Web.OData.Builder;
using System.Web.OData.Extensions;
using System.Web.OData.Batch;
using ConnectomeDataModel;
using Microsoft.OData.Edm.Library;
using Microsoft.OData.Edm;
 

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
            config.EnableUnqualifiedNameCall(true);

            config.MapHttpAttributeRoutes();

            Microsoft.OData.Edm.IEdmModel edmModel = GetModel();

            ODataBatchHandler odataBatchHandler = new DefaultODataBatchHandler(GlobalConfiguration.DefaultServer);
            odataBatchHandler.ODataRouteName = "odata";

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
            ODataConventionModelBuilder builder = new ODataConventionModelBuilder();

            builder.Namespace = "ConnectomeODataV4";

            builder.EntitySet<StructureType>("StructureTypes");
            builder.EntitySet<Structure>("Structures");
            builder.EntitySet<Location>("Locations");
            
            AddScaleType(builder);
            AddStructureLinks(builder);
            AddPermittedStructureLinks(builder);
            AddLocationLinks(builder);
            AddFunctions(builder);

            var edmModel = builder.GetEdmModel();
            AddStructureLocationLinks(edmModel);
            AddLocation(edmModel);

            
            return edmModel;
        }

        private static void AddScaleType(ODataConventionModelBuilder builder)
        {
            builder.ComplexType<Geometry.AxisUnits>().Property(c => c.Units);
            builder.ComplexType<Geometry.AxisUnits>().Property<double>(c => c.Value);
            builder.ComplexType<Geometry.Scale>().ComplexProperty<Geometry.AxisUnits>(c => c.X);
            builder.ComplexType<Geometry.Scale>().ComplexProperty<Geometry.AxisUnits>(c => c.Y);
            builder.ComplexType<Geometry.Scale>().ComplexProperty<Geometry.AxisUnits>(c => c.Z);

            builder.Function("Scale").Returns<Geometry.Scale>();
        }
         
        private static Microsoft.OData.Edm.IEdmModel AddStructureLocationLinks(IEdmModel edmModel)
        { 
            
            var structures = edmModel.EntityContainer.FindEntitySet("Structures") as EdmEntitySet;
            var locationLinks = edmModel.EntityContainer.FindEntitySet("LocationLinks") as EdmEntitySet;
            var structType = structures.EntityType() as EdmEntityType;
            var locLinksType = locationLinks.EntityType() as EdmEntityType;

            var structLocLinksProperty = new EdmNavigationPropertyInfo();
            structLocLinksProperty.TargetMultiplicity = Microsoft.OData.Edm.EdmMultiplicity.Many;
            structLocLinksProperty.Target = locLinksType;
            structLocLinksProperty.ContainsTarget = true; 
            structLocLinksProperty.OnDelete = Microsoft.OData.Edm.EdmOnDeleteAction.None;
            structLocLinksProperty.Name = "LocationLinks";
            
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

            var LocLinksProperty = new EdmNavigationPropertyInfo();
            LocLinksProperty.TargetMultiplicity = Microsoft.OData.Edm.EdmMultiplicity.Many;
            LocLinksProperty.Target = locLinksType;
            LocLinksProperty.ContainsTarget = true;
            LocLinksProperty.OnDelete = Microsoft.OData.Edm.EdmOnDeleteAction.None;
            LocLinksProperty.Name = "LocationLinks";

            var navigationProperty = locationType.AddUnidirectionalNavigation(LocLinksProperty);
            locations.AddNavigationTarget(navigationProperty, locationLinks);

            return edmModel;
        }

        public static void AddStructureLinks(ODataModelBuilder builder)
        {
            var type = builder.EntityType<StructureLink>();
            type.HasKey(sl => sl.SourceID);
            type.HasKey(sl => sl.TargetID);
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
        
        public static void AddFunctions(ODataModelBuilder builder)
        {
            builder.EntitySet<Structure>("Structures");
            
            var NetworkIDsFuncConfig = builder.Function("Network");
            NetworkIDsFuncConfig.CollectionParameter<long>("IDs");
            NetworkIDsFuncConfig.Parameter<int>("Hops");
            NetworkIDsFuncConfig.ReturnsCollection<long>();
            
            var NetworkCellsFuncConfig = builder.Function("NetworkCells");
            NetworkCellsFuncConfig.CollectionParameter<long>("IDs");
            NetworkCellsFuncConfig.Parameter<int>("Hops");
            NetworkCellsFuncConfig.ReturnsCollectionFromEntitySet<Structure>("Structures");

            var StructuresNetworkFuncConfig = builder.EntityType<Structure>().Collection.Function("Network");
            StructuresNetworkFuncConfig.CollectionParameter<long>("IDs");
            StructuresNetworkFuncConfig.Parameter<int>("Hops");
            StructuresNetworkFuncConfig.ReturnsCollectionFromEntitySet<Structure>("Structures");

            var NetworkChildStructuresFuncConfig = builder.EntityType<Structure>().Collection.Function("NetworkChildStructures");
            NetworkChildStructuresFuncConfig.CollectionParameter<long>("IDs");
            NetworkChildStructuresFuncConfig.Parameter<int>("Hops");
            NetworkChildStructuresFuncConfig.ReturnsCollectionFromEntitySet<Structure>("Structures");

            var StructureLinksNetworkFuncConfig = builder.EntityType<StructureLink>().Collection.Function("Network");
            StructureLinksNetworkFuncConfig.CollectionParameter<long>("IDs");
            StructureLinksNetworkFuncConfig.Parameter<int>("Hops");
            StructureLinksNetworkFuncConfig.ReturnsCollectionFromEntitySet<StructureLink>("StructureLinks");
        }
    }
}
