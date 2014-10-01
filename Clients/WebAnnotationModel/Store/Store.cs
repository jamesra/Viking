using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebAnnotationModel
{
    /// <summary>
    /// Static class that holds references to store singletons
    /// </summary>
    public class Store
    {
        public static LocationStore Locations
        {
            get { return Nested.Locations; }
        }

        public static StructureStore Structures
        {
            get { return Nested.Structures; }
        }

        public static StructureTypeStore StructureTypes
        {
            get { return Nested.StructureTypes; }
        }

        public static StructureLinkStore StructureLinks
        {
            get { return Nested.StructureLinks; }
        }

        public static LocationLinkStore LocationLinks
        {
            get { return Nested.LocationLinks; }
        }

        class Nested
        {
            static Nested()
            {
                StructureTypes.Init(); 
                Structures.Init(); 
                Locations.Init();
                StructureLinks.Init();
                LocationLinks.Init(); 
            }

            internal readonly static StructureTypeStore StructureTypes = new StructureTypeStore();
            internal readonly static StructureStore Structures = new StructureStore();
            internal readonly static LocationStore Locations = new LocationStore();
            internal readonly static StructureLinkStore StructureLinks = new StructureLinkStore();
            internal readonly static LocationLinkStore LocationLinks = new LocationLinkStore();
        }
    }
}
