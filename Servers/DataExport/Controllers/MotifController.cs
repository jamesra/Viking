using AnnotationVizLib;
using AnnotationVizLib.OData;

namespace DataExport.Controllers;

/// <summary>
/// Controller for exporting motif graph data in various formats (DOT, TLP, JSON).
/// </summary>
[ApiController]
[Route("[controller]/[action]")]
public class MotifController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private const string DefaultOutputFile = "motifs";
    private static long _nextId;

    /// <summary>
    /// Gets the next unique filename ID for motif exports.
    /// </summary>
    private static long NextFilenameID => Interlocked.Increment(ref _nextId);

    /// <summary>
    /// Initializes a new instance of the <see cref="MotifController"/> class.
    /// </summary>
    /// <param name="env">The web host environment.</param>
    /// <param name="configuration">The configuration service.</param>
    public MotifController(IWebHostEnvironment env, IConfiguration configuration)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

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

    private string GetOutputFilename(string ext)
    {
        return $"{DefaultOutputFile}{NextFilenameID}{OutputNameGenerator.GetFileFriendlyDateString()}.{ext}";
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

    /// <summary>
    /// Exports motif graph data in DOT format.
    /// </summary>
    /// <returns>The generated DOT file for download.</returns>
    [HttpGet("GetDot")]
    [HttpGet("dot")]
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
    [HttpGet("GetTLP")]
    [HttpGet("tlp")]
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
    [HttpGet("GetJSON")]
    [HttpGet("json")]
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
        return await ODataMotifFactory.FromODataAsync(GetODataUrl());
    }
}
