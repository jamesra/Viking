using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MorphologyMeshTest.DifficultCases
{
    /// <summary>
    /// A 3D camera worth capturing for a difficult case.  Mirrors the fields of the testbed's
    /// <c>CaptureCameraRequest</c> so the skill script can copy it straight into a capture request.
    /// </summary>
    public sealed class DifficultCaseCamera
    {
        public string Name { get; set; }

        /// <summary>top, oblique, oblique-back, side, front, below.</summary>
        public string Preset { get; set; }

        public double? Azimuth { get; set; }

        public double? Elevation { get; set; }

        public double? Distance { get; set; }

        public float[] LookAt { get; set; }

        public float[] Position { get; set; }

        /// <summary>The PNG suffix the testbed will use for this camera.</summary>
        public string SlugName => Name ?? Preset ?? $"az{Azimuth ?? 0:0}-el{Elevation ?? 0:0}";
    }

    /// <summary>An extra 2D zoom to capture for a difficult case.</summary>
    public sealed class DifficultCaseShot2D
    {
        public string Stage { get; set; } = "Final mesh";

        public float[] LookAt { get; set; }

        public double? Downsample { get; set; }
    }

    /// <summary>
    /// One entry of <c>difficult-cases.json</c>: a slice, by volume and LocationIDs, that the generator once meshed
    /// wrongly, with what was wrong, what fixed it, and the camera placements that show the defect.
    /// </summary>
    public sealed class DifficultCase
    {
        public string Volume { get; set; }

        public ulong[] Locations { get; set; }

        /// <summary>What the mesh looked like and which stage caused it.</summary>
        public string Problem { get; set; }

        /// <summary>The code change that made the slice right.</summary>
        public string Fix { get; set; }

        /// <summary>
        /// When true the slice is tracked from a BajajMultiTest failure list but is not yet required to mesh
        /// cleanly.  Regression tests and Compare-DifficultCases skip open cases until a fix lands and
        /// <c>open</c> is cleared (with baselines accepted).
        /// </summary>
        public bool Open { get; set; }

        /// <summary>
        /// Optional <c>SliceFailureKind</c> name from the failed-slices header (Topology, InvalidSurface,
        /// UntiledLinkedPair, …).  Informational for open imports; ignored by regression tests.
        /// </summary>
        public string FailureKind { get; set; }

        public List<DifficultCaseCamera> Cameras { get; set; } = [];

        public List<DifficultCaseShot2D> Shots2D { get; set; } = [];

        [JsonIgnore]
        public string Description => string.IsNullOrWhiteSpace(Fix) ? Problem : $"{Problem} Fix: {Fix}";

        [JsonIgnore]
        public Uri Endpoint => Enum.TryParse(Volume, ignoreCase: true, out Viking.Common.Endpoint known)
            ? Viking.Common.ODataEndpointCatalog.EndpointMap[known]
            : new Uri(Volume, UriKind.Absolute);

        /// <summary>Folder name shared with the baseline images: <c>RPC1-368195-368197</c>.</summary>
        [JsonIgnore]
        public string Key => $"{Volume.ToUpperInvariant()}-{string.Join("-", Locations)}";

        public override string ToString() => $"{Volume} {string.Join(",", Locations)}";
    }

    public sealed class DifficultCaseFile
    {
        [JsonPropertyName("$comment")]
        public string[] Comment { get; set; }

        public List<DifficultCase> Cases { get; set; } = [];
    }

    public static class DifficultCaseList
    {
        public const string FileName = "difficult-cases.json";

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "DifficultCases", FileName);

        public static IReadOnlyList<DifficultCase> Load(string path = null)
        {
            path ??= DefaultPath;
            if (!File.Exists(path))
                throw new FileNotFoundException($"Difficult case list not found: {path}");

            DifficultCaseFile file = JsonSerializer.Deserialize<DifficultCaseFile>(File.ReadAllText(path), JsonOptions)
                ?? throw new FormatException($"Difficult case list is empty: {path}");

            foreach (DifficultCase c in file.Cases)
            {
                if (string.IsNullOrWhiteSpace(c.Volume))
                    throw new FormatException($"Difficult case without a volume: {JsonSerializer.Serialize(c, JsonOptions)}");
                if (c.Locations is null || c.Locations.Length == 0)
                    throw new FormatException($"Difficult case {c.Volume} has no location IDs");
            }

            return file.Cases;
        }
    }
}
