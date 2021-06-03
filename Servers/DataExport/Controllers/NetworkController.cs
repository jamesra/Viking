using AnnotationVizLib;
using AnnotationVizLib.WCFClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using VikingWebAppSettings;

namespace DataExport.Controllers
{
    public class NetworkController : Controller
    {
        public string GetOutputFilename(ICollection<long> requestIDs, string ext)
        {
            string ID_List = OutputNameGenerator.GetFileFriendlyIDList(requestIDs);
            string date = OutputNameGenerator.GetFileFriendlyDateString(); 

            return string.Format("nw-{0}_hops_{1} {2}.{3}", ID_List, GetNumHops(), date, ext);
        }

        private ActionResult RedirectToFile(string outputFilename)
        {
            Response.StatusCode = (int)System.Net.HttpStatusCode.Created;
            Uri host = AppSettings.VolumeURI;
            string url = new Uri(host, Request.ApplicationPath + "/Output/" + outputFilename).ToString();
            Response.Headers["Location"] = url;
            Response.Redirect(url, true);
            return new EmptyResult(); 
        }

        [HttpPost()]
        [AcceptVerbs(HttpVerbs.Post)]
        public async Task<ActionResult> PostDot(HttpPostedFileBase req)
        {
            ICollection<long> requestIDs = RequestVariables.GetIDs(Request);

            string outputFilename = GetOutputFilename(requestIDs, "dot");
            string outputFileFullPath = System.IO.Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = GetGraph(requestIDs);
            NeuronDOTView DotGraph = NeuronDOTView.ToDOT(neuronGraph, false);
            DotGraph.SaveDOT(outputFileFullPath); 
            return RedirectToFile(outputFilename);
        }


        [HttpPost()]
        [AcceptVerbs(HttpVerbs.Post)]
        public async Task<ActionResult> PostTLP(HttpPostedFileBase req)
        {
            ICollection<long> requestIDs = RequestVariables.GetIDs(Request);

            string outputFilename = GetOutputFilename(requestIDs, "tlp");
            string outputFileFullPath = System.IO.Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = GetGraph(requestIDs);
            NeuronTLPView TlpGraph = NeuronTLPView.ToTLP(neuronGraph, AppSettings.VolumeURL);
            TlpGraph.SaveTLP(outputFileFullPath);

            return RedirectToFile(outputFilename);
        }

        [HttpPost()]
        [AcceptVerbs(HttpVerbs.Post)]
        public async Task<ActionResult> PostGML(HttpPostedFileBase req)
        {
            ICollection<long> requestIDs = RequestVariables.GetIDs(Request);

            string outputFilename = GetOutputFilename(requestIDs, "graphml");
            string outputFileFullPath = System.IO.Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = GetGraph(requestIDs);
            NeuronGMLView GmlGraph = NeuronGMLView.ToGML(neuronGraph, AppSettings.VolumeURL);
            GmlGraph.SaveGML(outputFileFullPath);

            return RedirectToFile(outputFilename);
        }

        [HttpPost()]
        [AcceptVerbs(HttpVerbs.Post)]
        public async Task<ActionResult> PostJSON(HttpPostedFileBase req)
        {
            ICollection<long> requestIDs = RequestVariables.GetIDs(Request);

            string outputFilename = GetOutputFilename(requestIDs, "json");
            string outputFileFullPath = System.IO.Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = GetGraph(requestIDs);
            NeuronJSONView JsonGraph = NeuronJSONView.ToJSON(neuronGraph);
            JsonGraph.SaveJSON(outputFileFullPath);

            return RedirectToFile(outputFilename);
        }

        //
        // GET: /Network/Dot 
        [ActionName("GetDot")]
        [HttpGet]
        public async Task<ActionResult> GetDot()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDs(Request);
            string outputFilename = GetOutputFilename(requestIDs, "dot");
            string outputFileFullPath = System.IO.Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = GetGraph(requestIDs);
            NeuronDOTView DotGraph = NeuronDOTView.ToDOT(neuronGraph, false);
            DotGraph.SaveDOT(outputFileFullPath);

            return File(outputFileFullPath, "text/plain", outputFilename);
        }

        [ActionName("GetTLP")]
        [HttpGet]
        public async Task<ActionResult> GetTLP()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDs(Request);
            string outputFilename = GetOutputFilename(requestIDs, "tlp");
            string outputFileFullPath = System.IO.Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = GetGraph(requestIDs);
            AnnotationVizLib.SimpleODataClient.SimpleODataSpatialDataFactory.AppendSpatialDataFromOData(neuronGraph, VikingWebAppSettings.AppSettings.ODataURL, requestIDs, GetNumHops());
            NeuronTLPView TlpGraph = NeuronTLPView.ToTLP(neuronGraph, AppSettings.VolumeURL);
            TlpGraph.SaveTLP(outputFileFullPath);

            return File(outputFileFullPath, "text/plain", outputFilename);
        }

        [ActionName("GetGML")]
        [HttpGet]
        public async Task<ActionResult> GetGML()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDs(Request);
            string outputFilename = GetOutputFilename(requestIDs, "graphml");
            string outputFileFullPath = System.IO.Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = GetGraph(requestIDs);
            NeuronGMLView GmlGraph = NeuronGMLView.ToGML(neuronGraph, AppSettings.VolumeURL);
            GmlGraph.SaveGML(outputFileFullPath);

            return File(outputFileFullPath, "text/plain", outputFilename);
        }

        [ActionName("GetJSON")]
        [HttpGet]
        public async Task<ActionResult> GetJSON()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDs(Request);
            string outputFilename = GetOutputFilename(requestIDs, "json");
            string outputFileFullPath = System.IO.Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = GetGraph(requestIDs);
            AnnotationVizLib.SimpleODataClient.SimpleODataSpatialDataFactory.AppendSpatialDataFromOData(neuronGraph, VikingWebAppSettings.AppSettings.ODataURL, requestIDs, GetNumHops());

            NeuronJSONView JsonGraph = NeuronJSONView.ToJSON(neuronGraph);

            JsonGraph.SaveJSON(outputFileFullPath);

            return File(outputFileFullPath, "text/plain", outputFilename);
        }

        private string GetAndCreateOutputDirectory( )
        {
            string output_dir = "~/Output";
            if (Server != null)
                output_dir = Server.MapPath(output_dir);

            if (!System.IO.Directory.Exists(output_dir))
                System.IO.Directory.CreateDirectory(output_dir);

            return output_dir;
        }

        private NeuronGraph GetGraph(ICollection<long> requestIDs)
        {
            string EndpointURL = AppSettings.WebServiceURL;
            
            ConnectionFactory.SetConnection(EndpointURL, AppSettings.EndpointCredentials);
             
            if (requestIDs == null || requestIDs.Count == 0)
                requestIDs = Queries.GetLinkedStructureParentIDs(); 

            return WCFNeuronFactory.BuildGraph(requestIDs, GetNumHops(), EndpointURL, AppSettings.EndpointCredentials);
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
