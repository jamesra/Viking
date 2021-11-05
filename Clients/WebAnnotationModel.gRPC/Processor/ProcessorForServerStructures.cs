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
    class ProcessorForServerStructures : IServerQuerySingleAddOrUpdateHandler<IStructure>, IServerQueryMultipleAddsOrUpdatesHandler< IStructure>
    {
        private readonly IStoreServerQueryResultsHandler<long, StructureObj, IStructure> StructureProcessor;
        private readonly IStoreServerQueryResultsHandler<StructureLinkKey, StructureLinkObj, IStructureLink> StructureLinkProcessor;

        public ProcessorForServerStructures(IStoreServerQueryResultsHandler<long, StructureObj, IStructure> structureProcessor,
            IStoreServerQueryResultsHandler<StructureLinkKey, StructureLinkObj, IStructureLink> structureLinkProcessor)
        {
            StructureProcessor = structureProcessor;
            StructureLinkProcessor = structureLinkProcessor;
        }

        public Task ProcessServerDelete(long deletedID)
        {
            return StructureProcessor.ProcessServerUpdate(new ServerUpdate<long, IStructure[]>(deleted: deletedID));
        }

        public async Task ProcessServerResult(DateTime queryTime, IStructure obj)
        {
            var links = await StructureLinkProcessor.ProcessServerUpdate(new ServerUpdate<StructureLinkKey, IStructureLink[]>(queryTime, obj.Links, Array.Empty<StructureLinkKey>()));
            
            var structures = await StructureProcessor.ProcessServerUpdate(new ServerUpdate<long, IStructure[]>(queryTime: queryTime, obj: new IStructure[]{obj}, Array.Empty<long>()));

            await StructureLinkProcessor.EndBatch(links);
            await StructureProcessor.EndBatch(structures);
        }

        public async Task ProcessServerResults(DateTime queryTime, IStructure[] input)
        {
            var links = await StructureLinkProcessor.ProcessServerUpdate(new ServerUpdate<StructureLinkKey, IStructureLink[]>(queryTime, input.SelectMany(o => o.Links).ToArray(), Array.Empty<StructureLinkKey>()));

            var structures = await StructureProcessor.ProcessServerUpdate(new ServerUpdate<long, IStructure[]>(queryTime, input, Array.Empty<long>()));
        }
    }
}
