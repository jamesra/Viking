using AnnotationVizLib;
using AnnotationVizLib.OData;
using VikingWebAppSettings;

namespace DataExport.Controllers;

/// <summary>
/// Controller for exporting network graph data in various formats (DOT, TLP, GML, JSON).
/// </summary>
[ApiController]
[Route("[controller]/[action]")]
public class NetworkController : Controller
{
    private readonly IWebHostEnvironment _env;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkController"/> class.
    /// </summary>
    /// <param name="env">The web host environment.</param>
    public NetworkController(IWebHostEnvironment env)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }

    private string GetOutputFilename(ICollection<long> requestIDs, string ext)
    {
        string idList = OutputNameGenerator.GetFileFriendlyIDList(requestIDs);
        string date = OutputNameGenerator.GetFileFriendlyDateString();
        return $"nw-{idList}_hops_{GetNumHops()} {date}.{ext}";
    }

    private string GetAndCreateOutputDirectory()
    {
        string outputDir = Path.Combine(_env.ContentRootPath, "Output");
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }
        return outputDir;
    }

    private IActionResult RedirectToFile(string outputFilename)
    {
        string url = $"/Output/{outputFilename}";
        Response.StatusCode = StatusCodes.Status201Created;
        Response.Headers.Location = url;
        return Redirect(url);
    }

    /// <summary>
    /// Exports network data in DOT format via POST request.
    /// </summary>
    /// <param name="req">The form file (not used, but required for routing).</param>
    /// <returns>Redirect to the generated DOT file.</returns>
    [HttpPost]
    public async Task<IActionResult> PostDot([FromForm] IFormFile? req)
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFilename = GetOutputFilename(requestIDs, "dot");
            string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
            NeuronDOTView DotGraph = NeuronDOTView.ToDOT(neuronGraph, false);
            DotGraph.SaveDOT(outputFileFullPath);
            return RedirectToFile(outputFilename);
        }

    /// <summary>
    /// Exports network data in TLP (Tulip) format via POST request.
    /// </summary>
    /// <param name="req">The form file (not used, but required for routing).</param>
    /// <returns>Redirect to the generated TLP file.</returns>
    [HttpPost]
    public async Task<IActionResult> PostTLP([FromForm] IFormFile? req)
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFilename = GetOutputFilename(requestIDs, "tlp");
            string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
            NeuronTLPView TlpGraph = NeuronTLPView.ToTLP(neuronGraph, AppSettings.VolumeURL);
            TlpGraph.SaveTLP(outputFileFullPath);
            return RedirectToFile(outputFilename);
        }

    /// <summary>
    /// Exports network data in GraphML format via POST request.
    /// </summary>
    /// <param name="req">The form file (not used, but required for routing).</param>
    /// <returns>Redirect to the generated GraphML file.</returns>
    [HttpPost]
    public async Task<IActionResult> PostGML([FromForm] IFormFile? req)
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFilename = GetOutputFilename(requestIDs, "graphml");
            string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
            NeuronGMLView GmlGraph = NeuronGMLView.ToGML(neuronGraph, AppSettings.VolumeURL);
            GmlGraph.SaveGML(outputFileFullPath);
            return RedirectToFile(outputFilename);
        }

    /// <summary>
    /// Exports network data in JSON format via POST request.
    /// </summary>
    /// <param name="req">The form file (not used, but required for routing).</param>
    /// <returns>Redirect to the generated JSON file.</returns>
    [HttpPost]
    public async Task<IActionResult> PostJSON([FromForm] IFormFile? req)
        {
            ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
            string outputFilename = GetOutputFilename(requestIDs, "json");
            string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

            NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
            NeuronJSONView JsonGraph = NeuronJSONView.ToJSON(neuronGraph);
            JsonGraph.SaveJSON(outputFileFullPath);
            return RedirectToFile(outputFilename);
        }

    /// <summary>
    /// Exports network data in DOT format via GET request.
    /// </summary>
    /// <returns>The generated DOT file for download.</returns>
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

    /// <summary>
    /// Exports network data in TLP (Tulip) format via GET request.
    /// </summary>
    /// <returns>The generated TLP file for download.</returns>
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

    /// <summary>
    /// Exports network data in GraphML format via GET request.
    /// </summary>
    /// <returns>The generated GraphML file for download.</returns>
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

    /// <summary>
    /// Exports network data in JSON format via GET request.
    /// </summary>
    /// <returns>The generated JSON file for download.</returns>
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
        // Use ODataClient logic to retrieve the graph
        return await Task.Run(() => 
            ODataNeuronFactory.FromOData(requestIDs, GetNumHops(), AppSettings.ODataURL));
    }

    private uint GetNumHops()
    {
        if (Request.Query.ContainsKey("hops") && 
            uint.TryParse(Request.Query["hops"], out uint hops))
        {
            return hops;
        }
        return 1;
    }
}
