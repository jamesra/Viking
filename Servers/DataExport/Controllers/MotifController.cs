using AnnotationVizLib;
using AnnotationVizLib.OData; // Use correct namespace
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http; 
using Microsoft.AspNetCore.Hosting;
using VikingWebAppSettings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DataExport.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class MotifController : Controller
    {
        private readonly IWebHostEnvironment _env;
        public MotifController(IWebHostEnvironment env)
        {
            _env = env;
        }

        public string DefaultOutputFile = "motifs";
        private static long _next_id = 0;
        public static long NextFilenameID => _next_id++;

        private string GetOutputFilename(string ext)
        {
            return $"{DefaultOutputFile}{NextFilenameID}{OutputNameGenerator.GetFileFriendlyDateString()}.{ext}";
        }

        private string GetAndCreateOutputDirectory()
        {
            string outputDir = Path.Combine(_env.ContentRootPath, "Output");
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);
            return outputDir;
        }

        [HttpGet]
        public async Task<IActionResult> GetDot()
        {
            string userDotDirectory = GetAndCreateOutputDirectory();
            string outputFilename = GetOutputFilename("dot");
            string userDotFileFullPath = Path.Combine(userDotDirectory, outputFilename);

            MotifGraph motifGraph = await GetMotifGraphAsync();
            motifGraph.AddEdgeStatistics();
            MotifDOTView DotGraph = MotifDOTView.ToDOT(motifGraph);
            DotGraph.SaveDOT(userDotFileFullPath);

            return PhysicalFile(userDotFileFullPath, "text/plain", outputFilename);
        }

        [HttpGet]
        public async Task<IActionResult> GetTLP()
        {
            string userDotDirectory = GetAndCreateOutputDirectory();
            string outputFilename = GetOutputFilename("tlp");
            string userDotFileFullPath = Path.Combine(userDotDirectory, outputFilename);

            MotifGraph motifGraph = await GetMotifGraphAsync();
            motifGraph.AddEdgeStatistics();
            MotifTLPView TlpGraph = MotifTLPView.ToTLP(motifGraph, AppSettings.VolumeURL);
            TlpGraph.SaveTLP(userDotFileFullPath);

            return PhysicalFile(userDotFileFullPath, "text/plain", outputFilename);
        }

        [HttpGet]
        public async Task<IActionResult> GetJSON()
        {
            string userDotDirectory = GetAndCreateOutputDirectory();
            string outputFilename = GetOutputFilename("json");
            string userJSONFullPath = Path.Combine(userDotDirectory, outputFilename);

            MotifGraph motifGraph = await GetMotifGraphAsync();
            motifGraph.AddEdgeStatistics();
            MotifJSONView JsonGraph = MotifJSONView.ToJSON(motifGraph);
            JsonGraph.SaveJSON(userJSONFullPath);

            return PhysicalFile(userJSONFullPath, "text/plain", outputFilename);
        }

        private async Task<MotifGraph> GetMotifGraphAsync()
        {
            // TODO: Replace with ODataClient logic
            // Example: await ODataClient.GetMotifGraphAsync(...)
            // For now, return a stub or throw NotImplementedException
            throw new NotImplementedException("ODataClient motif graph retrieval not yet implemented.");
        }
    }
}
