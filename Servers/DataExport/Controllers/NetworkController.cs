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
            string outputFilename = GetOutputFilename("dot");
            string outputFileFullPath = System.IO.Path.Combine(GetAndCreateOutputDirectory(), outputFilename);


            NeuronGraph neuronGraph = GetGraph();
            NeuronDOTView DotGraph = NeuronDOTView.ToDOT(neuronGraph, false);
            DotGraph.SaveDOT(outputFileFullPath);

            return File(outputFileFullPath, "text/plain", outputFilename);
        }

        [ActionName("GetTLP")]
        public ActionResult GetTLP()
        {
            string outputFilename = GetOutputFilename("tlp");
            string outputFileFullPath = System.IO.Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = GetGraph();
            NeuronTLPView TlpGraph = NeuronTLPView.ToTLP(neuronGraph, AppSettings.VolumeURL);
            TlpGraph.SaveTLP(outputFileFullPath);

            return File(outputFileFullPath, "text/plain", outputFilename);
        }

        [ActionName("GetGML")]
        public ActionResult GetGML()
        {
            string outputFilename = GetOutputFilename("graphml");
            string outputFileFullPath = System.IO.Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = GetGraph();
            NeuronGMLView GmlGraph = NeuronGMLView.ToGML(neuronGraph, AppSettings.VolumeURL);
            GmlGraph.SaveGML(outputFileFullPath);

            return File(outputFileFullPath, "text/plain", outputFilename);
        }

        [ActionName("GetJSON")]
        public ActionResult GetJSON()
        {
            string outputFilename = GetOutputFilename("json");
            string outputFileFullPath = System.IO.Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = GetGraph();
            NeuronJSONView JsonGraph = NeuronJSONView.ToJSON(neuronGraph);
            JsonGraph.SaveJSON(outputFileFullPath);

            return File(outputFileFullPath, "text/plain", outputFilename);
        }

        private string GetAndCreateOutputDirectory()
        {
            string userDotDirectory = Server.MapPath("~/Output/");
            if (!System.IO.Directory.Exists(userDotDirectory))
                System.IO.Directory.CreateDirectory(userDotDirectory);

            return userDotDirectory;
        }

        private NeuronGraph GetGraph()
        {
            string EndpointURL = AppSettings.WebServiceURL;
            
            AnnotationVizLib.ConnectionFactory.SetConnection(EndpointURL, AppSettings.EndpointCredentials);
            
            ICollection<long> requestIDs = RequestVariables.GetQueryStringIDs(Request).Cast<long>().ToArray();
            if (requestIDs == null || requestIDs.Count == 0)
                requestIDs = Queries.GetLinkedStructureParentIDs(); 

            return NeuronGraph.BuildGraph(requestIDs, GetNumHops(), EndpointURL, AppSettings.EndpointCredentials);
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
