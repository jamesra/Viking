using System;
using System.Linq;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel.gRPC.Converters
{
    /// <summary>
    /// An annotation set combines both structures and locations in a single result set.  This result processor
    /// handles updating both stores when these results arrive
    /// </summary>
    class ProcessorForServerAnnotationSets : IServerQuerySingleAddOrUpdateHandler<AnnotationSet>
    { 
        private readonly IStoreServerQueryResultsHandler<long, StructureObj, Structure> StructureProcessor;
        private readonly IStoreServerQueryResultsHandler<long, LocationObj, Location> LocationProcessor;

        public ProcessorForServerAnnotationSets(IStoreServerQueryResultsHandler<long, StructureObj, Structure> structureProcessor,
            IStoreServerQueryResultsHandler<long, LocationObj, Location> locationProcessor)
        {
            StructureProcessor = structureProcessor;
            LocationProcessor = locationProcessor;
        }

        public async Task ProcessServerResult(DateTime queryTime, AnnotationSet obj)
        {
            var structures = await StructureProcessor.ProcessServerUpdate(obj.Structures.ToArray(), null);


            var locations = await LocationProcessor.ProcessServerUpdate(obj.Locations.ToArray(), null);

            await StructureProcessor.EndBatch(structures);
            await LocationProcessor.EndBatch(locations);
        }
    }
}
