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
    /// Mosaic region loads through RegionLoader 2000×2000 cells. Each cell passes its own LastQuery.
    /// After cells complete, at most one incremental GetLocationLinksForSection for deleted links —
    /// never a full-section dump on first visit.
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

            var result = await _cells.GetObjectsInRegionAsync(
                VolumeBounds, screenPixelSizeInVolume, sectionNumber, queryTargets, token, foundObjectsCallback)
                .ConfigureAwait(false);

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
                    Trace.WriteLine(
                        $"GetLocationLinksForSection failed for section {sectionNumber}: {e.Message}",
                        "WebAnnotation");
                    return result;
                }
            }

            _locations.TouchSectionQueryTime(sectionNumber);
            return result;
        }
    }
}
