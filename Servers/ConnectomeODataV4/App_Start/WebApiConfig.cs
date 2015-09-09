using System.Web.Http;
using System.Web.OData.Builder;
using System.Web.OData.Extensions;
using ConnectomeDataModel;
using Microsoft.OData.Edm.Library;
using Microsoft.OData;
using Microsoft.OData.Edm;
using System.Collections.Generic; 
 

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

            // Web API routes 
            config.MapHttpAttributeRoutes();

            Microsoft.OData.Edm.IEdmModel edmModel = GetModel();
            
            config.MapODataServiceRoute(routeName: "odata",
                routePrefix: null,
                model: edmModel);
            
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
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
            //    ODataModelBuilder builder = new ODataModelBuilder();
            //    var edmModel = builder.GetEdmModel();
            ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
            builder.EntitySet<StructureType>("StructureTypes");
            var struct_config = builder.EntitySet<Structure>("Structures");
            builder.EntitySet<Location>("Locations");


            AddStructureLinks(builder);
            AddLocationLinks(builder);

            return AddStructure(builder);
            //builder.EntitySet<SelectStructureLocations_Result>("SelectStructureLocations");

            // var collection = struct_config.EntityType.CollectionProperty(s => WebApiConfig.StructureLocationLinks(s.ID));
            // collection.Name = "LocationLinks";
            //collection.AddedExplicitly = true;

             
            //return builder;
        }

        private static Microsoft.OData.Edm.IEdmModel AddStructure(ODataConventionModelBuilder builder)
        {
            
            
            var edmModel = builder.GetEdmModel();
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


        public static void AddStructureLinks(ODataModelBuilder builder)
        {
            var type = builder.EntityType<StructureLink>();
            type.HasKey(sl => sl.SourceID);
            type.HasKey(sl => sl.TargetID);
            builder.EntitySet<StructureLink>("StructureLinks");
        }

        public static void AddLocationLinks(ODataModelBuilder builder)
        {
            var type = builder.EntityType<LocationLink>();
            type.HasKey(sl => sl.A);
            type.HasKey(sl => sl.B);
            builder.EntitySet<LocationLink>("LocationLinks");
        }
    }
}
