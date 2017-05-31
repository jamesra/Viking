using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using VikingWebAppSettings;
using Neo4j.Driver.V1;
using System.IO;
using Newtonsoft.Json.Converters;
using System.Text;

namespace Neo4JService.Controllers
{
    //[Authorize]
    public class QueryController : ApiController
    {
        // GET api/values
        [System.Web.Mvc.HttpGet]
        [System.Web.Mvc.AcceptVerbs(HttpVerbs.Get)]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // POST api/values 
        [System.Web.Mvc.HttpPost]
        [System.Web.Mvc.AcceptVerbs(HttpVerbs.Post)]
        public string Post([FromBody]string value)
        {
            string Neo4JDatabase = VikingWebAppSettings.AppSettings.GetApplicationSetting("Neo4JDatabase");
            string username = VikingWebAppSettings.AppSettings.GetApplicationSetting("Neo4JUser");
            string password = VikingWebAppSettings.AppSettings.GetApplicationSetting("Neo4JPassword");

            using (var driver = GraphDatabase.Driver(Neo4JDatabase, AuthTokens.Basic(username, password)))
            using (var session = driver.Session())
            {
                IStatementResult result = session.Run(value);

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
    }
}
