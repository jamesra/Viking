using Neo4j.Driver.V1;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;

namespace Neo4JService.Controllers
{
    //[Authorize]
    public class QueryController : ApiController
    {
        /*
        // GET api/values
        [System.Web.Mvc.HttpGet]
        [System.Web.Mvc.AcceptVerbs(HttpVerbs.Get)]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }
        */
        // POST api/values 
        [System.Web.Mvc.HttpPost]
        [System.Web.Mvc.AcceptVerbs(HttpVerbs.Post)]
        [System.Web.Mvc.ActionName("PostQuery")]
        public string PostQuery()
        {
            Task<string> content = Request.Content.ReadAsStringAsync();
            content.Wait();
            string query = content.Result;
            
            if(query == null || query.Length == 0)
            {
                throw new ArgumentException("No query found in post");
            }

            string FoundKeywords; 
            if(ContainsWritableKeywords(query, out FoundKeywords))
            {
                throw new ArgumentException("Found write-capable keywords in query.  Please submit only readonly queries:\n" + FoundKeywords);
            }

            string Neo4JDatabase = VikingWebAppSettings.AppSettings.GetApplicationSetting("Neo4JDatabase");
            string username = VikingWebAppSettings.AppSettings.GetApplicationSetting("Neo4JUser");
            string password = VikingWebAppSettings.AppSettings.GetApplicationSetting("Neo4JPassword");

            using (var driver = GraphDatabase.Driver(Neo4JDatabase, AuthTokens.Basic(username, password)))
            using (var session = driver.Session())
            {
                IStatementResult result = session.Run(query);

                string json = EncodeResult(result);

                return json;
            }
        }

        public string EncodeResult(IStatementResult result)
        {
            //Do something to encode the result
            StringBuilder sb = new StringBuilder(); 
            foreach (IRecord record in result)
            {
                string values = Newtonsoft.Json.JsonConvert.SerializeObject(record.Values);
                sb.AppendLine(values);
            }
                
            return sb.ToString();
        }


        /// <summary>
        /// Check the query passed to Neo4J and remove any commands that would not be read-only
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private bool ContainsWritableKeywords(string query, out string FoundKeywords)
        {
            FoundKeywords = null;

            StringBuilder sb = new StringBuilder();

            string upper_query = query.ToUpper(); 

            string[] Keywords =new string[] { "%", "CALL", "CREATE", "DELETE", "DETACH", "SET", "REMOVE", "DROP", "FOREACH", "LOAD" };

            foreach (string keyword in Keywords)
            {
                if (upper_query.Contains(keyword))
                {
                    sb.AppendLine(keyword);
                }
            }

            FoundKeywords = sb.ToString();

            return FoundKeywords.Length > 0;
        }
    }
}
