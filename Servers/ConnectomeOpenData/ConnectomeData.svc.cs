using System;
using System.Collections.Generic;
using System.Data.Services;
using System.Data.Services.Common;
using System.Linq;
using System.ServiceModel.Web;
using System.Web;
using VikingWebAppSettings;

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

        protected override ConnectomeEntities CreateDataSource()
        {
            System.Data.EntityClient.EntityConnection connection = new System.Data.EntityClient.EntityConnection(BuildConnectionString());

            return new ConnectomeEntities(connection);
        }

        private string BuildConnectionString()
        {
            //string conn_name = AppSettings.GetApplicationSetting("ODataConnection");
            string template = AppSettings.GetConnectionString("ODataConnectionTemplate");
            return string.Format(template, AppSettings.GetDatabaseServer(), AppSettings.GetDatabaseCatalogName());
            //return template;
        } 

        private static void ConfigureTable(DataServiceConfiguration config, string Tablename)
        {
            config.SetEntitySetAccessRule(Tablename, EntitySetRights.AllRead);
            config.SetEntitySetPageSize(Tablename, 16384);
        }

        [WebGet]
        public IQueryable<Structure> SelectRootStructures()
        {
            return CurrentDataSource.SelectRootStructures().AsQueryable();
        }

        [WebGet]
        public IQueryable<SelectStructureLocations_Result> SelectStructureLocations(Int64? ID)
        {
            return CurrentDataSource.SelectStructureLocations(ID).AsQueryable();
        }
        [WebGet]
        public IQueryable<LocationLink> SelectStructureLocationLinks(Int64 ID)
        {
            return CurrentDataSource.SelectStructureLocationLinksNoChildren(ID).AsQueryable();
        }

        [WebGet]
        public IQueryable<StructureLink> SelectChildStructureLinks(Int64 ID)
        {
            return CurrentDataSource.SelectChildStructureLinks(ID).AsQueryable();
        }

        [WebGet]
        public IQueryable<ApproximateStructureLocation_Result> ApproximateStructureLocation(Int64 ID)
        {
            return CurrentDataSource.ApproximateStructureLocation(Convert.ToInt32(ID)).AsQueryable();
        }

        [WebGet]
        public IQueryable<ApproxStructurePosition> ApproximateStructureLocations()
        {
            return CurrentDataSource.ApproximateStructureLocations().AsQueryable();
        }

        [WebGet]
        public IQueryable<SelectStructureChangeLog_Result> StructureChangeLog(Int64? ID)
        {
            return CurrentDataSource.SelectStructureChangeLog(ID, null, null).AsQueryable();
        }

        [WebGet]
        public IQueryable<SelectStructureLocationChangeLog_Result> StructureLocationChangeLog(Int64? ID)
        {
            return CurrentDataSource.SelectStructureLocationChangeLog(ID, null, null).AsQueryable();
        }

        [WebGet]
        public IQueryable<long?> StructuresLinkedViaChildren(long? ID)
        {
            return CurrentDataSource.SelectStructuresLinkedViaChildren(ID).AsQueryable();
        }
    }
}
