using System;
using System.Threading;
using System.Threading.Tasks;
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

        /// <summary>
        /// Warm static/slow-changing stores (structure types, permitted links, …) after the endpoint is known.
        /// </summary>
        Task InitializeAsync(CancellationToken token = default);
    }

    /// <summary>
    /// Static access point for the annotation stores used throughout the UI (Converters, Forms, Controls,
    /// ViewModels that can't easily accept constructor-injected dependencies).
    ///
    /// The composition root (currently the gRPC client bootstrap in WebAnnotationModel.gRPC) is responsible
    /// for building the concrete, DI-composed store instances and calling <see cref="Initialize(IAnnotationStores)"/>
    /// once at application startup, before any UI code touches Store.X.
    /// </summary>
    public static class Store
    {
        private static IAnnotationStores _current;

        /// <summary>
        /// True once <see cref="Initialize(IAnnotationStores)"/> has been called.
        /// </summary>
        public static bool IsInitialized => _current != null;

        /// <summary>
        /// Called once by the composition root at application startup with the fully constructed,
        /// gRPC-backed store implementations.
        /// </summary>
        public static void Initialize(IAnnotationStores stores)
        {
            _current = stores ?? throw new ArgumentNullException(nameof(stores));
            stores.InitializeAsync().GetAwaiter().GetResult();
        }

        private static IAnnotationStores Current =>
            _current ?? throw new InvalidOperationException(
                "WebAnnotationModel.Store has not been initialized. The application's composition root must call Store.Initialize(...) with the gRPC-backed stores before any UI code accesses Store.X.");

        public static ILocationStore Locations => Current.Locations;

        public static IStructureStore Structures => Current.Structures;

        public static IStructureTypeStore StructureTypes => Current.StructureTypes;

        public static IStructureLinkStore StructureLinks => Current.StructureLinks;

        public static ILocationLinkStore LocationLinks => Current.LocationLinks;

        public static IPermittedStructureLinkStore PermittedStructureLinks => Current.PermittedStructureLinks;

        public static IRegionLoader<LocationObj> LocationsByRegion => Current.LocationsByRegion;
    }
}
