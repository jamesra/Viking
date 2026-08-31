using AnnotationVizLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Guards against silent truncation of paged OData collections.
    ///
    /// The service caps an $expand'ed collection at 2048 entries and reports the rest as a nested nextLink. A client
    /// that materializes only the first page loses annotations without any error, which previously left large cells
    /// with holes in their membrane. These counts come from querying RC1 directly.
    /// </summary>
    [TestClass]
    public class ODataPagingTests
    {
        private const long CellStructureId = 476;

        /// <summary>Locations reported by Structures(476)/Locations, which the service returns unpaged.</summary>
        private const int ExpectedLocationCount = 3161;

        private static readonly Uri Endpoint = new("http://websvc.codepharm.net/RC1/OData");

        [TestMethod]
        [TestCategory("LiveData")]
        [Ignore("Depends on RC1 being reachable. Remove this attribute to re-run it.")]
        public async Task ExpandedLocationsAreNotTruncatedAtThePageBoundary()
        {
            MorphologyGraph root = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                new[] { CellStructureId }, false, Endpoint);

            MorphologyGraph cell = root.Subgraphs[(ulong)CellStructureId];
            Console.WriteLine($"Structure {CellStructureId}: {cell.Nodes.Count} nodes, {cell.Edges.Count} edges");

            Assert.AreEqual(ExpectedLocationCount, cell.Nodes.Count,
                "Expanded Locations were truncated; the nested nextLink was not followed.");
        }

        [TestMethod]
        [TestCategory("LiveData")]
        [Ignore("Depends on RC1 being reachable. Remove this attribute to re-run it.")]
        public async Task ChildStructuresLoadTheirOwnLocations()
        {
            MorphologyGraph root = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync(
                new[] { CellStructureId }, true, Endpoint);

            MorphologyGraph cell = root.Subgraphs[(ulong)CellStructureId];
            int childrenWithNodes = 0;
            foreach (var child in cell.Subgraphs.Values)
            {
                if (child.Nodes.Count > 0)
                    childrenWithNodes++;
            }

            Console.WriteLine($"Cell {CellStructureId}: {cell.Nodes.Count} nodes, {cell.Subgraphs.Count} children, {childrenWithNodes} with locations");
            Assert.AreEqual(ExpectedLocationCount, cell.Nodes.Count);
            Assert.AreEqual(cell.Subgraphs.Count, childrenWithNodes, "Some child structures loaded no locations.");
        }
    }
}
