using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests
{
    /// <summary>
    /// Ensures store eviction can clear warm region cells so a return visit re-fetches.
    /// </summary>
    [TestClass]
    public class RegionLoaderInvalidateSectionTests
    {
        private sealed class StubRegionQuery : IRegionQuery<long, LocationObj>
        {
            public ICollection<LocationObj> GetLocalObjectsInRegion(long SectionNumber, Rectangle bounds, double MinRadius) =>
                [];

            public ICollection<LocationObj> GetObjectsInRegion(
                long SectionNumber,
                Rectangle bounds,
                double MinRadius,
                System.DateTime? LastQueryUtc) =>
                [];

            public MixedLocalAndRemoteQueryResults<long, LocationObj> GetObjectsInRegionAsync(
                long SectionNumber,
                Rectangle bounds,
                double MinRadius,
                System.DateTime? LastQueryUtc,
                System.Action<ICollection<LocationObj>> OnLoadedCallback,
                CancellationToken token = default) =>
                new(null, []);
        }

        [TestMethod]
        public void InvalidateSection_ClearsWarmPyramidSoAreRegionQueriesCompleteIsFalse()
        {
            var loader = new RegionLoader<long, LocationObj>(new StubRegionQuery());
            Rectangle bounds = new(0, 0, 100, 100);

            // Force a pyramid entry into existence, then invalidate.
            loader.LoadSectionAnnotationsInRegion(bounds, 1.0, 42, _ => { }, _ => { }, CancellationToken.None);
            loader.InvalidateSection(42);

            Assert.IsFalse(loader.AreRegionQueriesComplete(bounds, 1.0, 42));
        }
    }
}
