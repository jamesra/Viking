using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataExport.Controllers
{
    /// <summary>
    /// Helper class to generate names for generated files
    /// </summary>
    public static class OutputNameGenerator {
        public static string GetFileFriendlyDateString()
        {
            DateTime now = System.DateTime.Now;
            return string.Format("nw-{0,04:d4}-{1,02:d2}-{2,02:d2} {3,02:d2}{4,02:d2}{5,02:d2}", now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
        }

        public static string GetFileFriendlyIDList(ICollection<long> requestIDs, int max_length=140)
        {
            if (requestIDs.Count == 0)
                return "ALL";

            System.Text.StringBuilder sb = new System.Text.StringBuilder(max_length);

            bool first = true;
            foreach (long ID in requestIDs)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    sb.Append("_");
                }

                sb.Append(ID.ToString());
                if (sb.Length > max_length)
                {
                    sb.Append("etc");
                    break;
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// A helper class to pull common URL query parameters from our requests
    /// </summary>
    public static class RequestVariables
    {
        public static ICollection<long> GetIDsFromQueryData(IQueryCollection queryData)
        {
            //A hack, but should only occur in unit testing
            if (queryData is null)
                return new long[] { 180, 476, 514 };

            SortedSet<long> IDs = new SortedSet<long>();

            IDs.UnionWith(ParseIDString(queryData["id"].ToString()));
            IDs.UnionWith(ParseIDString(queryData["ids"].ToString()));
            IDs.UnionWith(ParseIDString(queryData["$id"].ToString()));
            IDs.UnionWith(ParseIDString(queryData["$ids"].ToString()));

            string query_string = queryData["query"].ToString();
            if (!string.IsNullOrEmpty(query_string))
            {
                IDs.UnionWith(GetIDsFromQuery(VikingWebAppSettings.AppSettings.ODataURL, query_string));
            }

            query_string = queryData["$query"].ToString();
            if (!string.IsNullOrEmpty(query_string))
            {
                IDs.UnionWith(GetIDsFromQuery(VikingWebAppSettings.AppSettings.ODataURL, query_string));
            }

            return IDs;
        }

        public static ICollection<long> ParseIDString(string idListstr)
        {
            if (idListstr is null)
                return new long[0]; 

            string[] parts = idListstr.Split(new char[] {';', '\n'}, StringSplitOptions.RemoveEmptyEntries);
            List<long> ids = new List<long>(parts.Length);
            var query_tasks = new List<Task<ICollection<long>>>();
            foreach (string id in parts)
            {
                if (id is null)
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
                }
            }

            return ids;
        }
        
        public static ICollection<long> GetIDsFromQuery(Uri ODataURI, string query)
        {  
            // TODO: Replace with AnnotationVizLibODataClient implementation
            // For now, return empty collection
            System.Diagnostics.Trace.WriteLine($"OData query not implemented: {query}");
            return new List<long>();
        }

        public static async Task<ICollection<long>> GetIDsFromQueryAsync(Uri ODataURI, string query)
        { 
            // TODO: Replace with AnnotationVizLibODataClient implementation
            // For now, return empty collection
            System.Diagnostics.Trace.WriteLine($"OData query not implemented: {query}");
            return new List<long>();
        }
    }
}
