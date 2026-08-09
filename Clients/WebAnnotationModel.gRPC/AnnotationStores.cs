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

        public IRegionLoader<LocationObj> LocationsByRegion { get; }

        public AnnotationStores(ILocationStore locations,
            IStructureStore structures,
            IStructureTypeStore structureTypes,
            IStructureLinkStore structureLinks,
            ILocationLinkStore locationLinks,
            IPermittedStructureLinkStore permittedStructureLinks)
        {
            Locations = locations;
            Structures = structures;
            StructureTypes = structureTypes;
            StructureLinks = structureLinks;
            LocationLinks = locationLinks;
            PermittedStructureLinks = permittedStructureLinks;
            LocationsByRegion = locations as IRegionLoader<LocationObj>;
        }
    }
}
