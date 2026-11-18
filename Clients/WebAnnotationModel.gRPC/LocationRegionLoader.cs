using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Geometry;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC
{
    /// <summary>
    /// Mosaic region loads through RegionLoader cells (2000×2000 at full resolution, three times wider per pyramid level as
    /// the view zooms out; each level's MinRadius is sent to the server). Each cell passes its own LastQuery.
    /// After cells complete, at most one incremental GetLocationLinksForSection for deleted links —
    /// never a full-section dump on first visit. The first load of a section seeds its watermark with the oldest
    /// server query time of the cells in view; later loads leave it to the 30s poll.
    /// </summary>
    class LocationRegionLoader : IRegionLoader<LocationObj>
    {
        private readonly RegionLoader<long, LocationObj, AnnotationSet> _cells;
        private readonly LocationStore _locations;
        private readonly ILocationLinkStore _locationLinks;

        public LocationRegionLoader(
            ILocationStore locationStore,
            IServerAnnotationsClientFactory<IServerSpatialAnnotationsClient<long, AnnotationSet>> clientFactory,
            IServerQueryMultipleAddsOrUpdatesHandler<AnnotationSet> serverObjProcessor,
            IServerQueryDeleteHandler<long> serverDeletesProcessor,
            IBoundingBoxConverter<LocationObj> geometryConverter,
            ILocationLinkStore locationLinks)
        {
            _locations = locationStore as LocationStore
                ?? throw new ArgumentException($"{nameof(ILocationStore)} must be {nameof(LocationStore)}", nameof(locationStore));
            _locationLinks = locationLinks;
            _cells = new RegionLoader<long, LocationObj, AnnotationSet>(
                _locations, clientFactory, serverObjProcessor, serverDeletesProcessor, geometryConverter);
        }

        /// <summary>
        /// Loads the locations in the region through the region cells. Without <see cref="QueryTargets.Server"/> in
        /// <paramref name="queryTargets"/> it only reads the cells and changes no section state. With it, the section is marked
        /// in use and, when the section already has a watermark, one incremental location-link sync fetches deleted links
        /// (a failure is reported to <see cref="ServerLoadErrors"/> and the locations still returned; cancellation is rethrown).
        /// A failed region load is reported there too and rethrown. A section without a watermark
        /// instead has it seeded from the oldest cell query time. <paramref name="foundObjectsCallback"/> is passed to the cell loader.
        /// </summary>
        /// <returns>The locations found in the region.</returns>
        public async Task<List<LocationObj>> GetObjectsInRegionAsync(
            Rectangle VolumeBounds,
            double screenPixelSizeInVolume,
            int sectionNumber,
            QueryTargets queryTargets,
            CancellationToken token,
            Action<ICollection<LocationObj>> foundObjectsCallback)
        {
            if ((queryTargets & QueryTargets.Server) == 0)
            {
                return await _cells.GetObjectsInRegionAsync(
                    VolumeBounds, screenPixelSizeInVolume, sectionNumber, queryTargets, token, foundObjectsCallback)
                    .ConfigureAwait(false);
            }

            bool hadWatermark = _locations.TryGetSectionQueryTime(sectionNumber, out DateTime lastQueryUtc);

            List<LocationObj> result;
            try
            {
                result = await _cells.GetObjectsInRegionAsync(
                    VolumeBounds, screenPixelSizeInVolume, sectionNumber, queryTargets, token, foundObjectsCallback)
                    .ConfigureAwait(false);
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                // Tell the viewer why the section is empty; the caller still sees the failure.
                ServerLoadErrors.Report("annotations", sectionNumber, e);
                throw;
            }
            _locations.MarkSectionUsed(sectionNumber);

            if (hadWatermark)
            {
                try
                {
                    await _locationLinks.GetLinksForSectionAsync(sectionNumber, lastQueryUtc, token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    ServerLoadErrors.Report("location links", sectionNumber, e);
                    return result;
                }
            }

            // Seed, never advance: a load served from warm cells asked the server nothing, so stamping "now" made the
            // poll skip changes made since those cells were fetched (a new polygon, another user's edit).
            if (!hadWatermark)
            {
                DateTime? oldestCellQuery = _cells.OldestQueryTimeInRegion(VolumeBounds, screenPixelSizeInVolume, sectionNumber);
                if (oldestCellQuery.HasValue)
                    _locations.SeedSectionQueryTime(sectionNumber, oldestCellQuery.Value);
            }

            return result;
        }

        /// <inheritdoc />
        public void InvalidateSection(int sectionNumber) => _cells.InvalidateSection(sectionNumber);

        /// <inheritdoc />
        public bool AreRegionQueriesComplete(Geometry.Rectangle? volumeBounds, double screenPixelSizeInVolume, int sectionNumber) =>
            _cells.AreRegionQueriesComplete(volumeBounds, screenPixelSizeInVolume, sectionNumber);
    }
}
