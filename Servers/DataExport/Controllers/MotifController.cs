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
        //
        // GET: /Motifs/ 
        [ActionName("GetDot")]
        public ActionResult GetDot()
        {
            string database = AppSettings.GetDatabaseCatalogName();
            string EndpointURL = string.Format(AppSettings.WebServiceURLTemplate, database);
            string userDotDirectory = Server.MapPath("~/Dot/");

            if (!System.IO.Directory.Exists(userDotDirectory))
                System.IO.Directory.CreateDirectory(userDotDirectory);

            string userDotFileFullPath = System.IO.Path.Combine(userDotDirectory, "motifs.dot");

            MotifGraph motifGraph = MotifGraph.BuildGraph(EndpointURL, AppSettings.EndpointCredentials);
            MotifDOTView DotGraph = MotifDOTView.ToDOT(motifGraph);
            DotGraph.SaveDOT(userDotFileFullPath);
             
            return File(userDotFileFullPath, "text/plain", "motifs.dot");            
        }


        [ActionName("GetTLP")]
        public ActionResult GetTLP()
        {
            string database = AppSettings.GetDatabaseCatalogName();
            string EndpointURL = string.Format(AppSettings.WebServiceURLTemplate, database);
            string userDotDirectory = Server.MapPath("~/Dot/");

            if (!System.IO.Directory.Exists(userDotDirectory))
                System.IO.Directory.CreateDirectory(userDotDirectory);

            string userDotFileFullPath = System.IO.Path.Combine(userDotDirectory, "motifs.tlp");
             
            MotifGraph motifGraph = MotifGraph.BuildGraph(EndpointURL, AppSettings.EndpointCredentials);
            MotifTLPView TlpGraph = MotifTLPView.ToTLP(motifGraph);
            TlpGraph.SaveTLP(userDotFileFullPath);
            

            return File(userDotFileFullPath, "text/plain", "motifs.tlp");
        } 
    }
}
