using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebAnnotation
{
    /// <summary>
    /// Static class that holds references to store singletons
    /// </summary>
    class Store
    {
        public static LocationStore Locations => Nested.Locations;

        public static StructureStore Structures => Nested.Structures;

        public static StructureTypeStore StructureTypes => Nested.StructureTypes;

        class Nested
        {
            static Nested()
            {
                StructureTypes.Init(); 
                Structures.Init(); 
                Locations.Init();
            }

            internal readonly static StructureTypeStore StructureTypes = new StructureTypeStore();
            internal readonly static StructureStore Structures = new StructureStore();
            internal readonly static LocationStore Locations = new LocationStore();
        }
    }
}
