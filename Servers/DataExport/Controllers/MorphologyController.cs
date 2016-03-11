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

        public string GetOutputFilename(string ext)
        {
            ICollection<long> requestIDs = RequestVariables.GetQueryStringIDs(Request).Cast<long>().ToArray();
            string ID_List = "";
            bool first = true;
            if (requestIDs.Count == 0)
                ID_List = "ALL";
            foreach (long ID in requestIDs)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    ID_List += "_";
                }

                ID_List += ID.ToString();
                if (ID_List.Length > 200)
                {
                    ID_List += "etc";
                    break;
                }
            }

            return string.Format("morph-{0}.{1}", ID_List, ext);
        }


        [ActionName("GetTLP")]
        public ActionResult GetTLP()
        {
            string OutputFile = GetOutputFilename("tlp");
            string userOutputDirectory = GetAndCreateOutputDirectories("~/Output/");
            string userOutputFileFullPath = System.IO.Path.Combine(userOutputDirectory, OutputFile);
            Scale scale = AppSettings.GetScale();
             
            StructureMorphologyColorMap colorMap = new StructureMorphologyColorMap(GetStructureTypeColorMap(),
                                                                                   GetStructureColorMap(),
                                                                                   GetColorMapImage());

            MorphologyGraph structure_graph = GetGraph();
            MorphologyTLPView TlpGraph = MorphologyTLPView.ToTLP(structure_graph, scale, colorMap, AppSettings.VolumeURL);
            TlpGraph.SaveTLP(userOutputFileFullPath);

            return File(userOutputFileFullPath, "text/plain", OutputFile); 
        }

        [ActionName("GetJSON")]
        public ActionResult GetJSON()
        {
            string OutputFile = GetOutputFilename("json");
            string userOutputDirectory = GetAndCreateOutputDirectories("~/Output/");
            string userOutputFileFullPath = System.IO.Path.Combine(userOutputDirectory, OutputFile);
            Scale scale = AppSettings.GetScale();

            MorphologyGraph structure_graph = GetGraph();
            MorphologyJSONView JSONGraph = MorphologyJSONView.ToJSON(structure_graph);
            JSONGraph.SaveJSON(userOutputFileFullPath);

            return File(userOutputFileFullPath, "text/plain", OutputFile);
        }

        private ColorMapWithImages GetColorMapImage()
        {
            string ColorMapImagePath = AppSettings.GetApplicationSetting("DefaultLocationColorMapsPath");
            if (ColorMapImagePath == null || ColorMapImagePath.Length == 0)
                return null;


            /*try
            {
             */
                return ColorMapWithImages.CreateFromConfigFile(ColorMapImagePath);
            /*
            }
            catch(System.IO.DirectoryNotFoundException)
            {}
            catch (System.IO.FileNotFoundException)
            { }
            */
             
        }

        private ColorMapWithLong GetStructureColorMap()
        {
            string ColorMapPath = AppSettings.GetApplicationSetting("DefaultStructureColorsPath");
            if (ColorMapPath == null || ColorMapPath.Length == 0)
                return null;  

            return ColorMapWithLong.CreateFromConfigFile(ColorMapPath); 
        }
         
        private ColorMapWithLong GetStructureTypeColorMap()
        {
            string ColorMapPath = AppSettings.GetApplicationSetting("DefaultStructureTypeColorsPath");
            if (ColorMapPath == null || ColorMapPath.Length == 0)
                return null; 
            
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

            return MorphologyGraph.BuildGraphs(requestIDs, true, AppSettings.WebServiceURL, AppSettings.EndpointCredentials);
        }
    }
}
