using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using AnnotationVizLib;
using GraphLib;

using VikingWebAppSettings;

namespace DataExport.Controllers
{
    public class MotifController : Controller
    {
        public string DefaultOutputFile = "motifs";

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
        // GET: /Motifs/ 
        [ActionName("GetDot")]
        public ActionResult GetDot()
        {
            string EndpointURL = AppSettings.WebServiceURL; 
            string userDotDirectory = Server.MapPath("~/Output/");

            if (!System.IO.Directory.Exists(userDotDirectory))
                System.IO.Directory.CreateDirectory(userDotDirectory);

            string outputFilename = GetOutputFilename("dot");
            string userDotFileFullPath = System.IO.Path.Combine(userDotDirectory, outputFilename);

            MotifGraph motifGraph = MotifGraph.BuildGraph(EndpointURL, AppSettings.EndpointCredentials);
            MotifDOTView DotGraph = MotifDOTView.ToDOT(motifGraph);
            DotGraph.SaveDOT(userDotFileFullPath);

            return File(userDotFileFullPath, "text/plain", outputFilename);            
        }


        [ActionName("GetTLP")]
        public ActionResult GetTLP()
        {
            string EndpointURL = AppSettings.WebServiceURL; 
            string userDotDirectory = Server.MapPath("~/Output/");

            if (!System.IO.Directory.Exists(userDotDirectory))
                System.IO.Directory.CreateDirectory(userDotDirectory);

            string outputFilename = GetOutputFilename("tlp");
            string userDotFileFullPath = System.IO.Path.Combine(userDotDirectory, outputFilename);
             
            MotifGraph motifGraph = MotifGraph.BuildGraph(EndpointURL, AppSettings.EndpointCredentials);
            MotifTLPView TlpGraph = MotifTLPView.ToTLP(motifGraph);
            TlpGraph.SaveTLP(userDotFileFullPath);


            return File(userDotFileFullPath, "text/plain", outputFilename);
        } 
    }
}
