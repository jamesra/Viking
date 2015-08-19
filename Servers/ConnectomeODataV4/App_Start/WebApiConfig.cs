using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Configuration;
using System.Web.Http;
using System.Web.OData.Builder;
using System.Web.OData.Extensions;

using ConnectomeODataV4.Models;

namespace ConnectomeODataV4
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services
            var json = GlobalConfiguration.Configuration.Formatters.JsonFormatter;
            json.UseDataContractJsonSerializer = true;
            //json.SerializerSettings.PreserveReferencesHandling = Newtonsoft.Json.PreserveReferencesHandling.All;
             

            // Web API routes
            config.MapHttpAttributeRoutes();

            ODataConventionModelBuilder builder = GetModel();

            config.MapODataServiceRoute(routeName: "odata",
                routePrefix: null,
                model: builder.GetEdmModel());
            
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }

        public static ODataConventionModelBuilder GetModel()
        {
            ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
            builder.EntitySet<StructureType>("StructureTypes");
            builder.EntitySet<Structure>("Structures");
            builder.EntitySet<Location>("Locations");
            AddStructureLinks(builder);
            AddLocationLinks(builder);
          
            return builder;
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
