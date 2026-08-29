using AnnotationVizLib;
using AnnotationVizLib.OData;
using ODataClient.ConnectomeDataModel;

namespace DataExport.Controllers;

/// <summary>
/// Controller for exporting morphology data in various formats (TLP, JSON, DAE).
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MorphologyController"/> class.
/// </remarks>
/// <param name="env">The web host environment.</param>
/// <param name="configuration">The configuration service.</param>
/*
 * The route prefix deliberately omits [action]. With it, the controller prefix already
 * resolved to "Morphology/GetTLP" and the action template "tlp" appended to it, so the
 * only reachable URL was Morphology/GetTLP/tlp with the format named twice. Actions now
 * carry explicit templates: the short form, and the longer form kept for compatibility.
 */
[ApiController]
[Route("[controller]")]
public class MorphologyController(IWebHostEnvironment env, IConfiguration configuration) : Controller
{
    private readonly IWebHostEnvironment _env = env ?? throw new ArgumentNullException(nameof(env));
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private UnitsAndScale.Scale? _cachedScale;
    private readonly SemaphoreSlim _scaleLock = new(1, 1);

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

    private async Task<UnitsAndScale.Scale> GetOrFetchScaleAsync()
    {
        if (_cachedScale != null)
        {
            return _cachedScale;
        }

        await _scaleLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_cachedScale != null)
            {
                return _cachedScale;
            }

            Container container = new(GetODataUrl())
            {
                MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
            };

            var scale = await Task.Run(() => container.Scale().GetValue());
            _cachedScale = scale.ToGeometryScale();
            return _cachedScale;
        }
        finally
        {
            _scaleLock.Release();
        }
    }

    private static string GetOutputFilename(ICollection<long> requestIDs, string ext)
    {
        string idList = OutputNameGenerator.GetFileFriendlyIDList(requestIDs);
        string date = OutputNameGenerator.GetFileFriendlyDateString();
        return $"morph-{idList} {date}.{ext}";
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
    /// Exports morphology data in TLP (Tulip) format via POST request.
    /// </summary>
    /// <returns>Redirect to the generated TLP file.</returns>
    [HttpPost("PostTLP")]
    public async Task<IActionResult> PostTLP()
    {
        ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query, GetODataUrl());
        string outputFile = GetOutputFilename(requestIDs, "tlp");
        string userOutputDirectory = GetAndCreateOutputDirectory();
        string userOutputFileFullPath = Path.Combine(userOutputDirectory, outputFile);

        StructureMorphologyColorMap colorMap = new(GetStructureTypeColorMap(),
                                                                               GetStructureColorMap(),
                                                                               GetColorMapImage());

        UnitsAndScale.Scale scale = await GetOrFetchScaleAsync();
        MorphologyGraph structure_graph = await GetGraphAsync(requestIDs, scale);
        if (RequestedStickFigure())
        {
            structure_graph.ToStickFigure();
        }
        MorphologyTLPView TlpGraph = MorphologyTLPView.ToTLP(structure_graph, (UnitsAndScale.Scale)structure_graph.scale, colorMap, GetVolumeUrl());
        TlpGraph.SaveTLP(userOutputFileFullPath);

        return RedirectToFile(outputFile);
    }

    /// <summary>
    /// Exports morphology data in JSON format via POST request.
    /// </summary>
    /// <returns>Redirect to the generated JSON file.</returns>
    [HttpPost("PostJSON")]
    public async Task<IActionResult> PostJSON()
    {
        ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query, GetODataUrl());
        string outputFile = GetOutputFilename(requestIDs, "json");
        string userOutputDirectory = GetAndCreateOutputDirectory();
        string userOutputFileFullPath = Path.Combine(userOutputDirectory, outputFile);

        UnitsAndScale.Scale scale = await GetOrFetchScaleAsync();
        MorphologyGraph structure_graph = await GetGraphAsync(requestIDs, scale);
        if (RequestedStickFigure())
        {
            structure_graph.ToStickFigure();
        }
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

    /// <summary>
    /// Exports morphology data in TLP (Tulip) format via GET request.
    /// </summary>
    /// <returns>The generated TLP file for download.</returns>
    [HttpGet("tlp")]
    [HttpGet("GetTLP")]
    [HttpGet("GetTLP/tlp")]
    public async Task<IActionResult> GetTLP()
    {
        ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query, GetODataUrl());
        string outputFile = GetOutputFilename(requestIDs, "tlp");
        string userOutputDirectory = GetAndCreateOutputDirectory();
        string userOutputFileFullPath = Path.Combine(userOutputDirectory, outputFile);

        StructureMorphologyColorMap colorMap = new(GetStructureTypeColorMap(),
                                                                               GetStructureColorMap(),
                                                                               GetColorMapImage());

        UnitsAndScale.Scale scale = await GetOrFetchScaleAsync();
        MorphologyGraph structure_graph = await GetGraphAsync(requestIDs, scale);
        if (RequestedStickFigure())
        {
            structure_graph.ToStickFigure();
        }
        MorphologyTLPView TlpGraph = MorphologyTLPView.ToTLP(structure_graph, (UnitsAndScale.Scale)structure_graph.scale, colorMap, GetVolumeUrl());
        TlpGraph.SaveTLP(userOutputFileFullPath);

        return PhysicalFile(userOutputFileFullPath, "text/plain", outputFile);
    }

    /// <summary>
    /// Exports morphology data in JSON format via GET request.
    /// </summary>
    /// <returns>The generated JSON file for download.</returns>
    [HttpGet("json")]
    [HttpGet("GetJSON")]
    [HttpGet("GetJSON/json")]
    public async Task<IActionResult> GetJSON()
    {
        ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query, GetODataUrl());
        string outputFile = GetOutputFilename(requestIDs, "json");
        string userOutputDirectory = GetAndCreateOutputDirectory();
        string userOutputFileFullPath = Path.Combine(userOutputDirectory, outputFile);

        UnitsAndScale.Scale scale = await GetOrFetchScaleAsync();
        MorphologyGraph structure_graph = await GetGraphAsync(requestIDs, scale);
        if (RequestedStickFigure())
        {
            structure_graph.ToStickFigure();
        }

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
        string? path = _configuration["AppSettings:DefaultStructureTypeColorsPath"];
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException("AppSettings:DefaultStructureTypeColorsPath not configured");
        }
        string fullPath = Path.Combine(_env.ContentRootPath, path);
        return ColorMapWithLong.CreateFromConfigFile(fullPath);
    }

    private ColorMapWithLong GetStructureColorMap()
    {
        string? path = _configuration["AppSettings:DefaultStructureColorsPath"];
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException("AppSettings:DefaultStructureColorsPath not configured");
        }
        string fullPath = Path.Combine(_env.ContentRootPath, path);
        return ColorMapWithLong.CreateFromConfigFile(fullPath);
    }

    private ColorMapWithImages GetColorMapImage()
    {
        string? path = _configuration["AppSettings:DefaultLocationColorMapsPath"];
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException("AppSettings:DefaultLocationColorMapsPath not configured");
        }
        string fullPath = Path.Combine(_env.ContentRootPath, path);
        return ColorMapWithImages.CreateFromConfigFile(fullPath);
    }

    private async Task<MorphologyGraph> GetGraphAsync(ICollection<long> requestIDs, UnitsAndScale.Scale scale)
    {
        return await ODataMorphologyFactory.FromODataAsync(
            requestIDs,
            include_children: false,
            GetODataUrl(),
            scale);
    }

    private bool RequestedStickFigure()
    {
        return (Request.Query.ContainsKey("stick") &&
                uint.TryParse(Request.Query["stick"], out uint stick) &&
                stick > 0) ||
               (Request.Query.ContainsKey("Stick") &&
                uint.TryParse(Request.Query["Stick"], out uint stickUpper) &&
                stickUpper > 0);
    }
}
