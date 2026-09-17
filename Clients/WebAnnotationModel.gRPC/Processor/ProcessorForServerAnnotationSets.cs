using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC.Converters
{
    /// <summary>
    /// Merges an AnnotationSet (structures, then locations, then Location.Links) into the stores.
    /// Missing parent structures are fetched once per set via GetObjectsByIDs.
    /// </summary>
    class ProcessorForServerAnnotationSets :
        IServerQuerySingleAddOrUpdateHandler<AnnotationSet>,
        IServerQueryMultipleAddsOrUpdatesHandler<AnnotationSet>
    {
        private readonly IStructureStore _structureStore;
        private readonly ILocationStore _locationStore;
        private readonly ILocationLinkStore _locationLinkStore;
        private readonly IStructureLinkStore _structureLinkStore;

        private readonly IStoreServerQueryResultsHandler<long, StructureObj, IStructure> StructureProcessor;
        private readonly IStoreServerQueryResultsHandler<long, LocationObj, ILocation> LocationProcessor;
        private readonly IStoreServerQueryResultsHandler<LocationLinkKey, LocationLinkObj, ILocationLink> LocationLinkProcessor;
        private readonly IStoreServerQueryResultsHandler<StructureLinkKey, StructureLinkObj, IStructureLink> StructureLinkProcessor;

        public ProcessorForServerAnnotationSets(
            ILocationStore locationStore,
            IStructureStore structureStore,
            ILocationLinkStore locationLinkStore,
            IStructureLinkStore structureLinkStore,
            IObjectConverter<ILocation, LocationObj> locationConverter,
            IObjectUpdater<LocationObj, ILocation> locationUpdater,
            IObjectConverter<IStructure, StructureObj> structureConverter,
            IObjectUpdater<StructureObj, IStructure> structureUpdater,
            IObjectConverter<ILocationLink, LocationLinkObj> locationLinkConverter,
            IObjectConverter<IStructureLink, StructureLinkObj> structureLinkConverter)
        {
            _locationStore = locationStore;
            _structureStore = structureStore;
            _locationLinkStore = locationLinkStore;
            _structureLinkStore = structureLinkStore;

            LocationProcessor = new StoreServerQueryResultsHandler<long, LocationObj, ILocation>(
                (IStoreEditor<long, LocationObj>)locationStore, locationConverter, locationUpdater);
            StructureProcessor = new StoreServerQueryResultsHandler<long, StructureObj, IStructure>(
                (IStoreEditor<long, StructureObj>)structureStore, structureConverter, structureUpdater);
            LocationLinkProcessor = new StoreServerQueryResultsHandler<LocationLinkKey, LocationLinkObj, ILocationLink>(
                (IStoreEditor<LocationLinkKey, LocationLinkObj>)locationLinkStore, locationLinkConverter);
            StructureLinkProcessor = new StoreServerQueryResultsHandler<StructureLinkKey, StructureLinkObj, IStructureLink>(
                (IStoreEditor<StructureLinkKey, StructureLinkObj>)structureLinkStore, structureLinkConverter);
        }

        public Task ProcessServerResult(DateTime queryTime, AnnotationSet obj)
        {
            return ProcessServerResults(queryTime, obj == null ? Array.Empty<AnnotationSet>() : new[] { obj });
        }

        public async Task ProcessServerResults(DateTime queryTime, AnnotationSet[] objs)
        {
            if (objs == null || objs.Length == 0)
                return;

            foreach (var set in objs)
            {
                if (set == null)
                    continue;
                await MergeSetAsync(queryTime, set).ConfigureAwait(false);
            }
        }

        async Task MergeSetAsync(DateTime queryTime, AnnotationSet set)
        {
            var structures = set.Structures?.ToArray() ?? Array.Empty<Structure>();
            var locations = set.Locations?.ToArray() ?? Array.Empty<Location>();

            var structureLinks = structures
                .SelectMany(s => s.Links?.Cast<IStructureLink>() ?? Array.Empty<IStructureLink>())
                .ToArray();
            var locationLinks = locations
                .Where(l => l?.Links != null)
                .SelectMany(l => l.Links.Select(peer => (ILocationLink)new LocationLinkObj(peer, l.Id)))
                .ToArray();

            var structureChanges = await StructureProcessor
                .ProcessServerUpdate(new ServerUpdate<long, IStructure[]>(queryTime, structures.Cast<IStructure>().ToArray(), Array.Empty<long>()))
                .ConfigureAwait(false);
            var locationChanges = await LocationProcessor
                .ProcessServerUpdate(new ServerUpdate<long, ILocation[]>(queryTime, locations.Cast<ILocation>().ToArray(), Array.Empty<long>()))
                .ConfigureAwait(false);
            var structureLinkChanges = await StructureLinkProcessor
                .ProcessServerUpdate(new ServerUpdate<StructureLinkKey, IStructureLink[]>(queryTime, structureLinks, Array.Empty<StructureLinkKey>()))
                .ConfigureAwait(false);
            var locationLinkChanges = await LocationLinkProcessor
                .ProcessServerUpdate(new ServerUpdate<LocationLinkKey, ILocationLink[]>(queryTime, locationLinks, Array.Empty<LocationLinkKey>()))
                .ConfigureAwait(false);

            await ((StructureStore)_structureStore).CallOnCollectionChanged(structureChanges).ConfigureAwait(false);
            await ((LocationStore)_locationStore).CallOnCollectionChanged(locationChanges).ConfigureAwait(false);
            await ((StructureLinkStore)_structureLinkStore).CallOnCollectionChanged(structureLinkChanges).ConfigureAwait(false);
            await ((LocationLinkStore)_locationLinkStore).CallOnCollectionChanged(locationLinkChanges).ConfigureAwait(false);

            var missingParents = locations
                .Where(l => l != null && l.HasParentId && !_structureStore.Contains(l.ParentId))
                .Select(l => l.ParentId)
                .Distinct()
                .ToArray();
            if (missingParents.Length > 0)
                await _structureStore.GetObjectsByIDs(missingParents, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
