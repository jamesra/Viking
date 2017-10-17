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
        public static ICollection<long> GetIDs(HttpRequestBase Request)
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromRequestFiles(Request.Files);
            ICollection<long> queryIDs = RequestVariables.GetIDsFromQueryData(Request.QueryString);

            List<long> IDs = new List<long>(requestIDs.Count + queryIDs.Count);
            IDs.AddRange(requestIDs);
            IDs.AddRange(queryIDs);

            return IDs;
        }

        public static ICollection<long> GetIDsFromRequestFiles(HttpFileCollectionBase files)
        {
            if (files == null)
                return new long[0];

            List<long> ids = new List<long>();

            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];

                byte[] buffer = new byte[f.ContentLength];
                int length = f.InputStream.Read(buffer, 0, f.ContentLength);
                string ids_string = System.Text.Encoding.UTF8.GetString(buffer);

                ids.AddRange(RequestVariables.ParseIDString(ids_string));
            }

            return ids;
        }

        /// <summary>
        /// A query can specify IDs using either the IDs
        /// </summary>
        /// <param name="QueryData"></param>
        /// <returns></returns>
        public static ICollection<long> GetIDsFromQueryData(NameValueCollection  QueryData)
        {
            //A hack, but should only occur in unit testing
            if (QueryData == null)
                return new long[] { 180, 476, 514 };

            SortedSet<long> IDs = new SortedSet<long>();

            IDs.UnionWith(ParseIDString(QueryData["id"]));
            IDs.UnionWith(ParseIDString(QueryData["ids"]));
            IDs.UnionWith(ParseIDString(QueryData["$id"]));
            IDs.UnionWith(ParseIDString(QueryData["$ids"]));

            string query_string = QueryData["query"];
            if (query_string != null)
            {
                IDs.UnionWith(GetIDsFromQuery(VikingWebAppSettings.AppSettings.ODataURL, query_string));
            }

            query_string = QueryData["$query"];
            if (query_string != null)
            {
                IDs.UnionWith(GetIDsFromQuery(VikingWebAppSettings.AppSettings.ODataURL, query_string));
            }

            return IDs;
        } 

        public static ICollection<long> ParseIDString(string idListstr)
        {
            if (idListstr == null)
                return new long[0]; 

            string[] parts = idListstr.Split(new char[] {';', '\n'}, StringSplitOptions.RemoveEmptyEntries);
            List<long> ids = new List<long>(parts.Length);
            var query_tasks = new List<Task<ICollection<long>>>();
            foreach (string id in parts)
            {
                if (id == null)
                    continue;

                try
                {
                    //Do not allow a negative id
                    ids.Add(Convert.ToInt64(Convert.ToUInt64(id)));
                }
                catch (FormatException)
                {
                    ICollection<long> query_ids = GetIDsFromQuery(VikingWebAppSettings.AppSettings.ODataURL, id);
                    ids.AddRange(query_ids);
                    //Try to parse a 
                    /*
                    var task = GetIDsFromQueryAsync(VikingWebAppSettings.AppSettings.ODataURL, id);
                    query_tasks.Add(task);
                    continue;
                    */
                }
            }
            /*
            foreach(var query_task in query_tasks)
            { 
                if(!query_task.IsFaulted)
                {
                    ids.AddRange(query_task.Result);
                }
            }
            */

            return ids;
        }
        
        public static ICollection<long> GetIDsFromQuery(Uri ODataURI, string query)
        {  
            ODataClient client = new ODataClient(ODataURI);
            IEnumerable<IDictionary<string, object>> packages = null;

            try
            {
                Task<IEnumerable<IDictionary<string, object>>> packages_task = client.FindEntriesAsync(query);
                packages_task.Wait();
                packages = packages_task.Result;
            }
            catch(Simple.OData.Client.WebRequestException e)
            {
                System.Diagnostics.Trace.WriteLine(string.Format("Exception requesting OData\n{0}\n{1}", query, e.ToString()));
                return new List<long>();
            }

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
