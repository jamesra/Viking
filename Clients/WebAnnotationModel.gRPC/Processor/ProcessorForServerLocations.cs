using System;
using System.Linq;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel.gRPC.Converters
{
    /// <summary>
    /// An IStructure combines a structure and links.  This result processor
    /// handles updating both stores when these results arrive
    /// </summary>
    class ProcessorForServerLocations : IServerQuerySingleAddOrUpdateHandler<ILocation>, IServerQueryMultipleAddsOrUpdatesHandler<ILocation>
    {
        private readonly IStoreServerQueryResultsHandler<long, LocationObj, ILocation> LocationProcessor;
        private readonly IStoreServerQueryResultsHandler<LocationLinkKey, LocationLinkObj, ILocationLink> LocationLinkProcessor;

        public ProcessorForServerLocations(IStoreServerQueryResultsHandler<long, LocationObj, ILocation> locationProcessor,
            IStoreServerQueryResultsHandler<LocationLinkKey, LocationLinkObj, ILocationLink> locationLinkProcessor)
        {
            LocationProcessor = locationProcessor;
            LocationLinkProcessor = locationLinkProcessor;
        }

        public Task ProcessServerDelete(long deletedID)
        {
            return LocationProcessor.ProcessServerUpdate(new ServerUpdate<long, ILocation[]>(deleted: deletedID));
        }

        public async Task ProcessServerResult(DateTime queryTime, ILocation obj)
        {
            var links = await LocationLinkProcessor.ProcessServerUpdate(new ServerUpdate<LocationLinkKey, ILocationLink[]>(queryTime, obj.Links.Select(l => (ILocationLink)(new LocationLinkObj(l, obj.ID))).ToArray(), Array.Empty<LocationLinkKey>()));
            
            var structures = await LocationProcessor.ProcessServerUpdate(new ServerUpdate<long, ILocation[]>(queryTime: queryTime, obj: new ILocation[]{obj}, Array.Empty<long>()));

            await LocationLinkProcessor.EndBatch(links);
            await LocationProcessor.EndBatch(structures);
        }

        public async Task ProcessServerResults(DateTime queryTime, ILocation[] input)
        {
            var links = await LocationLinkProcessor.ProcessServerUpdate(new ServerUpdate<LocationLinkKey, ILocationLink[]>(queryTime, input.SelectMany(l => l.Links.Select(ll => (ILocationLink)(new LocationLinkObj(ll, l.ID)))).ToArray(), Array.Empty<LocationLinkKey>()));

            var structures = await LocationProcessor.ProcessServerUpdate(new ServerUpdate<long, ILocation[]>(queryTime, input, Array.Empty<long>()));
        }
    }
}
