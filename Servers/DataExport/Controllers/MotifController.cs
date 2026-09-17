using AnnotationVizLib;
using AnnotationVizLib.OData;

namespace DataExport.Controllers;

/// <summary>
/// Controller for exporting motif graph data in various formats (DOT, TLP, JSON).
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MotifController"/> class.
/// </remarks>
/// <param name="env">The web host environment.</param>
/// <param name="configuration">The configuration service.</param>
/*
 * The route prefix deliberately omits [action]. With it, the controller prefix already
 * resolved to "Motif/GetTLP" and the action template "tlp" appended to it, so the
 * only reachable URL was Motif/GetTLP/tlp with the format named twice. Actions now
 * carry explicit templates: the short form, and the longer form kept for compatibility.
 */
[ApiController]
[Route("[controller]")]
public class MotifController(IWebHostEnvironment env, IConfiguration configuration) : Controller
{
    private readonly IWebHostEnvironment _env = env ?? throw new ArgumentNullException(nameof(env));
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private const string DefaultOutputFile = "motifs";
    private static long _nextId;

    /// <summary>
    /// Gets the next unique filename ID for motif exports.
    /// </summary>
    private static long NextFilenameID => Interlocked.Increment(ref _nextId);

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

    private static string GetOutputFilename(string ext) => $"{DefaultOutputFile}{NextFilenameID}{OutputNameGenerator.GetFileFriendlyDateString()}.{ext}";

    private string GetAndCreateOutputDirectory()
    {
        string outputDir = Path.Combine(_env.ContentRootPath, "Output");
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }
        return outputDir;
    }

    /// <summary>
    /// Exports motif graph data in DOT format.
    /// </summary>
    /// <returns>The generated DOT file for download.</returns>
    [HttpGet("dot")]
    [HttpGet("GetDot")]
    [HttpGet("GetDot/dot")]
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

    /// <summary>
    /// Exports motif graph data in TLP (Tulip) format.
    /// </summary>
    /// <returns>The generated TLP file for download.</returns>
    [HttpGet("tlp")]
    [HttpGet("GetTLP")]
    [HttpGet("GetTLP/tlp")]
    public async Task<IActionResult> GetTLP()
    {
        string userDotDirectory = GetAndCreateOutputDirectory();
        string outputFilename = GetOutputFilename("tlp");
        string userDotFileFullPath = Path.Combine(userDotDirectory, outputFilename);

        MotifGraph motifGraph = await GetMotifGraphAsync();
        motifGraph.AddEdgeStatistics();
        MotifTLPView TlpGraph = MotifTLPView.ToTLP(motifGraph, GetVolumeUrl());
        TlpGraph.SaveTLP(userDotFileFullPath);

        return PhysicalFile(userDotFileFullPath, "text/plain", outputFilename);
    }

    /// <summary>
    /// Exports motif graph data in JSON format.
    /// </summary>
    /// <returns>The generated JSON file for download.</returns>
    [HttpGet("json")]
    [HttpGet("GetJSON")]
    [HttpGet("GetJSON/json")]
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

    private async Task<MotifGraph> GetMotifGraphAsync() => await ODataMotifFactory.FromODataAsync(GetODataUrl());
}
