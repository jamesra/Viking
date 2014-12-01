using System;
using System.Collections.Generic;
using System.Data.Services;
using System.Data.Services.Common;
using System.Linq;
using System.ServiceModel.Web;
using System.Web;

namespace ConnectomeOpenData
{
    [DataServicesJSONP.JSONPSupportBehavior]
    public class ConnectomeDataService : DataService < ConnectomeEntities >
    {
        // This method is called only once to initialize service-wide policies.
        public static void InitializeService(DataServiceConfiguration config)
        {
            // TODO: set rules to indicate which entity sets and service operations are visible, updatable, etc.
            // Examples:
            config.UseVerboseErrors = true;
            //ConfigureTable(config, "StructureTypes");
            //ConfigureTable(config, "Structures");
            //ConfigureTable(config, "StructureLinks");
            //ConfigureTable(config, "Locations");
            //ConfigureTable(config, "LocationLinks");


            config.SetServiceOperationAccessRule("*", ServiceOperationRights.AllRead);
            
            ConfigureTable(config, "*");
            
            // config.SetServiceOperationAccessRule("MyServiceOperation", ServiceOperationRights.All);
            config.DataServiceBehavior.MaxProtocolVersion = DataServiceProtocolVersion.V3;
        }


        private static void ConfigureTable(DataServiceConfiguration config, string Tablename)
        {
            config.SetEntitySetAccessRule(Tablename, EntitySetRights.AllRead);
            config.SetEntitySetPageSize(Tablename, 1024);
        }

        [WebGet]
        public IQueryable<LocationLink> SelectStructureLocationLinks(Int64 StructureID)
        {
            //return new List<LocationLink>();
            return CurrentDataSource.SelectStructureLocationLinksNoChildren(StructureID).AsQueryable();
            
            //return entities.SelectStructureLocationLinks(Convert.ToInt64(StructureID)).ToList();
        }

        [WebGet]
        public IQueryable<StructureLink> SelectChildStructureLinks(Int64 StructureID)
        {
            //return new List<LocationLink>();
            return CurrentDataSource.SelectChildStructureLinks(StructureID).AsQueryable();

            //return entities.SelectStructureLocationLinks(Convert.ToInt64(StructureID)).ToList();
        }

        [WebGet]
        public IQueryable<ApproximateStructureLocation_Result> ApproximateStructureLocation(Int64 StructureID)
        {
            //return new List<LocationLink>();
            return CurrentDataSource.ApproximateStructureLocation(Convert.ToInt32(StructureID)).AsQueryable();
        }

        [WebGet]
        public IQueryable<ApproxStructurePosition> ApproximateStructureLocations()
        {
            //return new List<LocationLink>();
            return CurrentDataSource.ApproximateStructureLocations().AsQueryable();

            //return entities.SelectStructureLocationLinks(Convert.ToInt64(StructureID)).ToList();
        }
    }
}
