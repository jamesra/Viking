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
using AnnotationVizLib.WCFClient;
using System.Threading.Tasks;

namespace DataExport.Controllers
{
    public class MorphologyController : Controller
    {

        public string GetOutputFilename(ICollection<long> requestIDs, string ext)
        {
            string ID_List = OutputNameGenerator.GetFileFriendlyIDList(requestIDs);
            string date = OutputNameGenerator.GetFileFriendlyDateString();
            return string.Format("morph-{0} {1}.{2}", ID_List, date, ext);
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
        public async Task<ActionResult> PostTLP()
        {
            ICollection<long> requestIDs = await RequestVariables.GetIDs(Request);

            string OutputFile = GetOutputFilename(requestIDs, "tlp");
            string userOutputDirectory = GetAndCreateOutputDirectories();
            string userOutputFileFullPath = System.IO.Path.Combine(userOutputDirectory, OutputFile);
            Scale scale = AppSettings.GetScale();

            StructureMorphologyColorMap colorMap = new StructureMorphologyColorMap(GetStructureTypeColorMap(),
                                                                                   GetStructureColorMap(),
                                                                                   GetColorMapImage());

            MorphologyGraph structure_graph = GetGraph(requestIDs);
            if (RequestedStickFigure())
                structure_graph.ToStickFigure();
            MorphologyTLPView TlpGraph = MorphologyTLPView.ToTLP(structure_graph, scale, colorMap, AppSettings.VolumeURL);
            TlpGraph.SaveTLP(userOutputFileFullPath);

            return RedirectToFile(OutputFile);
        }

        [HttpPost()]
        [AcceptVerbs(HttpVerbs.Post)]
        public async Task<ActionResult> PostJSON()
        { 
            ICollection<long> requestIDs = await RequestVariables.GetIDsFromQueryData(Request.QueryString);

            string OutputFile = GetOutputFilename(requestIDs, "json");
            string userOutputDirectory = GetAndCreateOutputDirectories();
            string userOutputFileFullPath = System.IO.Path.Combine(userOutputDirectory, OutputFile);
            Scale scale = AppSettings.GetScale();

            MorphologyGraph structure_graph = GetGraph(requestIDs);
            if (RequestedStickFigure())
                structure_graph.ToStickFigure();
            MorphologyJSONView JSONGraph = MorphologyJSONView.ToJSON(structure_graph);
            JSONGraph.SaveJSON(userOutputFileFullPath);

            return RedirectToFile(OutputFile);
        }

        [HttpPost()]
        [AcceptVerbs(HttpVerbs.Post)]
        public async Task<ActionResult> PostDAE()
        {
            ICollection<long> requestIDs = await RequestVariables.GetIDsFromQueryData(Request.QueryString);

            string OutputFile = GetOutputFilename(requestIDs, "dae");
            string userOutputDirectory = GetAndCreateOutputDirectories();
            string userOutputFileFullPath = System.IO.Path.Combine(userOutputDirectory, OutputFile); 

            StructureMorphologyColorMap colorMap = new StructureMorphologyColorMap(GetStructureTypeColorMap(),
                                                                                   GetStructureColorMap(),
                                                                                   GetColorMapImage());

            MorphologyGraph structure_graph = GetGraph(requestIDs);
            if (RequestedStickFigure())
                structure_graph.ToStickFigure();
            MorphologyMesh.MorphologyColladaView view = new MorphologyMesh.MorphologyColladaView(structure_graph.scale, colorMap);
            view.Add(structure_graph);
            ColladaIO.DynamicRenderMeshColladaSerializer.SerializeToFile(view, userOutputFileFullPath);
            
            return RedirectToFile(OutputFile);
        }


        [ActionName("GetTLP")]
        public async Task<ActionResult> GetTLP()
        { 
            ICollection<long> requestIDs = await RequestVariables.GetIDs(Request);

            string OutputFile = GetOutputFilename(requestIDs, "tlp");
            string userOutputDirectory = GetAndCreateOutputDirectories();
            string userOutputFileFullPath = System.IO.Path.Combine(userOutputDirectory, OutputFile);
             
            StructureMorphologyColorMap colorMap = new StructureMorphologyColorMap(GetStructureTypeColorMap(),
                                                                                   GetStructureColorMap(),
                                                                                   GetColorMapImage());

            MorphologyGraph structure_graph = GetGraph(requestIDs);
            if (RequestedStickFigure())
                structure_graph.ToStickFigure();
            MorphologyTLPView TlpGraph = MorphologyTLPView.ToTLP(structure_graph, structure_graph.scale, colorMap, AppSettings.VolumeURL);
            TlpGraph.SaveTLP(userOutputFileFullPath);

            return File(userOutputFileFullPath, "text/plain", OutputFile); 
        }

        [ActionName("GetJSON")]
        public async Task<ActionResult> GetJSON()
        {

            ICollection<long> requestIDs = await RequestVariables.GetIDsFromQueryData(Request.QueryString);

            string OutputFile = GetOutputFilename(requestIDs, "json");
            string userOutputDirectory = GetAndCreateOutputDirectories();
            string userOutputFileFullPath = System.IO.Path.Combine(userOutputDirectory, OutputFile);

            MorphologyGraph structure_graph = GetGraph(requestIDs);
            if (RequestedStickFigure())
                structure_graph.ToStickFigure();

            MorphologyJSONView JSONGraph = MorphologyJSONView.ToJSON(structure_graph);
            JSONGraph.SaveJSON(userOutputFileFullPath);

            return File(userOutputFileFullPath, "application/json", OutputFile);
        }

        [ActionName("GetDAE")]
        public async Task<ActionResult> GetDAE()
        { 
            ICollection<long> requestIDs = await RequestVariables.GetIDsFromQueryData(Request.QueryString);

            string OutputFile = GetOutputFilename(requestIDs, "dae");
            string userOutputDirectory = GetAndCreateOutputDirectories();
            string userOutputFileFullPath = System.IO.Path.Combine(userOutputDirectory, OutputFile);
            Scale scale = AppSettings.GetScale();

            StructureMorphologyColorMap colorMap = new StructureMorphologyColorMap(GetStructureTypeColorMap(),
                                                                                   GetStructureColorMap(),
                                                                                   GetColorMapImage());

            MorphologyGraph structure_graph = GetGraph(requestIDs);
            if (RequestedStickFigure())
                structure_graph.ToStickFigure();

            MorphologyMesh.MorphologyColladaView view = new MorphologyMesh.MorphologyColladaView(structure_graph.scale, colorMap);
            view.Add(structure_graph);
            ColladaIO.DynamicRenderMeshColladaSerializer.SerializeToFile(view, userOutputFileFullPath);
            
            return File(userOutputFileFullPath, "model/vnd.collada+xml", OutputFile);
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
          
        private async Task<MorphologyGraph> GetGraph(ICollection<long> requestIDs)
        {
            AnnotationVizLib.WCFClient.ConnectionFactory.SetConnection(AppSettings.WebServiceURL , AppSettings.EndpointCredentials);

            if (requestIDs == null || requestIDs.Count == 0)
                requestIDs = Queries.GetLinkedStructureParentIDs();

            return WCFMorphologyFactory.FromWCF(requestIDs, true, AppSettings.WebServiceURL, AppSettings.EndpointCredentials);
        }

        private bool RequestedStickFigure()
        {
            string hopstr = Request.RequestContext.HttpContext.Request.QueryString["stick"];
            if (hopstr == null)
            {
                hopstr = Request.RequestContext.HttpContext.Request.QueryString["Stick"];
                if (hopstr == null)
                {
                    return false;
                }
            }

            try
            {
                return Convert.ToUInt32(hopstr) > 0;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
