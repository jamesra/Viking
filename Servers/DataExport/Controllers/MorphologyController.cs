using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using AnnotationVizLib;
using GraphLib;
using VikingWebAppSettings;

namespace DataExport.Controllers
{
    public class MorphologyController : Controller
    { 
        public static string DefaultOutputFile = "morphology";

        private static long _next_id = 0;

        public static long NextFilenameID
        {
            get
            {
                return _next_id++; 
            }
        }

        public string GetOutputFilename()
        {
            return string.Format("{0}{1}.tlp", DefaultOutputFile, NextFilenameID); 
        }

               
        [ActionName("GetTLP")]
        public ActionResult GetTLP()
        {
            string OutputFile = GetOutputFilename(); GetOutputFilename();
            string userOutputDirectory = GetAndCreateOutputDirectories("~/Output/");
            string userOutputFileFullPath = System.IO.Path.Combine(userOutputDirectory, OutputFile);
            Scale scale = AppSettings.GetScale();
             
            StructureMorphologyColorMap colorMap = new StructureMorphologyColorMap(GetStructureTypeColorMap(),
                                                                                   GetStructureColorMap(),
                                                                                   GetColorMapImage());

            MorphologyGraph structure_graph = GetGraph();
            MorphologyTLPView TlpGraph = MorphologyTLPView.ToTLP(structure_graph, scale, colorMap);
            TlpGraph.SaveTLP(userOutputFileFullPath);

            return File(userOutputFileFullPath, "text/plain", OutputFile); 
        }

        private ColorMapWithImages GetColorMapImage()
        {
            string ColorMapImagePath = AppSettings.GetApplicationSetting("DefaultLocationColorMapsPath");

            return ColorMapWithImages.CreateFromConfigFile(ColorMapImagePath);
        }

        private ColorMapWithLong GetStructureColorMap()
        {
            string ColorMapPath = AppSettings.GetApplicationSetting("DefaultStructureColorsPath");
            return ColorMapWithLong.CreateFromConfigFile(ColorMapPath);
        }
         
        private ColorMapWithLong GetStructureTypeColorMap()
        {
            string ColorMapPath = AppSettings.GetApplicationSetting("DefaultStructureTypeColorsPath");
            return ColorMapWithLong.CreateFromConfigFile(ColorMapPath);
        }

        /// <summary>
        /// Get output directory for the path, create directories if they do not exist
        /// </summary>
        /// <param name="output_path"></param>
        /// <returns></returns>
        private string GetAndCreateOutputDirectories(string output_dir)
        {
            string userDotDirectory = "Output";
            if (Server != null)
                userDotDirectory = Server.MapPath(output_dir); //"~/Dot/");
              
            if (!System.IO.Directory.Exists(userDotDirectory))
                System.IO.Directory.CreateDirectory(userDotDirectory);

            return userDotDirectory;
        }
          
        private MorphologyGraph GetGraph()
        {
            AnnotationVizLib.ConnectionFactory.SetConnection(AppSettings.WebServiceURL , AppSettings.EndpointCredentials);

            ICollection<long> requestIDs = RequestVariables.GetQueryStringIDs(Request);
            if (requestIDs == null || requestIDs.Count == 0)
                requestIDs = Queries.GetLinkedStructureParentIDs();

            return MorphologyGraph.BuildGraphs(requestIDs, false, AppSettings.WebServiceURL, AppSettings.EndpointCredentials);
        }
    }
}
