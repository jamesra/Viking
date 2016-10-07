using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using AnnotationVizLib;
using GraphLib;
using VikingWebAppSettings;
using Geometry;

namespace DataExport.Controllers
{
    public class MorphologyController : Controller
    {

        public string GetOutputFilename(ICollection<long> requestIDs, string ext)
        { 
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
        public ActionResult PostTLP()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDs(Request);

            string OutputFile = GetOutputFilename(requestIDs, "tlp");
            string userOutputDirectory = GetAndCreateOutputDirectories();
            string userOutputFileFullPath = System.IO.Path.Combine(userOutputDirectory, OutputFile);
            Scale scale = AppSettings.GetScale();

            StructureMorphologyColorMap colorMap = new StructureMorphologyColorMap(GetStructureTypeColorMap(),
                                                                                   GetStructureColorMap(),
                                                                                   GetColorMapImage());

            MorphologyGraph structure_graph = GetGraph(requestIDs);
            MorphologyTLPView TlpGraph = MorphologyTLPView.ToTLP(structure_graph, scale, colorMap, AppSettings.VolumeURL);
            TlpGraph.SaveTLP(userOutputFileFullPath);

            return RedirectToFile(OutputFile);
        }

        [HttpPost()]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult PostJSON()
        { 
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.QueryString);

            string OutputFile = GetOutputFilename(requestIDs, "json");
            string userOutputDirectory = GetAndCreateOutputDirectories();
            string userOutputFileFullPath = System.IO.Path.Combine(userOutputDirectory, OutputFile);
            Scale scale = AppSettings.GetScale();

            MorphologyGraph structure_graph = GetGraph(requestIDs);
            MorphologyJSONView JSONGraph = MorphologyJSONView.ToJSON(structure_graph);
            JSONGraph.SaveJSON(userOutputFileFullPath);

            return RedirectToFile(OutputFile);
        }


        [ActionName("GetTLP")]
        public ActionResult GetTLP()
        { 
            ICollection<long> requestIDs = RequestVariables.GetIDs(Request);

            string OutputFile = GetOutputFilename(requestIDs, "tlp");
            string userOutputDirectory = GetAndCreateOutputDirectories();
            string userOutputFileFullPath = System.IO.Path.Combine(userOutputDirectory, OutputFile);
            Scale scale = AppSettings.GetScale();
             
            StructureMorphologyColorMap colorMap = new StructureMorphologyColorMap(GetStructureTypeColorMap(),
                                                                                   GetStructureColorMap(),
                                                                                   GetColorMapImage());

            MorphologyGraph structure_graph = GetGraph(requestIDs);
            MorphologyTLPView TlpGraph = MorphologyTLPView.ToTLP(structure_graph, scale, colorMap, AppSettings.VolumeURL);
            TlpGraph.SaveTLP(userOutputFileFullPath);

            return File(userOutputFileFullPath, "text/plain", OutputFile); 
        }

        [ActionName("GetJSON")]
        public ActionResult GetJSON()
        {

            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.QueryString);

            string OutputFile = GetOutputFilename(requestIDs, "json");
            string userOutputDirectory = GetAndCreateOutputDirectories();
            string userOutputFileFullPath = System.IO.Path.Combine(userOutputDirectory, OutputFile);
            Scale scale = AppSettings.GetScale();

            MorphologyGraph structure_graph = GetGraph(requestIDs);
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
        private string GetAndCreateOutputDirectories()
        {
            string output_dir = "~/Output";
            if (Server != null)
                output_dir = Server.MapPath(output_dir);
              
            if (!System.IO.Directory.Exists(output_dir))
                System.IO.Directory.CreateDirectory(output_dir);

            return output_dir;
        }
          
        private MorphologyGraph GetGraph(ICollection<long> requestIDs)
        {
            AnnotationVizLib.ConnectionFactory.SetConnection(AppSettings.WebServiceURL , AppSettings.EndpointCredentials);

            if (requestIDs == null || requestIDs.Count == 0)
                requestIDs = Queries.GetLinkedStructureParentIDs();

            return MorphologyGraph.BuildGraphs(requestIDs, true, AppSettings.WebServiceURL, AppSettings.EndpointCredentials);
        }
    }
}
