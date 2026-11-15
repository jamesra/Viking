using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.Common;
using WebAnnotationModel;

namespace WebAnnotation.StructureLocations
{
    /// <summary>
    /// Builds and reads the OData query behind a structure's Locations tab: one row per location of the structure with the
    /// table's columns only, never the mosaic or volume shapes, so a large cell lists quickly on a slow connection. The
    /// server returns at most its page size per response with an <c>@odata.nextLink</c> to the rest; the tab shows each page
    /// as it arrives. Geometry is fetched for one location when the user acts on its row (<see cref="StructureLocationRow"/>).
    /// </summary>
    public static class StructureLocationsODataQuery
    {
        /// <summary>
        /// Rows asked for per response (the OData <c>odata.maxpagesize</c> preference), so the first rows show soon even when
        /// the server's own page is larger. A server that ignores the preference sends its own page size.
        /// </summary>
        public const int PreferredPageSize = 1000;

        /// <summary>
        /// The columns the table shows, plus <c>Tags</c> (the attributes that mark a varicosity cap or untraceable end) for
        /// the IsUnverifiedTerminal column. No shape columns.
        /// </summary>
        public const string SelectedColumns =
            "ID,ParentID,X,Y,Z,VolumeX,VolumeY,Radius,Width,TypeCode,Terminal,OffEdge,Tags,Username,LastModified";

        /// <summary>
        /// The link ends only, to count each location's links for the IsUnverifiedTerminal column.
        /// </summary>
        public const string ExpandedLinks = "LocationLinksA($select=A),LocationLinksB($select=B)";

        /// <summary>
        /// <c>Locations</c> of <paramref name="structureID"/> ordered by section then ID, with <see cref="SelectedColumns"/>
        /// and <see cref="ExpandedLinks"/>.
        /// </summary>
        public static Uri BuildUri(Uri odataRoot, long structureID)
        {
            if (odataRoot is null)
                throw new ArgumentNullException(nameof(odataRoot));

            string filter = Uri.EscapeDataString("ParentID eq " + structureID.ToString(CultureInfo.InvariantCulture));
            string root = odataRoot.AbsoluteUri.TrimEnd('/');
            return new Uri(root + "/Locations?$filter=" + filter
                + "&$select=" + SelectedColumns
                + "&$expand=" + ExpandedLinks
                + "&$orderby=Z,ID");
        }

        /// <summary>
        /// Reads one response: its rows (a row without an ID is skipped) and the link to the next page, null on the last.
        /// </summary>
        public static List<StructureLocationRow> ParsePage(string json, out Uri? nextLink)
        {
            nextLink = null;
            List<StructureLocationRow> rows = [];
            if (string.IsNullOrWhiteSpace(json))
                return rows;

            JToken root;
            using (JsonTextReader reader = new(new StringReader(json)))
            {
                reader.DateParseHandling = DateParseHandling.None;
                root = JToken.Load(reader);
            }

            string? next = Prop(root, "@odata.nextLink")?.Value<string>();
            if (!string.IsNullOrWhiteSpace(next) && Uri.TryCreate(next, UriKind.Absolute, out Uri? nextUri))
                nextLink = nextUri;

            if (Prop(root, "value") is not JArray values)
                return rows;

            foreach (JToken value in values)
            {
                if (TryParseRow(value, out StructureLocationRow row))
                    rows.Add(row);
            }

            return rows;
        }

        private static bool TryParseRow(JToken value, out StructureLocationRow row)
        {
            row = null!;
            if (value is not JObject || !TryReadInt64(Prop(value, "ID"), out long id) || id <= 0)
                return false;

            TryReadInt64(Prop(value, "ParentID"), out long parentID);
            TryReadDouble(Prop(value, "X"), out double x);
            TryReadDouble(Prop(value, "Y"), out double y);
            TryReadDouble(Prop(value, "Z"), out double z);
            TryReadDouble(Prop(value, "VolumeX"), out double volumeX);
            TryReadDouble(Prop(value, "VolumeY"), out double volumeY);
            TryReadDouble(Prop(value, "Radius"), out double radius);
            TryReadDouble(Prop(value, "Width"), out double width);
            TryReadInt64(Prop(value, "TypeCode"), out long typeCode);

            List<ObjAttribute> attributes = ParseTags(Prop(value, "Tags")?.Value<string>());
            bool varicosityCap = attributes.Exists(a => a.Name == "Varicosity Cap");
            bool untraceable = attributes.Exists(a => a.Name == "Untraceable");

            int links = CountOf(Prop(value, "LocationLinksA")) + CountOf(Prop(value, "LocationLinksB"));

            row = new StructureLocationRow(
                id,
                parentID,
                x,
                y,
                z,
                volumeX,
                volumeY,
                width,
                radius,
                (LocationType)typeCode,
                ReadBool(Prop(value, "Terminal")),
                ReadBool(Prop(value, "OffEdge")),
                varicosityCap,
                untraceable,
                links,
                Prop(value, "Username")?.Value<string>() ?? "",
                ReadUtc(Prop(value, "LastModified")));
            return true;
        }

        /// <summary>Seconds one page may take before the load reports a failure.</summary>
        public const int PageTimeoutSeconds = 120;

        /// <summary>
        /// GETs one page with the signed-in user's credentials and bearer token, as the Review feed's OData query does, asking
        /// for at most <see cref="PreferredPageSize"/> rows. Throws on a failed request. Call it off the UI thread.
        /// </summary>
        public static async Task<string> DownloadAsync(Uri query, CancellationToken cancel)
        {
            using HttpClient client = HttpClientFactory.CreateClient(query, Viking.UI.State.UserCredentials);
            client.Timeout = TimeSpan.FromSeconds(PageTimeoutSeconds);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.Add("Prefer", "odata.maxpagesize=" + PreferredPageSize.ToString(CultureInfo.InvariantCulture));

            string? token = Viking.UI.State.UserBearerToken?.AccessToken;
            if (!string.IsNullOrWhiteSpace(token))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await client.GetAsync(query, cancel).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase} from {query}");

            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        /// <summary>The location's attributes; none when its tags do not parse, so one bad row does not stop the list.</summary>
        private static List<ObjAttribute> ParseTags(string? tags)
        {
            try
            {
                return ObjAttribute.Parse(tags);
            }
            catch (Exception)
            {
                return [];
            }
        }

        private static int CountOf(JToken? token) => token is JArray array ? array.Count : 0;

        private static JToken? Prop(JToken? token, string name)
        {
            if (token is not JObject obj)
                return null;
            return obj.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out JToken? value) ? value : null;
        }

        private static bool ReadBool(JToken? token) =>
            token is not null && token.Type == JTokenType.Boolean && token.Value<bool>();

        private static bool TryReadInt64(JToken? token, out long value)
        {
            value = 0;
            if (token is null || token.Type == JTokenType.Null)
                return false;
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                value = token.Value<long>();
                return true;
            }

            return long.TryParse(token.Value<string>(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryReadDouble(JToken? token, out double value)
        {
            value = 0;
            if (token is null || token.Type == JTokenType.Null)
                return false;
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                value = token.Value<double>();
                return true;
            }

            return double.TryParse(token.Value<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>The time as UTC, as the WCF location shows it; <see cref="DateTime.MinValue"/> when missing.</summary>
        private static DateTime ReadUtc(JToken? token)
        {
            string? text = token is null || token.Type == JTokenType.Null ? null : token.Value<string>();
            if (string.IsNullOrWhiteSpace(text)
                || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset parsed))
            {
                return DateTime.MinValue;
            }

            return parsed.UtcDateTime;
        }
    }
}
