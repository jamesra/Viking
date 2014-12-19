using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DataExport;
using AnnotationVizLib;

namespace DataExport.Controllers
{
    public class NetworkController : Controller
    { 
        //
        // GET: /Network/Dot 
        [ActionName("GetDot")]
        public ActionResult GetDot()
        {
            string database = AppSettings.GetDatabaseCatalogName();
            string EndpointURL = string.Format(AppSettings.WebServiceURLTemplate, database);
            string userDotDirectory = Server.MapPath("~/Dot/");

            AnnotationVizLib.ConnectionFactory.SetConnection(EndpointURL, AppSettings.EndpointCredentials);

            ICollection<long> requestIDs = RequestVariables.GetQueryStringIDs(Request).Cast<long>().ToArray();
            if (requestIDs == null || requestIDs.Count == 0)
                requestIDs = Queries.GetLinkedStructureParentIDs();

            if (!System.IO.Directory.Exists(userDotDirectory))
                System.IO.Directory.CreateDirectory(userDotDirectory);

            string userDotFileFullPath = System.IO.Path.Combine(userDotDirectory, "network.dot");

            NeuronGraph neuronGraph = NeuronGraph.BuildGraph(requestIDs, GetNumHops(), EndpointURL, AppSettings.EndpointCredentials);
            NeuronDOTView DotGraph = NeuronDOTView.ToDOT(neuronGraph, false);
            DotGraph.SaveDOT(userDotFileFullPath);

            return File(userDotFileFullPath, "text/plain", "network.dot");
        }

        [ActionName("GetTLP")]
        public ActionResult GetTLP()
        {
            string database = AppSettings.GetDatabaseCatalogName();
            string EndpointURL = string.Format(AppSettings.WebServiceURLTemplate, database);
            string userDotDirectory = Server.MapPath("~/Dot/");

            AnnotationVizLib.ConnectionFactory.SetConnection(EndpointURL, AppSettings.EndpointCredentials);

            if (!System.IO.Directory.Exists(userDotDirectory))
                System.IO.Directory.CreateDirectory(userDotDirectory);

            ICollection<long> requestIDs = RequestVariables.GetQueryStringIDs(Request).Cast<long>().ToArray();
            if (requestIDs == null || requestIDs.Count == 0)
                requestIDs = Queries.GetLinkedStructureParentIDs();

            string userDotFileFullPath = System.IO.Path.Combine(userDotDirectory, "network.tlp");

            NeuronGraph neuronGraph = NeuronGraph.BuildGraph(requestIDs, GetNumHops(), EndpointURL, AppSettings.EndpointCredentials);
            NeuronTLPView TlpGraph = NeuronTLPView.ToTLP(neuronGraph);
            TlpGraph.SaveTLP(userDotFileFullPath);


            return File(userDotFileFullPath, "text/plain", "network.tlp");
        } 



        private uint GetNumHops()
        {
            string hopstr = Request.RequestContext.HttpContext.Request.QueryString["hops"];
            if (hopstr == null)
            {
                return 1;
            }

            try
            {
                return Convert.ToUInt32(hopstr);
            }
            catch (FormatException)
            {
                return 1;
            }
            
        }

         
    }
}
