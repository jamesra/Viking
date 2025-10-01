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
    public class NetworkController : Controller
    {
        private readonly IWebHostEnvironment _env;
        public NetworkController(IWebHostEnvironment env)
        {
            _env = env;
        }

        private string GetOutputFilename(ICollection<long> requestIDs, string ext)
        {
            string ID_List = OutputNameGenerator.GetFileFriendlyIDList(requestIDs);
            string date = OutputNameGenerator.GetFileFriendlyDateString();
            return $"nw-{ID_List}_hops_{GetNumHops()} {date}.{ext}";
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
        public async Task<IActionResult> PostDot([FromForm] IFormFile req)
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFilename = GetOutputFilename(requestIDs, "dot");
            string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
            NeuronDOTView DotGraph = NeuronDOTView.ToDOT(neuronGraph, false);
            DotGraph.SaveDOT(outputFileFullPath);
            return RedirectToFile(outputFilename);
        }

        [HttpPost]
        public async Task<IActionResult> PostTLP([FromForm] IFormFile req)
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFilename = GetOutputFilename(requestIDs, "tlp");
            string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
            NeuronTLPView TlpGraph = NeuronTLPView.ToTLP(neuronGraph, AppSettings.VolumeURL);
            TlpGraph.SaveTLP(outputFileFullPath);
            return RedirectToFile(outputFilename);
        }

        [HttpPost]
        public async Task<IActionResult> PostGML([FromForm] IFormFile req)
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFilename = GetOutputFilename(requestIDs, "graphml");
            string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
            NeuronGMLView GmlGraph = NeuronGMLView.ToGML(neuronGraph, AppSettings.VolumeURL);
            GmlGraph.SaveGML(outputFileFullPath);
            return RedirectToFile(outputFilename);
        }

        [HttpPost]
        public async Task<IActionResult> PostJSON([FromForm] IFormFile req)
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFilename = GetOutputFilename(requestIDs, "json");
            string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
            NeuronJSONView JsonGraph = NeuronJSONView.ToJSON(neuronGraph);
            JsonGraph.SaveJSON(outputFileFullPath);
            return RedirectToFile(outputFilename);
        }

        [HttpGet]
        public async Task<IActionResult> GetDot()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFilename = GetOutputFilename(requestIDs, "dot");
            string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
            NeuronDOTView DotGraph = NeuronDOTView.ToDOT(neuronGraph, false);
            DotGraph.SaveDOT(outputFileFullPath);
            return PhysicalFile(outputFileFullPath, "text/plain", outputFilename);
        }

        [HttpGet]
        public async Task<IActionResult> GetTLP()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFilename = GetOutputFilename(requestIDs, "tlp");
            string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
            // OData spatial data append here if needed
            NeuronTLPView TlpGraph = NeuronTLPView.ToTLP(neuronGraph, AppSettings.VolumeURL);
            TlpGraph.SaveTLP(outputFileFullPath);
            return PhysicalFile(outputFileFullPath, "text/plain", outputFilename);
        }

        [HttpGet]
        public async Task<IActionResult> GetGML()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFilename = GetOutputFilename(requestIDs, "graphml");
            string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
            NeuronGMLView GmlGraph = NeuronGMLView.ToGML(neuronGraph, AppSettings.VolumeURL);
            GmlGraph.SaveGML(outputFileFullPath);
            return PhysicalFile(outputFileFullPath, "text/plain", outputFilename);
        }

        [HttpGet]
        public async Task<IActionResult> GetJSON()
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFilename = GetOutputFilename(requestIDs, "json");
            string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
            // OData spatial data append here if needed
            NeuronJSONView JsonGraph = NeuronJSONView.ToJSON(neuronGraph);
            JsonGraph.SaveJSON(outputFileFullPath);
            return PhysicalFile(outputFileFullPath, "text/plain", outputFilename);
        }

        private async Task<NeuronGraph> GetGraphAsync(ICollection<long> requestIDs)
        {
            // TODO: Replace with ODataClient logic
            // Example: await ODataClient.GetGraphAsync(...)
            // For now, return a stub or throw NotImplementedException
            
            return ODataNeuronFactory.FromOData(requestIDs, GetNumHops(), AppSettings.ODataURL);

            throw new NotImplementedException("ODataClient graph retrieval not yet implemented.");
        }

        private uint GetNumHops()
        {
            if (Request.Query.ContainsKey("hops"))
            {
                if (uint.TryParse(Request.Query["hops"], out uint hops))
                    return hops;
            }
            return 1;
        }
    }
}
