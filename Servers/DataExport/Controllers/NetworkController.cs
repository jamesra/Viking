using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Net.Http;
using DataExport;
using AnnotationVizLib;
using VikingWebAppSettings;
using AnnotationVizLib.WCFClient;

namespace DataExport.Controllers
{
    public class NetworkController : Controller
    {
        public string GetOutputFilename(ICollection<long> requestIDs, string ext)
        { 
            string ID_List = "";
            bool first = true;
            if (requestIDs.Count == 0)
                ID_List = "ALL";

            foreach (long ID in requestIDs)
            {
                if(first)
                {
                    first = false;
                }
                else
                {
                    ID_List += "_";
                }

                ID_List += ID.ToString();
                if (ID_List.Length > 140)
                {
                    ID_List += "etc";
                    break;
                }
            }

            return string.Format("nw-{0}_hops_{1}.{2}", ID_List, GetNumHops(), ext);
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
        public ActionResult PostDot(HttpPostedFileBase req)
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
        public ActionResult PostTLP(HttpPostedFileBase req)
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
        public ActionResult PostGML(HttpPostedFileBase req)
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
        public ActionResult PostJSON(HttpPostedFileBase req)
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
        public ActionResult GetDot()
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
        public ActionResult GetTLP()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDs(Request);
            string outputFilename = GetOutputFilename(requestIDs, "tlp");
            string outputFileFullPath = System.IO.Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = GetGraph(requestIDs);
            NeuronTLPView TlpGraph = NeuronTLPView.ToTLP(neuronGraph, AppSettings.VolumeURL);
            TlpGraph.SaveTLP(outputFileFullPath);

            return File(outputFileFullPath, "text/plain", outputFilename);
        }

        [ActionName("GetGML")]
        public ActionResult GetGML()
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
        public ActionResult GetJSON()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDs(Request);
            string outputFilename = GetOutputFilename(requestIDs, "json");
            string outputFileFullPath = System.IO.Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = GetGraph(requestIDs);
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
