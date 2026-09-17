using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel.gRPC
{
    /// <summary>
    /// Composition-root implementation of <see cref="IAnnotationStores"/> that wires together the
    /// gRPC-backed store implementations for consumption by the static <see cref="Store"/> facade.
    /// </summary>
    public class AnnotationStores : IAnnotationStores
    {
        public ILocationStore Locations { get; }

        public IStructureStore Structures { get; }

        public IStructureTypeStore StructureTypes { get; }

        public IStructureLinkStore StructureLinks { get; }

        public ILocationLinkStore LocationLinks { get; }

        public IPermittedStructureLinkStore PermittedStructureLinks { get; }

        /// <summary>
        /// Same instance as the RegionLoader wrapping mosaic-cell AnnotationSet streams.
        /// </summary>
        public IRegionLoader<LocationObj> LocationsByRegion { get; }

        public AnnotationStores(ILocationStore locations,
            IStructureStore structures,
            IStructureTypeStore structureTypes,
            IStructureLinkStore structureLinks,
            ILocationLinkStore locationLinks,
            IPermittedStructureLinkStore permittedStructureLinks,
            IRegionLoader<LocationObj> locationsByRegion,
            LocationLinkToLocationUpdater locationLinkToLocationUpdater)
        {
            Locations = locations;
            Structures = structures;
            StructureTypes = structureTypes;
            StructureLinks = structureLinks;
            LocationLinks = locationLinks;
            PermittedStructureLinks = permittedStructureLinks;
            LocationsByRegion = locationsByRegion;
            // Constructed for its CollectionChanged subscription; keep the instance alive via DI.
            _ = locationLinkToLocationUpdater;
        }

        /// <summary>
        /// Warm static tables. Types before permitted links (updater attaches to types).
        /// Locations are not loaded here — they arrive from region/section queries after the view exists.
        /// </summary>
        public async Task InitializeAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            // Types first so PermittedStructureLink → StructureType updater can attach links.
            if (StructureTypes is StructureTypeStore structureTypeStore)
                await structureTypeStore.InitializeAsync().ConfigureAwait(false);

            token.ThrowIfCancellationRequested();
            if (PermittedStructureLinks is PermittedStructureLinkStore permittedStore)
                await permittedStore.InitializeAsync().ConfigureAwait(false);

            token.ThrowIfCancellationRequested();
            if (Structures is StructureStore structureStore)
                await structureStore.InitializeAsync().ConfigureAwait(false);
        }
    }
}
