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
    /// Reads the residual-field inputs (candidate locations, links, scale, section fingerprints) from an annotation OData
    /// endpoint, for annotation databases the correction service has no SQL connection to. Produces the same rows as
    /// <see cref="SqlMorphologyLoader"/>, so the rest of the publish pipeline is shared.
    /// </summary>
    /// <remarks>
    /// Built on the generated OData <see cref="Container"/> and the paging, retry and scale helpers of
    /// <see cref="ODataMorphologyFactory"/>, which the mesh generators also use. Only the residual-field query and the
    /// fingerprints are specific to corrections, and the generated client does not expose them as typed members. Links are
    /// drained in full and filtered to the candidates locally, because the service has no link query keyed by candidate
    /// set. The endpoint is called anonymously, like the other OData clients in this repo. Calls block, so run them on a
    /// worker thread.
    /// </remarks>
    public sealed class ODataMorphologyLoader : ISectionFingerprintSource
    {
        const string LocationSelect = "ID,ParentID,Z,X,Y,Terminal,OffEdge,TypeCode,LastModified";

        readonly Container _container;

        /// <param name="odataEndpoint">Annotation OData root, e.g. <c>https://host/RC1/OData/</c>.</param>
        public ODataMorphologyLoader(Uri odataEndpoint)
        {
            ArgumentNullException.ThrowIfNull(odataEndpoint);
            _container = new Container(odataEndpoint) { MergeOption = MergeOption.NoTracking };
        }

        /// <summary>Reads the database scale through the same call the mesh generators use.</summary>
        public Task<UnitsAndScale.Scale> LoadScaleAsync(CancellationToken cancellationToken) =>
            ODataMorphologyFactory.GetScaleAsync(_container, cancellationToken);

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

                DataServiceQuery<LocationLink> linkQuery = _container.LocationLinks.AddQueryOption("$select", "A,B,Created");
                return Viking.SectionCorrection.SectionFingerprintBuilder.Compute(
                    locations,
                    ODataMorphologyFactory.ExecuteAllPagesWithRetry(_container, linkQuery, cancellationToken)
                        .Select(l => (l.A, l.B, l.Created.DateTime)));
            }, cancellationToken);

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
