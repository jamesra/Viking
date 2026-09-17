using AnnotationVizLib;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System.Linq;
using System.Threading.Tasks;
using UnitsAndScale;
using Viking.AnnotationServiceTypes.Interfaces;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Vasculature and similar structures can contain cycles in the morphology graph. SliceGraph must build slices
    /// without removing edges or stack-overflowing in cycle detection.
    /// </summary>
    [TestClass]
    public class SliceGraphCycleTests
    {
        const double SectionThickness = 90.0;

        static IScale TestScale => new Scale(new AxisUnits(1, "nm"), new AxisUnits(1, "nm"), new AxisUnits(SectionThickness, "nm"));

        static TestLocation PointLocation(ulong id, int section, double x, double y) =>
            new()
            {
                ID = id,
                ParentID = 1,
                UnscaledZ = section,
                Z = section * SectionThickness,
                TypeCode = LocationType.POINT,
                VolumeGeometryWKT = $"POINT ({x} {y})"
            };

        /// <summary>
        /// Loop 2-3-4-2 at increasing Z with a leaf at 1; no same-Z edges.
        /// </summary>
        static MorphologyGraph BuildBranchMergeCycleGraph()
        {
            MorphologyGraph graph = new(1, TestScale);
            graph.AddNode(new MorphologyNode(1, PointLocation(1, 1, 0, 0), graph));
            graph.AddNode(new MorphologyNode(2, PointLocation(2, 2, 0, 0), graph));
            graph.AddNode(new MorphologyNode(3, PointLocation(3, 3, 0, 0), graph));
            graph.AddNode(new MorphologyNode(4, PointLocation(4, 4, 0, 0), graph));

            graph.AddEdge(new MorphologyEdge(graph, 1, 2));
            graph.AddEdge(new MorphologyEdge(graph, 2, 3));
            graph.AddEdge(new MorphologyEdge(graph, 3, 4));
            graph.AddEdge(new MorphologyEdge(graph, 4, 2));

            return graph;
        }

        [TestMethod]
        [Timeout(30000)]
        public async Task SliceGraph_Create_PreservesEdgesOnMorphologyCycle()
        {
            MorphologyGraph graph = BuildBranchMergeCycleGraph();
            int edgeCountBefore = graph.Edges.Count;

            SliceGraph slices = await SliceGraph.Create(graph, 2.0);

            Assert.AreEqual(edgeCountBefore, graph.Edges.Count, "Morphology graph edges must not be removed for cycles.");
            Assert.IsTrue(slices.Nodes.Count > 0, "Expected at least one slice from the cyclic graph.");

            foreach (MorphologyEdge edge in graph.Edges.Values)
            {
                bool edgeUsedInSlice = slices.Nodes.Values.Any(slice => slice.InternalEdges.Contains(edge));
                Assert.IsTrue(edgeUsedInSlice, $"Edge {edge} should appear in at least one slice.");
            }
        }

        [TestMethod]
        [Timeout(30000)]
        public async Task SliceGraph_Create_HandlesSameSectionEdge()
        {
            MorphologyGraph graph = new(1, TestScale);
            graph.AddNode(new MorphologyNode(1, PointLocation(1, 2, 0, 0), graph));
            graph.AddNode(new MorphologyNode(2, PointLocation(2, 2, 10, 0), graph));
            graph.AddNode(new MorphologyNode(3, PointLocation(3, 1, 0, 0), graph));
            graph.AddEdge(new MorphologyEdge(graph, 1, 2));
            graph.AddEdge(new MorphologyEdge(graph, 3, 1));

            SliceGraph slices = await SliceGraph.Create(graph, 2.0);

            Assert.AreEqual(2, graph.Edges.Count);
            Assert.IsTrue(slices.Nodes.Count > 0, "Same-section edges should still produce slices.");
            Assert.IsTrue(slices.Nodes.Values.Any(s => s.InternalEdges.Contains(new MorphologyEdge(graph, 1, 2))));
        }
    }
}
