using System;

namespace WebAnnotationModel
{
    /// <summary>
    /// Static class that holds references to store singletons
    ///
    /// The initialization is super goofy and needs to be moved to dependency injection
    /// </summary>
    public class Store
    {
        public static void Init() => Nested.Init();

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
            private static bool Initialized = false;
            static Nested()
            {
                Init();

                RegionLocationsLoader = new RegionLoader<long, LocationObj>(Store.Locations);
                RegionStructuresLoader = new RegionLoader<long, StructureObj>(Store.Structures);
            }

            public static void Init()
            {
                if (Initialized)
                    return;

                Initialized = true;

                try
                {
                    StructureTypes.Init();
                    Structures.Init();
                    Locations.Init();
                    StructureLinks.Init();
                    LocationLinks.Init();
                    PermittedStructureLinks.Init();
                }
                catch (System.ServiceModel.Security.MessageSecurityException securityException)
                {
                    throw new Exception("It is possible the user password is incorrect", securityException);
                }
                catch (System.ServiceModel.Security.SecurityAccessDeniedException accessDeniedException)
                {
                    throw new Exception(
                        "Access to the Annotation Service was denied. For anonymous users, ensure you are logged in and that the Bearer token is set (Viking.Tokens.TokenInjector.BearerToken). For named users, ensure your account has the required permissions.",
                        accessDeniedException);
                }
                catch (System.ServiceModel.FaultException faultException)
                {
                    throw new Exception(
                        "It is possible there is no network connection or the user account is locked if an incorrect password was used repeatedly.  Contact an administrator to unlock the account.",
                        faultException);
                }
            }

            internal static readonly StructureTypeStore StructureTypes = [];
            internal static readonly StructureStore Structures = [];
            internal static readonly LocationStore Locations = [];
            internal static readonly StructureLinkStore StructureLinks = [];
            internal static readonly LocationLinkStore LocationLinks = [];
            internal static readonly PermittedStructureLinkStore PermittedStructureLinks = [];

            internal static readonly RegionLoader<long, LocationObj> RegionLocationsLoader;
            internal static readonly RegionLoader<long, StructureObj> RegionStructuresLoader;
        }
    }
}
