using AnnotationVizLib;
using AnnotationVizLib.OData;

namespace DataExport.Controllers;

/// <summary>
/// Controller for exporting network graph data in various formats (DOT, TLP, GML, JSON).
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="NetworkController"/> class.
/// </remarks>
/// <param name="env">The web host environment.</param>
/// <param name="configuration">The configuration service.</param>
/*
 * The route prefix deliberately omits [action]. With it, the controller prefix already
 * resolved to "Network/GetTLP" and the action template "tlp" appended to it, so the
 * only reachable URL was Network/GetTLP/tlp with the format named twice. Actions now
 * carry explicit templates: the short form, and the longer form kept for compatibility.
 */
[ApiController]
[Route("[controller]")]
public class NetworkController(IWebHostEnvironment env, IConfiguration configuration) : Controller
{
    private readonly IWebHostEnvironment _env = env ?? throw new ArgumentNullException(nameof(env));
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    private Uri GetODataUrl()
    {
        string url = _configuration["AppSettings:ODataURL"]
            ?? throw new InvalidOperationException("AppSettings:ODataURL not configured");
        return new Uri(url);
    }

    private string GetVolumeUrl()
    {
        return _configuration["AppSettings:VolumeURL"]
            ?? throw new InvalidOperationException("AppSettings:VolumeURL not configured");
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
    [HttpPost("PostDot")]
    public async Task<IActionResult> PostDot([FromForm] IFormFile? req)
    {
        ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query, GetODataUrl());
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
    [HttpPost("PostTLP")]
    public async Task<IActionResult> PostTLP([FromForm] IFormFile? req)
    {
        ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query, GetODataUrl());
        string outputFilename = GetOutputFilename(requestIDs, "tlp");
        string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

        NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
        NeuronTLPView TlpGraph = NeuronTLPView.ToTLP(neuronGraph, GetVolumeUrl());
        TlpGraph.SaveTLP(outputFileFullPath);
        return RedirectToFile(outputFilename);
    }

    /// <summary>
    /// Exports network data in GraphML format via POST request.
    /// </summary>
    /// <param name="req">The form file (not used, but required for routing).</param>
    /// <returns>Redirect to the generated GraphML file.</returns>
    [HttpPost("PostGML")]
    public async Task<IActionResult> PostGML([FromForm] IFormFile? req)
    {
        ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query, GetODataUrl());
        string outputFilename = GetOutputFilename(requestIDs, "graphml");
        string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

        NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
        NeuronGMLView GmlGraph = NeuronGMLView.ToGML(neuronGraph, GetVolumeUrl());
        GmlGraph.SaveGML(outputFileFullPath);
        return RedirectToFile(outputFilename);
    }

    /// <summary>
    /// Exports network data in JSON format via POST request.
    /// </summary>
    /// <param name="req">The form file (not used, but required for routing).</param>
    /// <returns>Redirect to the generated JSON file.</returns>
    [HttpPost("PostJSON")]
    public async Task<IActionResult> PostJSON([FromForm] IFormFile? req)
    {
        ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query, GetODataUrl());
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
    [HttpGet("dot")]
    [HttpGet("GetDot")]
    [HttpGet("GetDot/dot")]
    public async Task<IActionResult> GetDot()
    {
        ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query, GetODataUrl());
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
    [HttpGet("tlp")]
    [HttpGet("GetTLP")]
    [HttpGet("GetTLP/tlp")]
    public async Task<IActionResult> GetTLP()
    {
        ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query, GetODataUrl());
        string outputFilename = GetOutputFilename(requestIDs, "tlp");
        string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

        NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
        // OData spatial data append here if needed
        NeuronTLPView TlpGraph = NeuronTLPView.ToTLP(neuronGraph, GetVolumeUrl());
        TlpGraph.SaveTLP(outputFileFullPath);
        return PhysicalFile(outputFileFullPath, "text/plain", outputFilename);
    }

    /// <summary>
    /// Exports network data in GraphML format via GET request.
    /// </summary>
    /// <returns>The generated GraphML file for download.</returns>
    [HttpGet("gml")]
    [HttpGet("GetGML")]
    [HttpGet("GetGML/gml")]
    public async Task<IActionResult> GetGML()
    {
        ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query, GetODataUrl());
        string outputFilename = GetOutputFilename(requestIDs, "graphml");
        string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

        NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
        NeuronGMLView GmlGraph = NeuronGMLView.ToGML(neuronGraph, GetVolumeUrl());
        GmlGraph.SaveGML(outputFileFullPath);
        return PhysicalFile(outputFileFullPath, "text/plain", outputFilename);
    }

    /// <summary>
    /// Exports network data in JSON format via GET request.
    /// </summary>
    /// <returns>The generated JSON file for download.</returns>
    [HttpGet("json")]
    [HttpGet("GetJSON")]
    [HttpGet("GetJSON/json")]
    public async Task<IActionResult> GetJSON()
    {
        ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query, GetODataUrl());
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
        // Use async OData client logic to retrieve the graph
        return await ODataNeuronFactory.FromODataAsync(
            requestIDs,
            GetNumHops(),
            GetODataUrl());
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
