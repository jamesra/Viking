using AnnotationVizLib;
using AnnotationVizLib.OData;
using VikingWebAppSettings;

namespace DataExport.Controllers;

/// <summary>
/// Controller for exporting motif graph data in various formats (DOT, TLP, JSON).
/// </summary>
[ApiController]
[Route("[controller]/[action]")]
public class MotifController : Controller
{
    private readonly IWebHostEnvironment _env;
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
    public MotifController(IWebHostEnvironment env)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
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

    /// <summary>
    /// Exports motif graph data in TLP (Tulip) format.
    /// </summary>
    /// <returns>The generated TLP file for download.</returns>
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

    /// <summary>
    /// Exports motif graph data in JSON format.
    /// </summary>
    /// <returns>The generated JSON file for download.</returns>
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
        await Task.CompletedTask;
        throw new NotImplementedException("ODataClient motif graph retrieval not yet implemented.");
    }
}
