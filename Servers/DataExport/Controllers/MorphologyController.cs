using System.Collections.Concurrent;
using AnnotationVizLib;
using AnnotationVizLib.OData;
using Grpc.Core;
using Grpc.Net.Client;
using ODataClient.ConnectomeDataModel;
using Viking.SectionCorrectionClient;
using Vector2 = Geometry.Vector2;

namespace DataExport.Controllers;

/// <summary>
/// Controller for exporting morphology data in various formats (TLP, JSON, DAE).
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MorphologyController"/> class.
/// </remarks>
/// <param name="env">The web host environment.</param>
/// <param name="configuration">The configuration service.</param>
/// <param name="logger">Logger for residual-field load diagnostics.</param>
/*
 * The route prefix deliberately omits [action]. With it, the controller prefix already
 * resolved to "Morphology/GetTLP" and the action template "tlp" appended to it, so the
 * only reachable URL was Morphology/GetTLP/tlp with the format named twice. Actions now
 * carry explicit templates: the short form, and the longer form kept for compatibility.
 */
[ApiController]
[Route("[controller]")]
public class MorphologyController(IWebHostEnvironment env, IConfiguration configuration, ILogger<MorphologyController> logger) : Controller
{
    private readonly IWebHostEnvironment _env = env ?? throw new ArgumentNullException(nameof(env));
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly ILogger<MorphologyController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private UnitsAndScale.Scale? _cachedScale;
    private readonly SemaphoreSlim _scaleLock = new(1, 1);
    private readonly SemaphoreSlim _residualFieldLock = new(1, 1);
    private NeighborResidualField? _cachedResidualField;
    private bool _residualFieldResolved;
    static readonly ConcurrentDictionary<Uri, GrpcChannel> CorrectionChannels = new();

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

    /// <summary>
    /// Host used for StructureURL links in exported graphs. Prefer the annotation
    /// endpoint so OData is derived by replacing Annotation with OData; fall back to
    /// the configured OData URL, then the volume service root.
    /// </summary>
    private string GetAnnotationUrlForLinks()
    {
        string annotation = _configuration["AppSettings:AnnotationURL"];
        if (!string.IsNullOrWhiteSpace(annotation))
            return annotation;

        string odata = _configuration["AppSettings:ODataURL"];
        if (!string.IsNullOrWhiteSpace(odata))
            return odata;

        return GetVolumeUrl();
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

    /// <summary>
    /// Exports morphology data in TLP (Tulip) format via POST request.
    /// </summary>
    /// <remarks>
    /// POST accepts the structure ID list in the request body, which avoids the URL length limit that
    /// constrains the equivalent GET.
    /// </remarks>
    /// <returns>The generated TLP file for download.</returns>
    [HttpPost("tlp")]
    [HttpPost("PostTLP")]
    [RequestSizeLimit(RequestBodyIds.MaxBodyBytes)]
    public async Task<IActionResult> PostTLP()
    {
        ICollection<long> requestIDs = await RequestVariables.GetIDsFromRequestAsync(Request, GetODataUrl(), HttpContext.RequestAborted);
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
        MorphologyTLPView TlpGraph = MorphologyTLPView.ToTLP(structure_graph, (UnitsAndScale.Scale)structure_graph.scale, colorMap, GetAnnotationUrlForLinks());
        TlpGraph.SaveTLP(userOutputFileFullPath);

        return PhysicalFile(userOutputFileFullPath, "text/plain", outputFile);
    }

    /// <summary>
    /// Exports morphology data in JSON format via POST request.
    /// </summary>
    /// <remarks>
    /// POST accepts the structure ID list in the request body, which avoids the URL length limit that
    /// constrains the equivalent GET.
    /// </remarks>
    /// <returns>The generated JSON file for download.</returns>
    [HttpPost("json")]
    [HttpPost("PostJSON")]
    [RequestSizeLimit(RequestBodyIds.MaxBodyBytes)]
    public async Task<IActionResult> PostJSON()
    {
        ICollection<long> requestIDs = await RequestVariables.GetIDsFromRequestAsync(Request, GetODataUrl(), HttpContext.RequestAborted);
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
        MorphologyTLPView TlpGraph = MorphologyTLPView.ToTLP(structure_graph, (UnitsAndScale.Scale)structure_graph.scale, colorMap, GetAnnotationUrlForLinks());
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
        MorphologyGraph graph = await ODataMorphologyFactory.FromODataAsync(
            requestIDs,
            include_children: false,
            GetODataUrl(),
            scale);
        await ApplyPublishedResidualFieldAsync(graph, HttpContext.RequestAborted).ConfigureAwait(false);
        return graph;
    }

    /// <summary>
    /// Applies the Neighbor residual field when one is available. Prefers
    /// <c>Corrections:ServiceUrl</c> (same maps MonogameTestbed loads via <c>--correction-url</c>),
    /// then a local <c>Corrections:RootDirectory</c>, then leaves the graph unchanged.
    /// </summary>
    private async Task ApplyPublishedResidualFieldAsync(MorphologyGraph graph, CancellationToken cancellationToken)
    {
        if (graph is null)
            return;

        NeighborResidualField? field = await GetOrLoadResidualFieldAsync(cancellationToken).ConfigureAwait(false);
        if (field is null)
            return;

        MorphologyGraph.ApplyResidualField(graph, field);
    }

    private async Task<NeighborResidualField?> GetOrLoadResidualFieldAsync(CancellationToken cancellationToken)
    {
        if (_residualFieldResolved)
            return _cachedResidualField;

        await _residualFieldLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_residualFieldResolved)
                return _cachedResidualField;

            NeighborResidualField? fromService = await TryLoadServiceResidualFieldAsync(cancellationToken)
                .ConfigureAwait(false);
            if (fromService is not null)
            {
                _cachedResidualField = fromService;
                _residualFieldResolved = true;
                return _cachedResidualField;
            }

            _cachedResidualField = TryLoadDiskResidualField();
            _residualFieldResolved = true;
            return _cachedResidualField;
        }
        finally
        {
            _residualFieldLock.Release();
        }
    }

    private async Task<NeighborResidualField?> TryLoadServiceResidualFieldAsync(CancellationToken cancellationToken)
    {
        string? serviceUrlText = _configuration["Corrections:ServiceUrl"];
        if (string.IsNullOrWhiteSpace(serviceUrlText))
        {
            _logger.LogInformation(
                "Correction service omitted (Corrections:ServiceUrl empty). " +
                "Residual field will use Corrections:RootDirectory if set.");
            return null;
        }

        if (!Uri.TryCreate(serviceUrlText.Trim(), UriKind.Absolute, out Uri? serviceUrl))
        {
            _logger.LogWarning(
                "Correction service cannot be used: invalid Corrections:ServiceUrl '{ServiceUrl}'.",
                serviceUrlText);
            return null;
        }

        string? explicitVolume = _configuration["Corrections:VolumeName"];
        string? stosGroup = _configuration["Corrections:StosGroup"];
        Uri odataUri = GetODataUrl();

        var loader = new GrpcPublishedCorrectionLoader(uri =>
            CorrectionChannels.GetOrAdd(uri, u => GrpcChannel.ForAddress(u)).CreateCallInvoker());

        _logger.LogInformation(
            "Downloading residual field from {ServiceUrl} volume={Volume} stos={StosGroup}",
            serviceUrl,
            string.IsNullOrWhiteSpace(explicitVolume) ? "(auto from ListVolumes / OData URL)" : explicitVolume,
            string.IsNullOrWhiteSpace(stosGroup) ? "(first StosGroup)" : stosGroup);

        try
        {
            DownloadedCorrectionSet set = await loader
                .LoadAsync(
                    serviceUrl,
                    string.IsNullOrWhiteSpace(explicitVolume) ? null : explicitVolume.Trim(),
                    string.IsNullOrWhiteSpace(stosGroup) ? null : stosGroup.Trim(),
                    volumeNameHint: string.IsNullOrWhiteSpace(explicitVolume) ? null : explicitVolume.Trim(),
                    annotationOrODataUri: odataUri,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!set.HasSections)
            {
                _logger.LogWarning(
                    "Correction service cannot be used: no section lattices for '{Volume}' '{StosGroup}' at {ServiceUrl}. " +
                    "Falling back to Corrections:RootDirectory.",
                    set.VolumeName,
                    set.StosGroup,
                    serviceUrl);
                return null;
            }

            _logger.LogInformation(
                "Loaded service residual field {Volume}/{StosGroup} ({SectionCount} sections, pitch={PitchNm:F0}nm) from {ServiceUrl}",
                set.VolumeName,
                set.StosGroup,
                set.Sections.Count,
                set.PitchNm,
                serviceUrl);

            IEnumerable<(int Z, int Gx, int Gy, Vector2 Offset, int Votes)> nodes = set.EnumerateLatticeNodes()
                .Select(n => (n.Z, n.Gx, n.Gy, new Vector2(n.Dx, n.Dy), n.Votes));
            return NeighborResidualField.FromLattice(nodes, set.PitchNm, set.KernelRadiusNm);
        }
        catch (Exception ex) when (ex is RpcException or InvalidOperationException or HttpRequestException
            or IOException or TimeoutException or ArgumentException)
        {
            _logger.LogWarning(
                ex,
                "Correction service cannot be used ({ServiceUrl}). Falling back to Corrections:RootDirectory.",
                serviceUrl);
            return null;
        }
    }

    private NeighborResidualField? TryLoadDiskResidualField()
    {
        string? dir = _configuration["Corrections:RootDirectory"];
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            _logger.LogInformation(
                "Residual field: no published directory (Corrections:RootDirectory empty or missing); leaving morphology uncorrected.");
            return null;
        }

        using Viking.SectionCorrection.CorrectionCatalog catalog = new(dir);
        catalog.Load();
        Viking.SectionCorrection.PublishedCorrectionSet? set;
        string? group = _configuration["Corrections:StosGroup"];
        if (!string.IsNullOrWhiteSpace(group))
        {
            if (!catalog.TryGet(group, out set) || set is null)
                throw new InvalidOperationException($"No published correction set '{group}' under {dir}.");
        }
        else
        {
            set = catalog.Sets.OrderBy(s => s.StosGroup, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if (set is null)
            {
                _logger.LogInformation("No published correction sets under {Directory}", dir);
                return null;
            }
        }

        _logger.LogInformation(
            "Residual field: published directory {StosGroup} under {Directory} (correction service not used).",
            set.StosGroup,
            dir);

        double pitch = set.Provenance?.PitchNm > 0 ? set.Provenance.PitchNm : NeighborResidualField.GridSizeNm;
        double kernel = set.Provenance?.KernelRadiusNm > 0 ? set.Provenance.KernelRadiusNm : NeighborResidualField.KernelRadiusNm;
        return NeighborResidualField.FromLattice(set.EnumerateLattice(), pitch, kernel);
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
