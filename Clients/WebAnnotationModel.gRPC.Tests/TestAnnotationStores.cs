using System.Threading;
using System.Threading.Tasks;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// <see cref="IAnnotationStores"/> over the stores a test builds, for code that reaches them through the static
    /// <see cref="Store"/>. Stores a test leaves null throw if touched.
    /// </summary>
    internal sealed class TestAnnotationStores : IAnnotationStores
    {
        public ILocationStore Locations { get; set; }
        public IStructureStore Structures { get; set; }
        public IStructureTypeStore StructureTypes { get; set; }
        public IStructureLinkStore StructureLinks { get; set; }
        public ILocationLinkStore LocationLinks { get; set; }
        public IPermittedStructureLinkStore PermittedStructureLinks { get; set; }
        public IRegionLoader<LocationObj> LocationsByRegion { get; set; }
        public Task InitializeAsync(CancellationToken token = default) => Task.CompletedTask;
    }
}
