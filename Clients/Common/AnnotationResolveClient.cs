using System;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.Common
{
    /// <summary>
    /// Opt-in settings for the AnnotationTest-only <c>/test/resolve/location</c> HTTP endpoint.
    /// Production stays off unless <see cref="ForceEnabled"/> or the env vars below are set.
    /// </summary>
    public static class AnnotationResolveOptions
    {
        /// <summary>Default LAN base for the AnnotationTest resolve service.</summary>
        public const string DefaultBaseUrl = "http://localhost:5012";

        /// <summary>When <c>1</c>/<c>true</c>/<c>yes</c>, enables resolve UI with <see cref="DefaultBaseUrl"/>.</summary>
        public const string EnvEnabled = "VIKING_ANNOTATION_RESOLVE";

        /// <summary>Optional override of the resolve service base URL; presence alone enables the feature.</summary>
        public const string EnvBaseUrl = "VIKING_ANNOTATION_RESOLVE_BASE";

        /// <summary>
        /// Set by Viking Test startup so the menu appears without an env var.
        /// Production Viking must leave this false.
        /// </summary>
        public static bool ForceEnabled { get; set; }

        /// <summary>True when resolve navigation should be offered in the client UI.</summary>
        public static bool IsEnabled =>
            ForceEnabled
            || IsTruthy(Environment.GetEnvironmentVariable(EnvEnabled))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvBaseUrl));

        /// <summary>Resolve service base URL (no trailing slash). Defaults to <see cref="DefaultBaseUrl"/>.</summary>
        public static string BaseUrl
        {
            get
            {
                string fromEnv = Environment.GetEnvironmentVariable(EnvBaseUrl);
                if (!string.IsNullOrWhiteSpace(fromEnv))
                    return fromEnv.Trim().TrimEnd('/');
                return DefaultBaseUrl;
            }
        }

        static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            value = value.Trim();
            return value == "1"
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Point returned by the AnnotationTest resolve endpoint.
    /// </summary>
    public readonly struct AnnotationResolvePoint
    {
        public AnnotationResolvePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
    }

    /// <summary>
    /// Axis-aligned bounds returned by the AnnotationTest resolve endpoint.
    /// </summary>
    public readonly struct AnnotationResolveBBox
    {
        public AnnotationResolveBBox(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }

        /// <summary>Width of the box; zero when the server returns a degenerate bbox.</summary>
        public double Width => MaxX - MinX;
    }

    /// <summary>
    /// Parsed <c>GET /test/resolve/location/{id}</c> payload used to fly the camera without a store round-trip.
    /// </summary>
    public sealed class AnnotationResolveResult
    {
        public long LocationId { get; set; }
        public long StructureId { get; set; }
        public string Volume { get; set; }
        public int Section { get; set; }
        public AnnotationResolvePoint Center { get; set; }
        public AnnotationResolveBBox BBox { get; set; }
    }

    /// <summary>
    /// Anonymous HTTP client for the AnnotationTest resolve endpoint.
    /// Intended for Viking Test / LAN prototypes; not for production Identity-backed volumes.
    /// </summary>
    public static class AnnotationResolveClient
    {
        static readonly HttpClient SharedClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        static readonly Regex AnnotationRefRegex = new(
            @"^\s*(?:@annotation[:\s]+)?(?<id>\d+)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Parses <c>@annotation 1</c>, <c>@annotation:1</c>, or a bare location id.
        /// Used by command-palette style entry points that accept a pasted token.
        /// </summary>
        public static bool TryParseAnnotationRef(string text, out long locationId)
        {
            locationId = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            Match match = AnnotationRefRegex.Match(text);
            if (!match.Success)
                return false;

            return long.TryParse(match.Groups["id"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out locationId)
                && locationId > 0;
        }

        /// <summary>
        /// GETs <c>{base}/test/resolve/location/{id}</c> and parses the JSON body.
        /// Called by Viking Test menu / shared navigation prototypes.
        /// </summary>
        public static async Task<AnnotationResolveResult> ResolveLocationAsync(
            long locationId,
            string baseUrl = null,
            CancellationToken cancellationToken = default)
        {
            if (locationId <= 0)
                throw new ArgumentOutOfRangeException(nameof(locationId));

            string root = string.IsNullOrWhiteSpace(baseUrl)
                ? AnnotationResolveOptions.BaseUrl
                : baseUrl.Trim().TrimEnd('/');

            string url = $"{root}/test/resolve/location/{locationId.ToString(CultureInfo.InvariantCulture)}";
            using HttpResponseMessage response = await SharedClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Resolve location {locationId} failed ({(int)response.StatusCode} {response.ReasonPhrase}): {Truncate(body, 200)}");
            }

            return ParseResolveJson(body);
        }

        /// <summary>
        /// Minimal JSON parse for the fixed resolve payload shape (avoids a JSON package on Common).
        /// </summary>
        internal static AnnotationResolveResult ParseResolveJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("Resolve response was empty.");

            long locationId = RequireLong(json, "locationId");
            long structureId = RequireLong(json, "structureId");
            int section = (int)RequireLong(json, "section");
            string volume = TryString(json, "volume");

            if (!TryObject(json, "center", out string centerJson)
                || !TryDouble(centerJson, "x", out double cx)
                || !TryDouble(centerJson, "y", out double cy))
            {
                throw new FormatException("Resolve response missing center.x / center.y.");
            }

            AnnotationResolveBBox bbox = default;
            if (TryObject(json, "bbox", out string bboxJson)
                && TryDouble(bboxJson, "minX", out double minX)
                && TryDouble(bboxJson, "minY", out double minY)
                && TryDouble(bboxJson, "maxX", out double maxX)
                && TryDouble(bboxJson, "maxY", out double maxY))
            {
                bbox = new AnnotationResolveBBox(minX, minY, maxX, maxY);
            }

            return new AnnotationResolveResult
            {
                LocationId = locationId,
                StructureId = structureId,
                Volume = volume,
                Section = section,
                Center = new AnnotationResolvePoint(cx, cy),
                BBox = bbox
            };
        }

        static long RequireLong(string json, string name)
        {
            if (!TryLong(json, name, out long value))
                throw new FormatException($"Resolve response missing '{name}'.");
            return value;
        }

        static bool TryLong(string json, string name, out long value)
        {
            value = 0;
            Match match = Regex.Match(
                json,
                $@"""{Regex.Escape(name)}""\s*:\s*(?<v>-?\d+)",
                RegexOptions.CultureInvariant);
            return match.Success
                && long.TryParse(match.Groups["v"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        static bool TryDouble(string json, string name, out double value)
        {
            value = 0;
            Match match = Regex.Match(
                json,
                $@"""{Regex.Escape(name)}""\s*:\s*(?<v>-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)",
                RegexOptions.CultureInvariant);
            return match.Success
                && double.TryParse(match.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        static string TryString(string json, string name)
        {
            Match match = Regex.Match(
                json,
                $@"""{Regex.Escape(name)}""\s*:\s*""(?<v>[^""]*)""",
                RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["v"].Value : null;
        }

        static bool TryObject(string json, string name, out string objectJson)
        {
            objectJson = string.Empty;
            Match match = Regex.Match(
                json,
                $@"""{Regex.Escape(name)}""\s*:\s*\{{(?<body>[^}}]*)\}}",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                return false;
            objectJson = "{" + match.Groups["body"].Value + "}";
            return true;
        }

        static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
                return text ?? string.Empty;
            return text.Substring(0, max) + "…";
        }
    }
}
