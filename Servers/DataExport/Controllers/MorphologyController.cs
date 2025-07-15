using AnnotationVizLib;
using AnnotationVizLib.OData; // Use correct namespace
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using VikingWebAppSettings;
using UnitsAndScale;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DataExport.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class MorphologyController : Controller
    {
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
        public MorphologyController(IWebHostEnvironment env)
        {
            _env = env;
        }

        private string GetOutputFilename(ICollection<long> requestIDs, string ext)
        {
            string ID_List = OutputNameGenerator.GetFileFriendlyIDList(requestIDs);
            string date = OutputNameGenerator.GetFileFriendlyDateString();
            return $"morph-{ID_List} {date}.{ext}";
        }

        private string GetAndCreateOutputDirectory()
        {
            string outputDir = Path.Combine(_env.ContentRootPath, "Output");
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);
            return outputDir;
        }

        private IActionResult RedirectToFile(string outputFilename)
        {
            string url = $"/Output/{outputFilename}";
            Response.StatusCode = StatusCodes.Status201Created;
            Response.Headers["Location"] = url;
            return Redirect(url);
        }

        [HttpPost]
        public async Task<IActionResult> PostTLP()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFile = GetOutputFilename(requestIDs, "tlp");
            string userOutputDirectory = GetAndCreateOutputDirectory();
            string userOutputFileFullPath = Path.Combine(userOutputDirectory, outputFile);
            Scale scale = AppSettings.GetScale();

            StructureMorphologyColorMap colorMap = new StructureMorphologyColorMap(GetStructureTypeColorMap(),
                                                                                   GetStructureColorMap(),
                                                                                   GetColorMapImage());

            MorphologyGraph structure_graph = await GetGraphAsync(requestIDs);
            if (RequestedStickFigure())
                structure_graph.ToStickFigure();
            MorphologyTLPView TlpGraph = MorphologyTLPView.ToTLP(structure_graph, scale, colorMap, AppSettings.VolumeURL);
            TlpGraph.SaveTLP(userOutputFileFullPath);

            return RedirectToFile(outputFile);
        }

        [HttpPost]
        public async Task<IActionResult> PostJSON()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFile = GetOutputFilename(requestIDs, "json");
            string userOutputDirectory = GetAndCreateOutputDirectory();
            string userOutputFileFullPath = Path.Combine(userOutputDirectory, outputFile);
            Scale scale = AppSettings.GetScale();

            MorphologyGraph structure_graph = await GetGraphAsync(requestIDs);
            if (RequestedStickFigure())
                structure_graph.ToStickFigure();
            MorphologyJSONView JSONGraph = MorphologyJSONView.ToJSON(structure_graph);
            JSONGraph.SaveJSON(userOutputFileFullPath);

            return RedirectToFile(outputFile);
        }

        /*
        [HttpPost]
        public async Task<IActionResult> PostDAE()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFile = GetOutputFilename(requestIDs, "dae");
            string userOutputDirectory = GetAndCreateOutputDirectory();
            string userOutputFileFullPath = Path.Combine(userOutputDirectory, outputFile);
            Scale scale = AppSettings.GetScale();

            StructureMorphologyColorMap colorMap = new StructureMorphologyColorMap(GetStructureTypeColorMap(),
                                                                                   GetStructureColorMap(),
                                                                                   GetColorMapImage());

            MorphologyGraph structure_graph = await GetGraphAsync(requestIDs);
            if (RequestedStickFigure())
                structure_graph.ToStickFigure();
            MorphologyDAEView DaeGraph = MorphologyDAEView.ToDAE(structure_graph, scale, colorMap, AppSettings.VolumeURL);
            DaeGraph.SaveDAE(userOutputFileFullPath);

            return RedirectToFile(outputFile);
        }
        */

        [HttpGet]
        public async Task<IActionResult> GetTLP()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFile = GetOutputFilename(requestIDs, "tlp");
            string userOutputDirectory = GetAndCreateOutputDirectory();
            string userOutputFileFullPath = Path.Combine(userOutputDirectory, outputFile);
            Scale scale = AppSettings.GetScale();

            StructureMorphologyColorMap colorMap = new StructureMorphologyColorMap(GetStructureTypeColorMap(),
                                                                                   GetStructureColorMap(),
                                                                                   GetColorMapImage());

            MorphologyGraph structure_graph = await GetGraphAsync(requestIDs);
            if (RequestedStickFigure())
                structure_graph.ToStickFigure();
            MorphologyTLPView TlpGraph = MorphologyTLPView.ToTLP(structure_graph, scale, colorMap, AppSettings.VolumeURL);
            TlpGraph.SaveTLP(userOutputFileFullPath);

            return PhysicalFile(userOutputFileFullPath, "text/plain", outputFile);
        }

        [HttpGet]
        public async Task<IActionResult> GetJSON()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFile = GetOutputFilename(requestIDs, "json");
            string userOutputDirectory = GetAndCreateOutputDirectory();
            string userOutputFileFullPath = Path.Combine(userOutputDirectory, outputFile);

            MorphologyGraph structure_graph = await GetGraphAsync(requestIDs);
            if (RequestedStickFigure())
                structure_graph.ToStickFigure();

            MorphologyJSONView JSONGraph = MorphologyJSONView.ToJSON(structure_graph);
            JSONGraph.SaveJSON(userOutputFileFullPath);

            return PhysicalFile(userOutputFileFullPath, "application/json", outputFile);
        }

        /*
        [HttpGet]
        public async Task<IActionResult> GetDAE()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFile = GetOutputFilename(requestIDs, "dae");
            string userOutputDirectory = GetAndCreateOutputDirectory();
            string userOutputFileFullPath = Path.Combine(userOutputDirectory, outputFile);
            Scale scale = AppSettings.GetScale();

            StructureMorphologyColorMap colorMap = new StructureMorphologyColorMap(GetStructureTypeColorMap(),
                                                                                   GetStructureColorMap(),
                                                                                   GetColorMapImage());

            MorphologyGraph structure_graph = await GetGraphAsync(requestIDs);
            if (RequestedStickFigure())
                structure_graph.ToStickFigure();
            MorphologyDAEView DaeGraph = MorphologyDAEView.ToDAE(structure_graph, scale, colorMap, AppSettings.VolumeURL);
            DaeGraph.SaveDAE(userOutputFileFullPath);

            return PhysicalFile(userOutputFileFullPath, "text/plain", outputFile);
        }
        */

        private ColorMapWithLong GetStructureTypeColorMap()
        {
            // TODO: Implement structure type color map retrieval
            throw new NotImplementedException("Structure type color map retrieval not yet implemented.");
        }

        private ColorMapWithLong GetStructureColorMap()
        {
            // TODO: Implement structure color map retrieval
            throw new NotImplementedException("Structure color map retrieval not yet implemented.");
        }

        private ColorMapWithImages GetColorMapImage()
        {
            // TODO: Implement color map image retrieval
            throw new NotImplementedException("Color map image retrieval not yet implemented.");
        }

        private async Task<MorphologyGraph> GetGraphAsync(ICollection<long> requestIDs)
        {
            // TODO: Replace with ODataClient logic
            // Example: await ODataClient.GetMorphologyGraphAsync(...)
            // For now, return a stub or throw NotImplementedException
            throw new NotImplementedException("ODataClient morphology graph retrieval not yet implemented.");
        }

        private bool RequestedStickFigure()
        {
            if (Request.Query.ContainsKey("stick"))
            {
                if (uint.TryParse(Request.Query["stick"], out uint stick) && stick > 0)
                    return true;
            }
            if (Request.Query.ContainsKey("Stick"))
            {
                if (uint.TryParse(Request.Query["Stick"], out uint stick) && stick > 0)
                    return true;
            }
            return false;
        }
    }
}
