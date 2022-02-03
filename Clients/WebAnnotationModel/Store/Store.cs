namespace WebAnnotationModel
{
    /// <summary>
    /// Static class that holds references to store singletons
    /// </summary>
    public class Store
    {
        public static LocationStore Locations => Nested.Locations;

        public static StructureStore Structures => Nested.Structures;

        public static StructureTypeStore StructureTypes => Nested.StructureTypes;

        public static StructureLinkStore StructureLinks => Nested.StructureLinks;

        public static LocationLinkStore LocationLinks => Nested.LocationLinks;

        public static PermittedStructureLinkStore PermittedStructureLinks => Nested.PermittedStructureLinks;

        public static RegionLoader<long, LocationObj> LocationsByRegion => Nested.RegionLocationsLoader;

        public static RegionLoader<long, StructureObj> StructuresByRegion => Nested.RegionStructuresLoader;
         
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
                RegionStructuresLoader = new RegionLoader<long, StructureObj>(Store.Structures);
            }

            internal readonly static StructureTypeStore StructureTypes = new StructureTypeStore();
            internal readonly static StructureStore Structures = new StructureStore();
            internal readonly static LocationStore Locations = new LocationStore();
            internal readonly static StructureLinkStore StructureLinks = new StructureLinkStore();
            internal readonly static LocationLinkStore LocationLinks = new LocationLinkStore();
            internal readonly static PermittedStructureLinkStore PermittedStructureLinks = new PermittedStructureLinkStore();

            internal readonly static RegionLoader<long, LocationObj> RegionLocationsLoader;
            internal readonly static RegionLoader<long, StructureObj> RegionStructuresLoader;
        }
    }
}
