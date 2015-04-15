using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DataExport;
using AnnotationVizLib;
using VikingWebAppSettings;

namespace DataExport.Controllers
{
    public class NetworkController : Controller
    {
        public string DefaultOutputFile = "network";

        private static long _next_id = 0;

        public static long NextFilenameID
        {
            get
            {
                return _next_id++;
            }
        }

        public string GetOutputFilename(string ext)
        {
            return string.Format("{0}{1}.{2}", DefaultOutputFile, NextFilenameID, ext);
        }

        //
        // GET: /Network/Dot 
        [ActionName("GetDot")]
        public ActionResult GetDot()
        {
            
            string EndpointURL = AppSettings.WebServiceURL; 
            string userDotDirectory = Server.MapPath("~/Output/");

            AnnotationVizLib.ConnectionFactory.SetConnection(EndpointURL, AppSettings.EndpointCredentials);

            ICollection<long> requestIDs = RequestVariables.GetQueryStringIDs(Request).Cast<long>().ToArray();
            if (requestIDs == null || requestIDs.Count == 0)
                requestIDs = Queries.GetLinkedStructureParentIDs();

            if (!System.IO.Directory.Exists(userDotDirectory))
                System.IO.Directory.CreateDirectory(userDotDirectory);

            string outputFilename = GetOutputFilename("dot");
            string userDotFileFullPath = System.IO.Path.Combine(userDotDirectory, outputFilename);

            NeuronGraph neuronGraph = NeuronGraph.BuildGraph(requestIDs, GetNumHops(), EndpointURL, AppSettings.EndpointCredentials);
            NeuronDOTView DotGraph = NeuronDOTView.ToDOT(neuronGraph, false);
            DotGraph.SaveDOT(userDotFileFullPath);

            return File(userDotFileFullPath, "text/plain", outputFilename);
        }

        [ActionName("GetTLP")]
        public ActionResult GetTLP()
        {
            string EndpointURL = AppSettings.WebServiceURL;
            string VolumeURL = AppSettings.VolumeURL; 
            string userDotDirectory = Server.MapPath("~/Output/");

            AnnotationVizLib.ConnectionFactory.SetConnection(EndpointURL, AppSettings.EndpointCredentials);

            if (!System.IO.Directory.Exists(userDotDirectory))
                System.IO.Directory.CreateDirectory(userDotDirectory);

            ICollection<long> requestIDs = RequestVariables.GetQueryStringIDs(Request).Cast<long>().ToArray();
            if (requestIDs == null || requestIDs.Count == 0)
                requestIDs = Queries.GetLinkedStructureParentIDs();

            string outputFilename = GetOutputFilename("tlp");
            string userDotFileFullPath = System.IO.Path.Combine(userDotDirectory, outputFilename);

            NeuronGraph neuronGraph = NeuronGraph.BuildGraph(requestIDs, GetNumHops(), EndpointURL, AppSettings.EndpointCredentials);
            NeuronTLPView TlpGraph = NeuronTLPView.ToTLP(neuronGraph, VolumeURL);
            TlpGraph.SaveTLP(userDotFileFullPath);

            return File(userDotFileFullPath, "text/plain", outputFilename);
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
