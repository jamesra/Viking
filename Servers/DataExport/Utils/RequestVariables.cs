using System;
using System.Web;
using System.Web.Mvc;
using System.Collections.Generic;
using System.Collections.Specialized;
using Simple.OData.Client;
using System.Threading.Tasks;



namespace DataExport.Controllers
{
    /// <summary>
    /// A helper class to pull common URL query parameters from our requests
    /// </summary>
    public static class RequestVariables
    {
        public static ICollection<long> GetIDs(NameValueCollection  QueryData)
        {
            //A hack, but should only occur in unit testing
            if (QueryData == null)
                return new long[] { 180, 476, 514 };

            List<long> IDs = new List<long>();

            string idListstr = QueryData["id"];
            if (idListstr == null)
            {
                idListstr = QueryData["ids"];
                if (idListstr == null)
                {
                    return new long[0];
                }
            }

            if (idListstr != null)
                IDs.AddRange(ParseIDString(idListstr));

            string query_string = QueryData["query"];
            if (query_string != null)
            {
                IDs.AddRange(GetIDsFromQuery(VikingWebAppSettings.AppSettings.ODataURL, query_string));
            }

            return IDs;
        } 

        public static ICollection<long> ParseIDString(string idListstr)
        {
            string[] parts = idListstr.Split(new char[] { ',', ';', ' ', '\n'}, StringSplitOptions.RemoveEmptyEntries);
            List<long> ids = new List<long>(parts.Length);
            var query_tasks = new List<Task<ICollection<long>>>();
            foreach (string id in parts)
            {
                try
                {
                    //Do not allow a negative id
                    ids.Add(Convert.ToInt64(Convert.ToUInt64(id)));
                }
                catch (FormatException)
                {
                    //Try to parse a query
                    var task = GetIDsFromQueryAsync(VikingWebAppSettings.AppSettings.ODataURL, id);
                    task.Start();
                    query_tasks.Add(task);
                    continue;
                }
            }

            foreach(var query_task in query_tasks)
            {
                query_task.Wait();

                if(!query_task.IsFaulted)
                {
                    ids.AddRange(query_task.Result);
                }
            }

            return ids;
        }

        public static ICollection<long> GetIDsFromQuery(Uri ODataURI, string query)
        {
            ODataClient client = new ODataClient(ODataURI);

            Task<IEnumerable<IDictionary<string, object>>> packages_task = client.FindEntriesAsync(query);
            packages_task.Wait();
            var packages = packages_task.Result;

            List<long> IDs = new List<long>();

            foreach (var package in packages)
            {
                if (package.ContainsKey("ID"))
                {
                    long ID = System.Convert.ToInt64(package["ID"]);
                    IDs.Add(ID);
                }
                else if(package.ContainsKey("__result"))
                {
                    long ID = System.Convert.ToInt64(package["__result"]);
                    IDs.Add(ID);
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine("Unable to parse response from query: " + query);
                    continue;
                }
            }

            return IDs;
        }

        public async static Task<ICollection<long>> GetIDsFromQueryAsync(Uri ODataURI, string query)
        { 
            ODataClient client = new ODataClient(ODataURI);

            IEnumerable<IDictionary<string,object>> packages = await client.FindEntriesAsync(query); 
            List<long> IDs = new List<long>();

            foreach(var package in packages)
            {
                if (!package.ContainsKey("ID"))
                    continue; 

                long ID = System.Convert.ToInt64(package["ID"]);
                IDs.Add(ID);
            }

            return IDs;
        } 
    }
}