using System;
using System.Collections.Generic;
using System.Linq;

namespace AnnotationVizLib
{
    /// <summary>
    /// Builds OData entity URLs from an annotation-service host.
    /// The OData root is the annotation path with the <c>Annotation</c> segment replaced by
    /// <c>OData</c>; the entity query is then appended to that root. Also accepts a volume
    /// service root or an OData root that is already correct.
    /// </summary>
    public static class ODataLinkBuilder
    {
        /// <summary>
        /// OData service root derived from <paramref name="annotationOrRelatedUrl"/>.
        /// Replaces a path segment named Annotation with OData and drops anything after it.
        /// When the path already has an OData segment, keeps the path through that segment.
        /// Otherwise appends <c>/OData</c> to the directory of the URL (file names such as
        /// Service.svc are dropped).
        /// </summary>
        public static string ServiceRoot(string annotationOrRelatedUrl)
        {
            if (string.IsNullOrWhiteSpace(annotationOrRelatedUrl))
                return annotationOrRelatedUrl;

            if (!Uri.TryCreate(annotationOrRelatedUrl.Trim(), UriKind.Absolute, out Uri uri))
                return annotationOrRelatedUrl.TrimEnd('/');

            string[] segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int annotationIndex = IndexOfSegment(segments, "Annotation");
            if (annotationIndex >= 0)
                return BuildRoot(uri, WithReplacedTail(segments, annotationIndex, "OData"));

            int odataIndex = IndexOfSegment(segments, "OData");
            if (odataIndex >= 0)
                return BuildRoot(uri, segments.Take(odataIndex + 1));

            // Volume service root or similar: drop a trailing file name, then append OData.
            IEnumerable<string> directory = segments.Length > 0 && LooksLikeFile(segments[segments.Length - 1])
                ? segments.Take(segments.Length - 1)
                : segments;
            return BuildRoot(uri, AppendSegment(directory, "OData"));
        }

        /// <summary>OData URL for one structure entity.</summary>
        public static string Structure(string annotationOrRelatedUrl, long id) =>
            string.Format("{0}/Structures({1})", ServiceRoot(annotationOrRelatedUrl), id);

        /// <summary>OData URL for one structure entity.</summary>
        public static string Structure(string annotationOrRelatedUrl, ulong id) =>
            string.Format("{0}/Structures({1})", ServiceRoot(annotationOrRelatedUrl), id);

        /// <summary>OData URL for one location entity.</summary>
        public static string Location(string annotationOrRelatedUrl, long id) =>
            string.Format("{0}/Locations({1})", ServiceRoot(annotationOrRelatedUrl), id);

        /// <summary>OData URL for one location entity.</summary>
        public static string Location(string annotationOrRelatedUrl, ulong id) =>
            string.Format("{0}/Locations({1})", ServiceRoot(annotationOrRelatedUrl), id);

        /// <summary>OData filter URL for structures whose label starts with <paramref name="labelPrefix"/>.</summary>
        public static string StructuresByLabelPrefix(string annotationOrRelatedUrl, string labelPrefix) =>
            string.Format("{0}/Structures?$filter=startswith(Label,'{1}') eq true",
                ServiceRoot(annotationOrRelatedUrl), labelPrefix);

        /// <summary>
        /// Export service root from the same family of URLs: Annotation becomes Export,
        /// an existing Export segment is kept, otherwise <c>/Export</c> is appended.
        /// </summary>
        public static string ExportServiceRoot(string annotationOrRelatedUrl)
        {
            if (string.IsNullOrWhiteSpace(annotationOrRelatedUrl))
                return annotationOrRelatedUrl;

            if (!Uri.TryCreate(annotationOrRelatedUrl.Trim(), UriKind.Absolute, out Uri uri))
                return annotationOrRelatedUrl.TrimEnd('/');

            string[] segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int annotationIndex = IndexOfSegment(segments, "Annotation");
            if (annotationIndex >= 0)
                return BuildRoot(uri, WithReplacedTail(segments, annotationIndex, "Export"));

            int exportIndex = IndexOfSegment(segments, "Export");
            if (exportIndex >= 0)
                return BuildRoot(uri, segments.Take(exportIndex + 1));

            int odataIndex = IndexOfSegment(segments, "OData");
            if (odataIndex >= 0)
                return BuildRoot(uri, WithReplacedTail(segments, odataIndex, "Export"));

            IEnumerable<string> directory = segments.Length > 0 && LooksLikeFile(segments[segments.Length - 1])
                ? segments.Take(segments.Length - 1)
                : segments;
            return BuildRoot(uri, AppendSegment(directory, "Export"));
        }

        private static int IndexOfSegment(string[] segments, string name)
        {
            for (int i = 0; i < segments.Length; i++)
            {
                if (string.Equals(segments[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static bool LooksLikeFile(string segment) =>
            segment.IndexOf('.') >= 0;

        private static IEnumerable<string> WithReplacedTail(string[] segments, int index, string replacement) =>
            AppendSegment(segments.Take(index), replacement);

        private static IEnumerable<string> AppendSegment(IEnumerable<string> segments, string segment) =>
            segments.Concat(new[] { segment });

        private static string BuildRoot(Uri uri, IEnumerable<string> segments)
        {
            var builder = new UriBuilder(uri)
            {
                Path = "/" + string.Join("/", segments),
                Query = string.Empty,
                Fragment = string.Empty
            };
            return builder.Uri.AbsoluteUri.TrimEnd('/');
        }
    }
}
