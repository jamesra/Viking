using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{
    public interface IAnnotationStores
    {
        ILocationStore Locations { get; }
    
        IStructureStore Structures { get; }

        IStructureTypeStore StructureTypes { get; }
    
        IStructureLinkStore StructureLinks { get; }

        ILocationLinkStore LocationLinks { get; }

        IPermittedStructureLinkStore PermittedStructureLinks { get; }

        IRegionLoader<LocationObj> LocationsByRegion { get; }

        //IRegionLoader<StructureObj> StructuresByRegion { get; }
    }

    public class Store
    {
        public readonly ILocationStore Locations;

        public readonly IStructureStore Structures;

        public readonly IStructureTypeStore StructureTypes;

        public readonly IStructureLinkStore StructureLinks;

        public readonly ILocationLinkStore LocationLinks;

        public readonly IPermittedStructureLinkStore PermittedStructureLinks;

        public readonly IRegionLoader<LocationObj> LocationsByRegion;

        public Store(IStructureTypeStore structureTypes,
            IStructureStore structures,
            ILocationStore locations)
        {

        }
         
    }
    
    /*
    /// <summary>
    /// Static class that holds references to store singletons
    /// </summary>
    public static class Store
    {
        
        public static ILocationStore Locations => Nested.Locations;

        public static IStructureStore Structures => Nested.Structures;

        public static IStructureTypeStore StructureTypes => Nested.StructureTypes;

        public static IStructureLinkStore StructureLinks => Nested.StructureLinks;

        public static ILocationLinkStore LocationLinks => Nested.LocationLinks;

        public static IPermittedStructureLinkStore PermittedStructureLinks => Nested.PermittedStructureLinks;

        public static IRegionLoader<LocationObj> LocationsByRegion => Nested.RegionLocationsLoader;

        //public static RegionLoader<long, StructureObj> StructuresByRegion => Nested.RegionStructuresLoader;
        
        /*
        class Nested
        {
            static Nested()
            {
                StructureTypes.Init();
                Structures.Init();
                Locations.Init();
                StructureLinks.Init();
                LocationLinks.Init();
                PermittedStructureLinks.Init();

                RegionLocationsLoader = new RegionLoader<long, LocationObj>(Store.Locations);
                //RegionStructuresLoader = new RegionLoader<long, StructureObj>(Store.Structures);
            }

            internal static readonly IStructureTypeStore StructureTypes = new StructureTypeStore();
            internal static readonly IStructureStore Structures = new StructureStore();
            internal static readonly ILocationStore Locations = new LocationStore();
            internal static readonly IStructureLinkStore StructureLinks = new StructureLinkStore();
            internal static readonly ILocationLinkStore LocationLinks = new LocationLinkStore();
            internal static readonly IPermittedStructureLinkStore PermittedStructureLinks = new PermittedStructureLinkStore();

            internal static readonly RegionLoader<long, LocationObj> RegionLocationsLoader;
            internal static readonly RegionLoader<long, StructureObj> RegionStructuresLoader;
        }
        
    }
    */
}
