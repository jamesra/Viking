using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Geometry;
using Microsoft.SqlServer.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlGeometryUtils;

namespace WebAnnotation.ReviewFeed
{
    /// <summary>
    /// Builds and reads the OData "newest locations" query used to seed the Review change feed.
    /// WCF exposes <c>GetLocationChanges</c> per section and <c>GetLastModifiedLocation</c> for the
    /// caller only, so a volume-wide newest-N list has to come from OData
    /// <c>Locations?$orderby=LastModified desc&amp;$top=N</c>.
    /// Called by <see cref="ReviewChangeFeedBackfill"/>.
    /// </summary>
    public static class ReviewChangeODataQuery
    {
        /// <summary>
        /// Builds the OData service root from the annotation WCF host URL by replacing the
        /// <c>Annotation</c> path segment with <c>OData</c> (e.g.
        /// <c>…/RC1/Annotation/Service.svc</c> → <c>…/RC1/OData/</c>).
        /// An URL that already ends in <c>OData</c> is returned unchanged (query and fragment dropped).
        /// </summary>
        public static bool TryODataRoot(string annotationUrl, out Uri odataRoot)
        {
            odataRoot = null;
            if (string.IsNullOrWhiteSpace(annotationUrl))
                return false;
            if (!Uri.TryCreate(annotationUrl.Trim(), UriKind.Absolute, out Uri annotation))
                return false;
            if (annotation.Scheme != Uri.UriSchemeHttp && annotation.Scheme != Uri.UriSchemeHttps)
                return false;

            string path = annotation.AbsolutePath;
            if (string.IsNullOrEmpty(path))
                return false;

            string[] segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return false;

            if (segments[segments.Length - 1].Equals("OData", StringComparison.OrdinalIgnoreCase))
            {
                odataRoot = WithPath(annotation, "/" + string.Join("/", segments) + "/");
                return true;
            }

            int annotationIndex = -1;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Equals("Annotation", StringComparison.OrdinalIgnoreCase))
                    annotationIndex = i;
            }

            if (annotationIndex < 0)
                return false;

            var rebuilt = new StringBuilder();
            for (int i = 0; i < annotationIndex; i++)
            {
                rebuilt.Append('/');
                rebuilt.Append(segments[i]);
            }

            rebuilt.Append("/OData/");
            odataRoot = WithPath(annotation, rebuilt.ToString());
            return true;
        }

        /// <summary>
        /// <c>Locations</c> ordered by <c>LastModified</c> descending, at most <paramref name="maxCount"/> rows.
        /// Optional filters (sections / usernames / structure) are ANDed into <c>$filter</c>.
        /// </summary>
        public static Uri BuildRecentLocationsUri(
            Uri odataRoot,
            int maxCount,
            IReadOnlyList<long> sections,
            IReadOnlyList<string> usernames = null,
            string structureOrLabel = null)
        {
            if (odataRoot is null)
                throw new ArgumentNullException(nameof(odataRoot));
            if (maxCount < 1)
                throw new ArgumentOutOfRangeException(nameof(maxCount));

            var query = new StringBuilder();
            query.Append("$orderby=LastModified%20desc");
            query.Append("&$top=").Append(maxCount.ToString(CultureInfo.InvariantCulture));
            query.Append("&$select=ID,ParentID,Z,X,Y,VolumeX,VolumeY,Radius,TypeCode,LastModified,Username,MosaicShape,VolumeShape");
            query.Append("&$expand=Parent($select=ID,Label;$expand=Type($select=Name,Code))");

            string filter = BuildFilter(sections, usernames, structureOrLabel);
            if (!string.IsNullOrEmpty(filter))
                query.Append("&$filter=").Append(Uri.EscapeDataString(filter));

            string root = odataRoot.AbsoluteUri.TrimEnd('/');
            return new Uri(root + "/Locations?" + query);
        }

        /// <summary>
        /// Combines section, username, and structure/label clauses with <c>and</c>.
        /// Null when nothing restricts the query.
        /// </summary>
        public static string BuildFilter(
            IReadOnlyList<long> sections,
            IReadOnlyList<string> usernames,
            string structureOrLabel)
        {
            var parts = new List<string>();

            string sectionFilter = BuildSectionFilter(sections);
            if (!string.IsNullOrEmpty(sectionFilter))
                parts.Add(sectionFilter.Contains(" or ") ? "(" + sectionFilter + ")" : sectionFilter);

            string userFilter = BuildUsernameFilter(usernames);
            if (!string.IsNullOrEmpty(userFilter))
                parts.Add(userFilter);

            string structureFilter = BuildStructureFilter(structureOrLabel);
            if (!string.IsNullOrEmpty(structureFilter))
                parts.Add(structureFilter);

            return parts.Count == 0 ? null : string.Join(" and ", parts);
        }

        /// <summary>
        /// OData <c>$filter</c> over location <c>Z</c>. Adjacent numbers collapse to <c>Z ge / Z le</c>.
        /// Returns null when <paramref name="sections"/> is null or empty (no section restriction).
        /// </summary>
        public static string BuildSectionFilter(IReadOnlyList<long> sections)
        {
            if (sections is null || sections.Count == 0)
                return null;

            var sorted = new long[sections.Count];
            for (int i = 0; i < sections.Count; i++)
                sorted[i] = sections[i];
            Array.Sort(sorted);

            var parts = new List<string>();
            int index = 0;
            while (index < sorted.Length)
            {
                if (index > 0 && sorted[index] == sorted[index - 1])
                {
                    index++;
                    continue;
                }

                long start = sorted[index];
                long end = start;
                while (index + 1 < sorted.Length && sorted[index + 1] == end + 1)
                {
                    index++;
                    end = sorted[index];
                }

                parts.Add(start == end
                    ? FormattableString.Invariant($"Z eq {start}")
                    : FormattableString.Invariant($"(Z ge {start} and Z le {end})"));
                index++;
            }

            return parts.Count == 0 ? null : string.Join(" or ", parts);
        }

        /// <summary>
        /// Exact username match (case-insensitive via <c>tolower</c>), OR of watched names.
        /// </summary>
        public static string BuildUsernameFilter(IReadOnlyList<string> usernames)
        {
            if (usernames is null || usernames.Count == 0)
                return null;

            var parts = new List<string>();
            foreach (string raw in usernames)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                string escaped = EscapeODataString(raw.Trim().ToLowerInvariant());
                parts.Add($"tolower(Username) eq '{escaped}'");
            }

            if (parts.Count == 0)
                return null;
            return parts.Count == 1 ? parts[0] : "(" + string.Join(" or ", parts) + ")";
        }

        /// <summary>
        /// Structure id equality or case-insensitive label/id substring via <c>contains</c>.
        /// </summary>
        public static string BuildStructureFilter(string structureOrLabel)
        {
            IReadOnlyList<string> tokens = Viking.Common.StructureOrLabelMatch.Tokenize(structureOrLabel);
            if (tokens.Count == 0)
                return null;

            var parts = new List<string>();
            foreach (string token in tokens)
            {
                if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) && id > 0)
                {
                    parts.Add(FormattableString.Invariant($"ParentID eq {id}"));
                    continue;
                }

                string escaped = EscapeODataString(token.ToLowerInvariant());
                parts.Add($"contains(tolower(Parent/Label),'{escaped}')");
            }

            if (parts.Count == 0)
                return null;
            return parts.Count == 1 ? parts[0] : "(" + string.Join(" or ", parts) + ")";
        }

        static string EscapeODataString(string value) =>
            (value ?? "").Replace("'", "''");

        /// <summary>
        /// Reads an OData v4 <c>Locations</c> payload (<c>value</c> array) into feed rows.
        /// Dates stay strings until parsed as UTC. A bad row is skipped.
        /// </summary>
        public static IReadOnlyList<ReviewChangeEntry> ParseLocations(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<ReviewChangeEntry>();

            JToken root;
            using (var reader = new JsonTextReader(new StringReader(json)))
            {
                reader.DateParseHandling = DateParseHandling.None;
                root = JToken.Load(reader);
            }

            JArray rows = root?["value"] as JArray;
            if (rows is null)
                return Array.Empty<ReviewChangeEntry>();

            var entries = new List<ReviewChangeEntry>(rows.Count);
            foreach (JToken row in rows)
            {
                if (TryParseLocation(row, out ReviewChangeEntry entry))
                    entries.Add(entry);
            }

            return entries;
        }

        static bool TryParseLocation(JToken row, out ReviewChangeEntry entry)
        {
            entry = null!;
            if (row is null || row.Type != JTokenType.Object)
                return false;

            if (!TryReadInt64(Prop(row, "ID"), out long id) || id <= 0)
                return false;
            if (!TryReadTime(Prop(row, "LastModified"), out DateTime modifiedUtc))
                return false;

            long? structureId = null;
            if (TryReadInt64(Prop(row, "ParentID"), out long parentId) && parentId > 0)
                structureId = parentId;

            int section = 0;
            if (TryReadInt64(Prop(row, "Z"), out long z) && z > 0 && z <= int.MaxValue)
                section = (int)z;

            JToken parent = Prop(row, "Parent");
            string label = Prop(parent, "Label")?.Value<string>();
            JToken type = Prop(parent, "Type");
            string typeName = Prop(type, "Name")?.Value<string>();
            if (string.IsNullOrWhiteSpace(typeName))
                typeName = Prop(type, "Code")?.Value<string>();
            if (string.IsNullOrWhiteSpace(typeName))
                typeName = "Location";

            double radius = 0;
            if (TryReadDouble(Prop(row, "Radius"), out double parsedRadius) && parsedRadius > 0)
                radius = parsedRadius;

            // Prefer volume shape (viewer / active transform); mosaic when volume geometry is missing.
            bool usedVolume = TryReadShape(Prop(row, "VolumeShape"), out Vector2[] ring, out Rectangle bbox)
                && ring.Length > 0;
            if (!usedVolume)
            {
                if (!TryReadShape(Prop(row, "MosaicShape"), out ring, out bbox) || ring.Length == 0)
                {
                    ring = Array.Empty<Vector2>();
                    bbox = default;
                }
            }

            ReviewChangeCoordinateSpace space = usedVolume
                ? ReviewChangeCoordinateSpace.Volume
                : ReviewChangeCoordinateSpace.Mosaic;

            double centerX = 0;
            double centerY = 0;
            if (usedVolume)
            {
                if (!TryReadDouble(Prop(row, "VolumeX"), out centerX))
                    TryReadDouble(Prop(row, "X"), out centerX);
                if (!TryReadDouble(Prop(row, "VolumeY"), out centerY))
                    TryReadDouble(Prop(row, "Y"), out centerY);
            }
            else
            {
                if (!TryReadDouble(Prop(row, "X"), out centerX))
                    TryReadDouble(Prop(row, "VolumeX"), out centerX);
                if (!TryReadDouble(Prop(row, "Y"), out centerY))
                    TryReadDouble(Prop(row, "VolumeY"), out centerY);
            }

            double minX = bbox.Left;
            double minY = bbox.Bottom;
            double maxX = bbox.Right;
            double maxY = bbox.Top;
            if (ring.Length == 0 && (maxX - minX) <= 1e-6 && (maxY - minY) <= 1e-6)
            {
                // No polygon: circle from radius. Prefer volume center when present.
                bool haveVx = TryReadDouble(Prop(row, "VolumeX"), out double vx);
                bool haveVy = TryReadDouble(Prop(row, "VolumeY"), out double vy);
                if (haveVx && haveVy)
                {
                    centerX = vx;
                    centerY = vy;
                    space = ReviewChangeCoordinateSpace.Volume;
                }
                else
                {
                    if (!TryReadDouble(Prop(row, "X"), out centerX))
                        centerX = 0;
                    if (!TryReadDouble(Prop(row, "Y"), out centerY))
                        centerY = 0;
                    space = ReviewChangeCoordinateSpace.Mosaic;
                }

                minX = centerX - radius;
                maxX = centerX + radius;
                minY = centerY - radius;
                maxY = centerY + radius;
            }

            entry = new ReviewChangeEntry(
                id,
                structureId,
                label,
                typeName,
                section,
                modifiedUtc,
                Prop(row, "Username")?.Value<string>(),
                centerX,
                centerY,
                minX,
                minY,
                maxX,
                maxY,
                ring,
                space,
                isDeleted: false);
            return true;
        }

        static bool TryReadShape(JToken shape, out Vector2[] ring, out Rectangle bbox)
        {
            ring = Array.Empty<Vector2>();
            bbox = default;
            JToken geometry = Prop(shape, "Geometry");
            if (geometry is null || geometry.Type == JTokenType.Null)
                return false;

            int srid = 0;
            if (TryReadInt64(Prop(geometry, "CoordinateSystemId"), out long sridValue) && sridValue >= 0 && sridValue <= int.MaxValue)
                srid = (int)sridValue;

            try
            {
                SqlGeometry sql = null;
                string wkt = Prop(geometry, "WellKnownText")?.Value<string>();
                if (!string.IsNullOrWhiteSpace(wkt))
                    sql = SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(wkt), srid);
                else
                {
                    string wkbText = Prop(geometry, "WellKnownBinary")?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(wkbText))
                    {
                        byte[] wkb = Convert.FromBase64String(wkbText);
                        sql = SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(wkb), srid);
                    }
                }

                if (sql is null || sql.IsNull)
                    return false;

                ring = sql.ToPoints() ?? Array.Empty<Vector2>();
                bbox = sql.BoundingBox();
                return true;
            }
            catch (Exception)
            {
                ring = Array.Empty<Vector2>();
                bbox = default;
                return false;
            }
        }

        static JToken Prop(JToken token, string name)
        {
            if (token is not JObject obj)
                return null;
            return obj.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out JToken value) ? value : null;
        }

        static bool TryReadInt64(JToken token, out long value)
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

        static bool TryReadDouble(JToken token, out double value)
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

        static bool TryReadTime(JToken token, out DateTime utc)
        {
            utc = default;
            if (token is null || token.Type == JTokenType.Null)
                return false;

            string text = token.Type == JTokenType.Date
                ? token.Value<DateTime>().ToString("o", CultureInfo.InvariantCulture)
                : token.Value<string>();
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset parsed))
                return false;

            utc = parsed.UtcDateTime;
            return true;
        }

        static Uri WithPath(Uri host, string path) =>
            new UriBuilder(host)
            {
                Path = path,
                Query = string.Empty,
                Fragment = string.Empty
            }.Uri;
    }
}
