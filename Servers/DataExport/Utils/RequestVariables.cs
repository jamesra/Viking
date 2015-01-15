using System;
using System.Web;
using System.Web.Mvc;
using System.Collections.Generic;


namespace DataExport.Controllers
{
    /// <summary>
    /// A helper class to pull common URL query parameters from our requests
    /// </summary>
    public static class RequestVariables
    {
            public static ICollection<long> GetQueryStringIDs(HttpRequestBase Request)
            {
                //A hack, but should only occur in unit testing
                if (Request == null)
                    return new long[] { 180, 476, 514};

                string idListstr = Request.RequestContext.HttpContext.Request.QueryString["id"];
                if (idListstr == null)
                {
                    return null; 
                }

                string[] parts = idListstr.Split(new char[]{',',';',' '}, StringSplitOptions.RemoveEmptyEntries);
                List<long> ids = new List<long>(parts.Length);
                foreach(string id in parts)
                {
                    try
                    {
                        //Do not allow a negative id
                        ids.Add(Convert.ToInt64(Convert.ToUInt64(id)));
                    }
                    catch(FormatException)
                    {
                        continue;
                    }
                }

                return ids;
            }

    }
}