using AnnotationVizLib.OData;
using Microsoft.OData.Client;
using ODataClient.ConnectomeDataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.SectionCorrectionBuilder
{
    /// <summary>
    /// Reads the residual-field inputs (candidate locations, links, scale, watermark) from an annotation OData
    /// endpoint, for annotation databases the correction service has no SQL connection to. Produces the same rows as
    /// <see cref="SqlMorphologyLoader"/>, so the rest of the publish pipeline is shared.
    /// </summary>
    /// <remarks>
    /// Built on the generated OData <see cref="Container"/> and the paging, retry and scale helpers of
    /// <see cref="ODataMorphologyFactory"/>, which the mesh generators also use. Only the residual-field query and the
    /// watermark are specific to corrections, and the generated client does not expose them as typed members. Links are
    /// drained in full and filtered to the candidates locally, because the service has no link query keyed by candidate
    /// set. The endpoint is called anonymously, like the other OData clients in this repo. Calls block, so run them on a
    /// worker thread.
    /// </remarks>
    public sealed class ODataMorphologyLoader : IMorphologySource
    {
        const string LocationSelect = "ID,ParentID,Z,X,Y,Terminal,OffEdge,TypeCode,LastModified";

        /// <summary>Terms per <c>$filter</c>, to keep request URLs well under server limits.</summary>
        const int FilterTermsPerQuery = 40;

        readonly Container _container;
        readonly object _linkGate = new();
        List<SqlMorphologyLoader.LinkRow> _allLinks;

        /// <param name="odataEndpoint">Annotation OData root, e.g. <c>https://host/RC1/OData/</c>.</param>
        public ODataMorphologyLoader(Uri odataEndpoint)
        {
            ArgumentNullException.ThrowIfNull(odataEndpoint);
            _container = new Container(odataEndpoint) { MergeOption = MergeOption.NoTracking };
        }

        /// <summary>Reads the database scale through the same call the mesh generators use.</summary>
        public Task<UnitsAndScale.Scale> LoadScaleAsync(CancellationToken cancellationToken) =>
            ODataMorphologyFactory.GetScaleAsync(_container, cancellationToken);

        /// <summary>Newest location modification or link creation, which gates whether a rebuild is needed.</summary>
        public Task<DateTime> LoadWatermarkAsync(CancellationToken cancellationToken) =>
            Task.Run(() =>
            {
                DateTime loc = ODataMorphologyFactory.ExecuteAllPagesWithRetry(
                        _container,
                        (DataServiceQuery<Location>)_container.Locations.OrderByDescending(l => l.LastModified).Take(1),
                        cancellationToken)
                    .Select(l => l.LastModified.DateTime).DefaultIfEmpty(DateTime.MinValue).Max();
                DateTime link = ODataMorphologyFactory.ExecuteAllPagesWithRetry(
                        _container,
                        (DataServiceQuery<LocationLink>)_container.LocationLinks.OrderByDescending(l => l.Created).Take(1),
                        cancellationToken)
                    .Select(l => l.Created.DateTime).DefaultIfEmpty(DateTime.MinValue).Max();
                return loc > link ? loc : link;
            }, cancellationToken);

        /// <summary>
        /// Loads locations of structures with at least <paramref name="minLocations"/> locations, and the links among them.
        /// </summary>
        /// <param name="minLocations">Smallest structure size worth correcting; below 1 means 3.</param>
        /// <param name="cancellationToken">Checked between pages.</param>
        public Task<(List<SqlMorphologyLoader.CandidateRow> Candidates, List<SqlMorphologyLoader.LinkRow> Links)> LoadRowsAsync(
            int minLocations, CancellationToken cancellationToken) =>
            Task.Run(() =>
            {
                int min = minLocations < 1 ? 3 : minLocations;

                // The server's ResidualFieldLocations function is not a typed member of the generated client.
                DataServiceQuery<Location> query = _container
                    .CreateQuery<Location>($"ResidualFieldLocations(MinLocations={min})")
                    .AddQueryOption("$select", LocationSelect);
                List<SqlMorphologyLoader.CandidateRow> candidates = [.. ODataMorphologyFactory
                    .ExecuteAllPagesWithRetry(_container, query, cancellationToken)
                    .Select(ToCandidate)];

                HashSet<long> ids = [.. candidates.Select(c => c.ID)];
                List<SqlMorphologyLoader.LinkRow> links = [];
                if (ids.Count > 0)
                {
                    DataServiceQuery<LocationLink> linkQuery = _container.LocationLinks.AddQueryOption("$select", "A,B,Created");
                    links = [.. ODataMorphologyFactory
                        .ExecuteAllPagesWithRetry(_container, linkQuery, cancellationToken)
                        .Where(l => ids.Contains(l.A) && ids.Contains(l.B))
                        .Select(l => new SqlMorphologyLoader.LinkRow { A = l.A, B = l.B, Created = l.Created.DateTime })];
                }

                return (candidates, links);
            }, cancellationToken);

        /// <summary>
        /// Per-Z fingerprints from the four columns the summary needs. The service has no group-by, so every location and
        /// link is read once (without coordinates) and reduced here; that is still far less than the full row load.
        /// </summary>
        /// <param name="cancellationToken">Checked between pages.</param>
        public Task<Dictionary<long, Viking.SectionCorrection.SectionFingerprintDto>> LoadFingerprintsAsync(
            CancellationToken cancellationToken) =>
            Task.Run(() =>
            {
                DataServiceQuery<Location> locationQuery = _container.Locations.AddQueryOption("$select", "ID,ParentID,Z,LastModified");
                List<(long Id, long ParentId, long Z, DateTime Modified)> locations = [.. ODataMorphologyFactory
                    .ExecuteAllPagesWithRetry(_container, locationQuery, cancellationToken)
                    .Select(l => (l.ID, l.ParentID, (long)l.Z, l.LastModified.DateTime))];

                return Viking.SectionCorrection.SectionFingerprintBuilder.Compute(
                    locations,
                    DrainAllLinks(cancellationToken).Select(l => (l.A, l.B, l.Created)));
            }, cancellationToken);

        /// <summary>Distinct structure IDs that own a location in any inclusive Z range.</summary>
        /// <param name="ranges">Inclusive Z ranges.</param>
        /// <param name="cancellationToken">Checked between pages.</param>
        public Task<List<long>> LoadParentIdsInRangesAsync(
            IReadOnlyList<(long Lo, long Hi)> ranges,
            CancellationToken cancellationToken) =>
            Task.Run(() =>
            {
                HashSet<long> ids = [];
                foreach (List<(long Lo, long Hi)> batch in SectionRanges.Batches(ranges, FilterTermsPerQuery))
                {
                    DataServiceQuery<Location> query = _container.Locations
                        .AddQueryOption("$select", "ParentID")
                        .AddQueryOption("$filter", RangeFilter(batch));
                    foreach (Location l in ODataMorphologyFactory.ExecuteAllPagesWithRetry(_container, query, cancellationToken))
                        ids.Add(l.ParentID);
                }

                return ids.ToList();
            }, cancellationToken);

        /// <summary>Every location of the structures, and the links among them.</summary>
        /// <param name="parentIds">Structure IDs.</param>
        /// <param name="cancellationToken">Checked between pages.</param>
        public Task<(List<SqlMorphologyLoader.CandidateRow> Candidates, List<SqlMorphologyLoader.LinkRow> Links)> LoadStructureRowsAsync(
            IReadOnlyCollection<long> parentIds,
            CancellationToken cancellationToken) =>
            Task.Run(() =>
            {
                List<SqlMorphologyLoader.CandidateRow> candidates = [];
                foreach (List<long> batch in SectionRanges.Batches(parentIds, FilterTermsPerQuery))
                {
                    DataServiceQuery<Location> query = _container.Locations
                        .AddQueryOption("$select", LocationSelect)
                        .AddQueryOption("$filter", ParentFilter(batch));
                    candidates.AddRange(ODataMorphologyFactory
                        .ExecuteAllPagesWithRetry(_container, query, cancellationToken)
                        .Select(ToCandidate));
                }

                HashSet<long> ids = [.. candidates.Select(c => c.ID)];
                List<SqlMorphologyLoader.LinkRow> links = [.. DrainAllLinks(cancellationToken)
                    .Where(l => ids.Contains(l.A) && ids.Contains(l.B))];
                return (candidates, links);
            }, cancellationToken);

        /// <summary>OData <c>$filter</c> matching any of the inclusive Z ranges.</summary>
        internal static string RangeFilter(IEnumerable<(long Lo, long Hi)> ranges) =>
            string.Join(" or ", ranges.Select(r => FormattableString.Invariant($"(Z ge {r.Lo} and Z le {r.Hi})")));

        /// <summary>OData <c>$filter</c> matching any of the structure IDs.</summary>
        internal static string ParentFilter(IEnumerable<long> parentIds) =>
            string.Join(" or ", parentIds.Select(id => FormattableString.Invariant($"ParentID eq {id}")));

        /// <summary>
        /// All links, read once per loader: the incremental publish asks for structure rows twice and the service has
        /// no link query keyed by structure.
        /// </summary>
        List<SqlMorphologyLoader.LinkRow> DrainAllLinks(CancellationToken cancellationToken)
        {
            lock (_linkGate)
            {
                _allLinks ??= [.. ODataMorphologyFactory
                    .ExecuteAllPagesWithRetry(
                        _container,
                        _container.LocationLinks.AddQueryOption("$select", "A,B,Created"),
                        cancellationToken)
                    .Select(l => new SqlMorphologyLoader.LinkRow { A = l.A, B = l.B, Created = l.Created.DateTime })];
                return _allLinks;
            }
        }

        static SqlMorphologyLoader.CandidateRow ToCandidate(Location l) => new()
        {
            ID = l.ID,
            ParentID = l.ParentID,
            Z = (long)l.Z,
            X = l.X,
            Y = l.Y,
            Terminal = l.Terminal,
            OffEdge = l.OffEdge,
            TypeCode = (short)l.TypeCode,
            LastModified = l.LastModified.DateTime
        };
    }
}
