using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo4j.Driver.V1;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;
using Simple.OData.Client.V4;
using Simple.OData.Client;
using Simple.OData.Client.Extensions;

namespace Neo4JGenerator
{
    static class SpatialAdapter
    { 
        public static long[] FetchCellIDsFromNeo4J()
        {

            List<long> IDs = new List<long>();
            using (var driver = GraphDatabase.Driver(Program.options.Neo4JDatabase, AuthTokens.Basic(Program.options.Username, Program.options.Password)))
            using (var session = driver.Session())
            {
                IStatementResult results = session.Run("MATCH (c) RETURN c.StructureID");
                foreach (var result in results)
                {
                    long ID = System.Convert.ToInt64(result.Values["c.StructureID"]);
                    IDs.Add(ID);
                }
            }

            return IDs.ToArray();
        }

        public static void AddSpatialProperties(string ODataServer)
        {
            AddSpatialProperties(ODataServer, FetchCellIDsFromNeo4J());
        }

        public static void AddSpatialProperties(string ODataServer, long[] CellIDs)
        {
            Task<IReadOnlyDictionary<long, IDictionary<string, object>>> fetchSpatialDataTask = null;

            /*Build a dictionary of all spatial properties in the graph using OData*/
            using (var driver = GraphDatabase.Driver(Program.options.Neo4JDatabase, AuthTokens.Basic(Program.options.Username, Program.options.Password)))
            using (var session = driver.Session())
            {
                IReadOnlyDictionary<long, IDictionary<string, object>> spatialData;

                if(CellIDs.Length > 0)
                {
                    fetchSpatialDataTask = FetchSpatialPropertiesForCellsAsync(ODataServer, new long[] { CellIDs[0] });
                }

                //Query all Spatial data to the nodes listed in IDs
                for (long i = 0; i < CellIDs.Length; i++)
                {
                    long CellID = CellIDs[i];
                    fetchSpatialDataTask.Wait();
                    spatialData = fetchSpatialDataTask.Result;
                    fetchSpatialDataTask = null;

                    //Queue the next OData request if needed
                    if(i + 1 < CellIDs.Length)
                    {
                        fetchSpatialDataTask = FetchSpatialPropertiesForCellsAsync(ODataServer, new long[] { CellIDs[i+1] });
                    }

                    Console.WriteLine(string.Format("{0} - {1} updates - {2:G2}% overall", CellID, spatialData.Count, ((double)i / (double)CellIDs.Length) * 100.0));
                    if (spatialData.ContainsKey(CellID))
                    {
                        AddOrUpdateNodePropertiesByParameters(session, CellID, spatialData[CellID]);
                    }

                    foreach (long ID in spatialData.Keys)
                    {
                        if (ID == CellID)
                        {
                            continue;  
                        }

                        AddOrUpdateEdgePropertiesByParameters(session, ID, spatialData[ID]);
                    }

                    Console.WriteLine("");
                }
            }
        }

        private static void AddOrUpdateNodeProperties(Neo4j.Driver.V1.ISession session, long ID, IDictionary<string, object> Properties)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("MATCH (c { StructureID: " + ID + " }) ");
            sb.Append(BuildSetParameters(Properties, "c"));

            string Neo4Jcmd = sb.ToString();
            Program.WriteLineProgress(Neo4Jcmd);

            session.WriteTransaction(tx => tx.Run(Neo4Jcmd));
        }

        private static void AddOrUpdateNodePropertiesByParameters(Neo4j.Driver.V1.ISession session, long ID, IDictionary<string, object> Properties)
        {
            ConvertGeometryObjectsToWellKnownText(Properties);

            StringBuilder sb = new StringBuilder();
            sb.Append("MATCH (c { StructureID: $ID}) ");
            sb.Append(BuildSetParameters(Properties, "c"));

            string Neo4Jcmd = sb.ToString();
            Program.WriteLineProgress(Neo4Jcmd);

            session.WriteTransaction(tx => tx.Run(Neo4Jcmd, Properties));
        }

        private static void AddOrUpdateEdgeProperties(Neo4j.Driver.V1.ISession session, long ID, IDictionary<string, object> Properties)
        {
            StringBuilder sb = new StringBuilder();

            long ParentID = System.Convert.ToInt64(Properties["ParentID"]);

            sb.Append("MATCH (c:Cell{StructureID:"+ ParentID+ "})-[s {SourceID: " + ID + "}]-() ");
            sb.Append(BuildSetStatements(Properties, "s", "Source"));
            string SourceNeo4Jcmd = sb.ToString();
            //Program.WriteLineProgress(SourceNeo4Jcmd);

            //session.WriteTransaction(tx =>);

            sb.Clear();

            sb.Append("MATCH ()-[t {TargetID: " + ID + "}]-(c:Cell{StructureID:"+ParentID+"}) ");
            sb.Append(BuildSetStatements(Properties, "t", "Target"));
            string TargetNeo4Jcmd = sb.ToString();
            Program.WriteLineProgress(SourceNeo4Jcmd + "\n" + TargetNeo4Jcmd);

            session.WriteTransaction(tx => { tx.Run(SourceNeo4Jcmd); tx.Run(TargetNeo4Jcmd);});
        }

        private static void AddOrUpdateEdgePropertiesByParameters(Neo4j.Driver.V1.ISession session, long ID, IDictionary<string, object> Properties)
        {
            StringBuilder sb = new StringBuilder();

            ConvertGeometryObjectsToWellKnownText(Properties);

            long ParentID = System.Convert.ToInt64(Properties["ParentID"]);

            sb.Append("MATCH (c:Cell{StructureID: $ParentID})-[s {SourceID: $ID}]-() ");
            sb.Append(BuildSetParameters(Properties, "s", "Source"));
            string SourceNeo4Jcmd = sb.ToString();
            //Program.WriteLineProgress(SourceNeo4Jcmd);

            session.WriteTransaction(tx => tx.Run(SourceNeo4Jcmd, Properties));

            sb.Clear();

            sb.Append("MATCH ()-[t {TargetID: $ID}]-(c:Cell{StructureID:$ParentID}) ");
            sb.Append(BuildSetParameters(Properties, "t", "Target"));
            string TargetNeo4Jcmd = sb.ToString();
            Program.WriteLineProgress(SourceNeo4Jcmd + "\n" + TargetNeo4Jcmd);

            session.WriteTransaction(tx => { tx.Run(TargetNeo4Jcmd, Properties);  });
        }
        
        private static string BuildSetParameters(IDictionary<string, object> Properties, string varname, string PrependName = null)
        {
            StringBuilder sb = new StringBuilder();

            bool FirstProperty = true;
            foreach (string propertyName in Properties.Keys)
            {
                if (IsReadOnlyProperty(propertyName))
                    continue;

                string modifiedName = propertyName;
                if (PrependName != null)
                {
                    modifiedName = PrependName + propertyName;
                }
                
                if (FirstProperty)
                {
                    FirstProperty = false;
                    sb.Append("SET " + varname + "." + modifiedName + " = $" + propertyName);
                }
                else
                {
                    sb.Append(", " + varname + "." + modifiedName + " = $" + propertyName);
                }
            }

            return sb.ToString();
        }

        private static string BuildSetStatements(IDictionary<string, object> Properties, string varname, string PrependName=null)
        {
            StringBuilder sb = new StringBuilder();

            bool FirstProperty = true;
            foreach (string propertyName in Properties.Keys)
            {
                if (IsReadOnlyProperty(propertyName))
                    continue;

                string modifiedName = propertyName;
                if(PrependName != null)
                {
                    modifiedName = PrependName + propertyName;
                }

                string propValue = ValueToString(Properties[propertyName]);

                if (FirstProperty)
                {
                    FirstProperty = false;
                    sb.Append("SET " + varname + "." + modifiedName + " = " + propValue);
                }
                else
                {
                    sb.Append(", " + varname + "." + modifiedName + " = " + propValue);
                }
            }

            return sb.ToString();
        }

        private static void ConvertGeometryObjectsToWellKnownText(IDictionary<string, object> Properties)
        {

            foreach(string key in Properties.Keys.ToArray())
            {
                object value = Properties[key];
                if(value is IDictionary<string, object>)
                {
                    IDictionary<string, object> v = value as IDictionary<string, object>;
                    if (v.ContainsKey("Geometry"))
                    {
                        IDictionary<string, object> geom = v["Geometry"] as IDictionary<string, object>;
                        Properties[key] = geom["WellKnownText"];
                    }
                    else
                    {
                        Properties[key] = "UnknownObject";
                    }
                }
            }
        }

        private static string ValueToString(object value)
        {
            
            if (value == null)
                return "null";

            if(value is IDictionary<string, object>)
            {
                IDictionary<string, object> v = value as IDictionary<string, object>;
                if(v.ContainsKey("Geometry"))
                {
                    IDictionary<string, object> geom = v["Geometry"] as IDictionary<string, object>;
                    return ValueToString(geom["WellKnownText"]);
                }
                else
                {
                    return "Dictionary";
                }
            }
            else if (value as String != null)
            {
                return "\"" + value.ToString() + "\"";
            }
            else
            {
                return value.ToString();
            }
            
        }

        /// <summary>
        /// Some properties should not be updated ever
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static bool IsReadOnlyProperty(string name)
        {
            switch(name.ToLower())
            {
                case "id":
                case "parentid":
                case "typeid":
                    return true;
                default:
                    return false; 
            }
        }

        private static IReadOnlyDictionary<long, IDictionary<string, object>> FetchSpatialPropertiesForCells(string ODataEndpoint, IReadOnlyCollection<long> IDs)
        {
            Dictionary<long, IDictionary<string, object>> spatialData = new Dictionary<long, IDictionary<string, object>>();
            ODataClientSettings s = new ODataClientSettings();
            Simple.OData.Client.ODataClient client = new Simple.OData.Client.ODataClient(ODataEndpoint);

            foreach (long ID in IDs)
            {
                ODataFeedAnnotations a = new ODataFeedAnnotations();
                //Query all Spatial data and child spatial data
                string ODataQuery = string.Format("StructureSpatialViews?$filter=(ID eq {0} or ParentID eq {0})", ID);
                Program.WriteLineProgress(ODataQuery.ToString());

                Task<IEnumerable<IDictionary<string, object>>> task = client.FindEntriesAsync(ODataQuery);                
                task.Wait();
                AppendStructureDataToDictionary(task.Result, spatialData);
                while (a.NextPageLink != null)
                {
                    task = client.FindEntriesAsync(a.NextPageLink.ToString(), a);
                    task.Wait();
                    AppendStructureDataToDictionary(task.Result, spatialData);
                }

            }

            return spatialData;
        }

        private static async Task<IReadOnlyDictionary<long, IDictionary<string, object>>> FetchSpatialPropertiesForCellsAsync(string ODataEndpoint, IReadOnlyCollection<long> IDs)
        {
            Dictionary<long, IDictionary<string, object>> spatialData = new Dictionary<long, IDictionary<string, object>>();
            ODataClientSettings s = new ODataClientSettings();
            Simple.OData.Client.ODataClient client = new Simple.OData.Client.ODataClient(ODataEndpoint);

            foreach (long ID in IDs)
            {
                ODataFeedAnnotations a = new ODataFeedAnnotations();
                //Query all Spatial data and child spatial data
                string ODataQuery = string.Format("StructureSpatialViews?$filter=(ID eq {0} or ParentID eq {0})", ID);
                Program.WriteLineProgress(ODataQuery.ToString());

                IEnumerable<IDictionary<string, object>> result = await client.FindEntriesAsync(ODataQuery);
                AppendStructureDataToDictionary(result, spatialData);
                while (a.NextPageLink != null)
                {
                    result = await client.FindEntriesAsync(a.NextPageLink.ToString(), a);
                    AppendStructureDataToDictionary(result, spatialData);
                } 
            }

            return spatialData;
        }


        private static void AppendStructureDataToDictionary(IEnumerable<IDictionary<string, object>> results, IDictionary<long, IDictionary<string, object>> data)
        {
            foreach(IDictionary<string, object> result in results)
            {
                long ID = System.Convert.ToInt64(result["ID"]);
 
                if (data.ContainsKey(ID))
                {
                    foreach(string key in result.Keys)
                    {
                        data[ID][key] = result[key];
                    }
                }
                else
                {
                    data.Add(ID, result);
                }
            }

            return; 
        }
    }
}
