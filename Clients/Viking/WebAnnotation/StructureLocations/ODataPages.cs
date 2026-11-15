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
using Viking.Common;
using WebAnnotation.ReviewFeed;

namespace WebAnnotation.StructureLocations
{
    /// <summary>
    /// Reads paged OData lists from the annotation host's OData service: the root next to the annotation endpoint, one GET per
    /// page with the signed-in user's credentials, and the rows plus <c>@odata.nextLink</c> of each response. Used by the
    /// structure property tabs (<see cref="StructureLocationsODataQuery"/>, <see cref="StructureListODataQuery"/>) and Go to
    /// Structure (<see cref="StructureGoTo"/>), which ask for table columns and positions only, never shapes.
    /// </summary>
    public static class ODataPages
    {
        /// <summary>
        /// Rows asked for per response (the OData <c>odata.maxpagesize</c> preference), so the first rows show soon even when
        /// the server's own page is larger. A server that ignores the preference sends its own page size.
        /// </summary>
        public const int PreferredPageSize = 1000;

        /// <summary>Seconds one page may take before the load reports a failure.</summary>
        public const int PageTimeoutSeconds = 120;

        /// <summary>
        /// The OData root of the annotation server Viking is connected to; false when its endpoint has no
        /// <c>Annotation</c> segment to replace (<see cref="ReviewChangeODataQuery.TryODataRoot"/>).
        /// </summary>
        public static bool TryRoot(out Uri root) =>
            ReviewChangeODataQuery.TryODataRoot(WebAnnotationModel.State.Endpoint?.AbsoluteUri ?? "", out root);

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

        /// <summary>
        /// Reads one response: each object in <c>value</c> through <paramref name="parseRow"/> (a row it rejects is skipped)
        /// and the link to the next page, null on the last.
        /// </summary>
        public static List<T> ParsePage<T>(string json, Func<JToken, T?> parseRow, out Uri? nextLink) where T : class
        {
            nextLink = null;
            List<T> rows = [];
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
                if (value is JObject && parseRow(value) is T row)
                    rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        /// Downloads every page from <paramref name="firstPage"/> on, off the UI thread, and returns all rows. For callers that
        /// need the whole list before they can act (Go to Structure); a table shows each page as it comes instead.
        /// </summary>
        public static async Task<List<T>> LoadAllAsync<T>(Uri firstPage, Func<JToken, T?> parseRow, CancellationToken cancel)
            where T : class
        {
            List<T> all = [];
            Uri? page = firstPage;
            while (page is not null)
            {
                string json = await DownloadAsync(page, cancel).ConfigureAwait(false);
                all.AddRange(ParsePage(json, parseRow, out page));
            }

            return all;
        }

        /// <summary>A short reason for a failed load that a user can read.</summary>
        public static string Reason(Exception e) =>
            e is TaskCanceledException or OperationCanceledException ? "the server did not answer in time" : e.Message;

        /// <summary><c>$filter</c> text for one integer property, escaped for the query string.</summary>
        public static string EqualsFilter(string property, long value) =>
            Uri.EscapeDataString(property + " eq " + value.ToString(CultureInfo.InvariantCulture));

        public static JToken? Prop(JToken? token, string name)
        {
            if (token is not JObject obj)
                return null;
            return obj.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out JToken? value) ? value : null;
        }

        public static int CountOf(JToken? token) => token is JArray array ? array.Count : 0;

        public static bool ReadBool(JToken? token) =>
            token is not null && token.Type == JTokenType.Boolean && token.Value<bool>();

        public static string? ReadString(JToken? token) =>
            token is null || token.Type == JTokenType.Null ? null : token.Value<string>();

        public static bool TryReadInt64(JToken? token, out long value)
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

        public static bool TryReadDouble(JToken? token, out double value)
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

        /// <summary>The time as UTC, as the WCF objects show it; <see cref="DateTime.MinValue"/> when missing.</summary>
        public static DateTime ReadUtc(JToken? token)
        {
            string? text = ReadString(token);
            if (string.IsNullOrWhiteSpace(text)
                || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset parsed))
            {
                return DateTime.MinValue;
            }

            return parsed.UtcDateTime;
        }

        /// <summary>An object's attributes from its <c>Tags</c>; none when they do not parse, so one bad row does not stop a list.</summary>
        public static List<WebAnnotationModel.ObjAttribute> ParseTags(string? tags)
        {
            try
            {
                return WebAnnotationModel.ObjAttribute.Parse(tags);
            }
            catch (Exception)
            {
                return [];
            }
        }
    }
}
