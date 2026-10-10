using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Viking.Common;
using Viking.UI;

namespace WebAnnotation.ReviewFeed
{
    /// <summary>
    /// Seeds the Review change feed with the newest locations from the annotation host's OData service.
    /// The OData root is the annotation WCF URL with the <c>Annotation</c> path segment replaced by <c>OData</c>.
    /// WCF cannot ask for the most recent N changes across sections, so this issues
    /// <c>Locations?$orderby=LastModified desc&amp;$top=N</c> and merges the rows with the live poll.
    /// Failures set <see cref="ReviewChangeFeed.SeedStatusMessage"/> so the Review tab can show them.
    /// </summary>
    public static class ReviewChangeFeedBackfill
    {
        /// <summary>
        /// Runs on a worker thread from the Review tab load or filter Apply.
        /// Later seeds merge with live poll rows via <see cref="ReviewChangeFeed.SeedRecent"/>.
        /// </summary>
        public static void Run(ReviewChangeFeed feed, ReviewChangeFeedFilter filter = null)
        {
            if (feed is null)
                throw new ArgumentNullException(nameof(feed));

            filter ??= ReviewChangeFeedFilter.Session;

            try
            {
                if (!filter.SectionsParse.Success)
                {
                    string message = $"Review seed skipped: {filter.SectionsParse.Interpretation}";
                    Trace.WriteLine(message, "WebAnnotation");
                    feed.SetSeedStatus(message);
                    return;
                }

                Uri annotationEndpoint = WebAnnotationModel.State.Endpoint;
                string annotationUrl = annotationEndpoint?.AbsoluteUri;
                if (!ReviewChangeODataQuery.TryODataRoot(annotationUrl, out Uri odataRoot))
                {
                    string message =
                        "Could not resolve the OData URL from the annotation endpoint (expected an …/Annotation/… path). Recent changes were not seeded.";
                    Trace.WriteLine(
                        $"Review feed backfill: {message} AnnotationEndpoint={annotationUrl ?? "(null)"}",
                        "WebAnnotation");
                    feed.SetSeedStatus(message);
                    feed.SetPollIngressCutoffUtc(DateTime.UtcNow - ReviewChangeFeed.DefaultLookback);
                    return;
                }

                IReadOnlyList<long> sections = null;
                if (!filter.IsAllSections)
                {
                    IReadOnlyList<int> present = ResolveSectionsToQuery(filter);
                    if (present.Count == 0)
                    {
                        feed.SetSeedStatus("No matching sections in this volume for the section filter.");
                        return;
                    }

                    var asLong = new long[present.Count];
                    for (int i = 0; i < present.Count; i++)
                        asLong[i] = present[i];
                    sections = asLong;
                }

                IReadOnlyList<string> usernames = StructureOrLabelMatch.Tokenize(filter.WatchedUsersText);
                Uri query = ReviewChangeODataQuery.BuildRecentLocationsUri(
                    odataRoot,
                    ReviewChangeFeed.DefaultMaxEntries,
                    sections,
                    usernames,
                    filter.StructureOrLabelText);

                string json;
                try
                {
                    json = Download(query);
                }
                catch (Exception ex)
                {
                    string message = $"OData recent-locations request failed: {ex.Message}";
                    Trace.WriteLine($"Review feed backfill: {message} Url={query}", "WebAnnotation");
                    feed.SetSeedStatus(message);
                    feed.SetPollIngressCutoffUtc(DateTime.UtcNow - ReviewChangeFeed.DefaultLookback);
                    return;
                }

                IReadOnlyList<ReviewChangeEntry> entries = ReviewChangeODataQuery.ParseLocations(json);
                // Always replace — filter Apply must drop rows that no longer match the query.
                feed.SeedRecent(entries);
                if (entries.Count == 0)
                {
                    Trace.WriteLine(
                        $"Review feed backfill: OData returned no locations. Url={query}",
                        "WebAnnotation");
                }
            }
            catch (Exception ex)
            {
                string message = $"Review seed failed: {ex.Message}";
                Trace.WriteLine(message, "WebAnnotation");
                feed.SetSeedStatus(message);
                feed.SetPollIngressCutoffUtc(DateTime.UtcNow - ReviewChangeFeed.DefaultLookback);
            }
        }

        /// <summary>
        /// Starts <see cref="Run"/> on the thread pool when Review access is present.
        /// </summary>
        public static void StartAsync(ReviewChangeFeed feed, ReviewChangeFeedFilter filter = null)
        {
            if (!VolumeAccessRoles.HasReviewAccess())
                return;
            _ = Task.Run(() => Run(feed, filter));
        }

        /// <summary>
        /// Sections to restrict the OData <c>Z</c> filter to. Blank filter is not passed here;
        /// otherwise only requested numbers that exist in the open volume.
        /// </summary>
        internal static IReadOnlyList<int> ResolveSectionsToQuery(ReviewChangeFeedFilter filter)
        {
            Viking.ViewModels.VolumeViewModel volume = State.volume;
            if (volume?.SectionViewModels is null || volume.SectionViewModels.Count == 0)
                return Array.Empty<int>();

            IReadOnlyList<int> volumeSections = volume.SectionViewModels.Keys.ToArray();

            SectionRangeParse parse = filter?.SectionsParse ?? SectionRangeParser.Parse("");
            if (!parse.Success || parse.IsAllSections)
                return Array.Empty<int>();

            VolumeSectionSelection selection = SectionRangeParser.SelectInVolume(
                parse.Sections,
                volumeSections);
            if (!selection.CanStart)
                return Array.Empty<int>();

            var present = new int[selection.Present.Count];
            for (int i = 0; i < selection.Present.Count; i++)
                present[i] = (int)selection.Present[i];
            return present;
        }

        /// <summary>
        /// GET the OData URL with the signed-in volume credentials and bearer token when one exists.
        /// Called from the backfill worker, not the UI thread.
        /// </summary>
        static string Download(Uri query)
        {
            using HttpClient client = HttpClientFactory.CreateClient(query, State.UserCredentials);
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            string token = State.UserBearerToken?.AccessToken;
            if (!string.IsNullOrWhiteSpace(token))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = client.GetAsync(query).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} from {query}");
            }

            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
    }
}
