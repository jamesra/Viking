using AnnotationVizLib;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace AnnotationVizLibTests
{
    /// <summary>
    /// Live RC1 check of the CurveFit LocationLink-length invariant
    /// (<see cref="MorphologyGraph.SumLocationLinkLengths"/>). A single hop can still grow when
    /// another incident link shrinks more; the parameterized family lives in
    /// <c>FSCheck/CurveFitLinkLengthSpec</c>. Neighbor residual-field correction is not involved.
    /// </summary>
    [TestClass]
    public class CurveFitOvercorrectionTests
    {
        private static readonly Uri Endpoint = new("http://websvc.codepharm.net/RC1/OData");

        /// <summary>
        /// BajajMultiTest RC1 410 selection: locations 200057 / 1420830. Raw hop is ~800 nm.
        /// CurveFit moves 200057 by ~1.5 µm along the 200043 shaft and grows the hop to ~2 µm.
        /// </summary>
        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(180000)]
        public async Task CurveFitProcesses_DoesNotStretch_RC1_200057_to_1420830()
        {
            const ulong lower = 200057;
            const ulong upper = 1420830;

            MorphologyGraph graph = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                [(long)lower, (long)upper], Endpoint, hops: 3);

            Assert.IsTrue(graph.Nodes.ContainsKey(lower));
            Assert.IsTrue(graph.Nodes.ContainsKey(upper));

            double hopBefore = (graph.Nodes[upper].Center.XY() - graph.Nodes[lower].Center.XY()).Magnitude;
            double linksBefore = MorphologyGraph.SumLocationLinkLengths(graph);
            Vector2 beforeLower = graph.Nodes[lower].Center.XY();

            MorphologyGraph.CurveFitProcesses(graph);

            double hopAfter = (graph.Nodes[upper].Center.XY() - graph.Nodes[lower].Center.XY()).Magnitude;
            double linksAfter = MorphologyGraph.SumLocationLinkLengths(graph);
            double offset = (graph.Nodes[lower].Center.XY() - beforeLower).Magnitude;

            Assert.IsTrue(linksAfter <= linksBefore + 1.0,
                $"CurveFit lengthened LocationLinks from {linksBefore:F1} to {linksAfter:F1} nm " +
                $"(hop {lower}->{upper} {hopBefore:F1}->{hopAfter:F1}, moved {lower} by {offset:F1} nm).");
        }
    }
}
