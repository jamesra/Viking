using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AnnotationVizLib;
using GraphLib;
using Geometry;
using ODataClient;
using ODataClient.ConnectomeDataModel;

namespace AnnotationVizLibTests
{
    /// <summary>
    /// Summary description for MotifGraphTest
    /// </summary>
    [TestClass]
    public class MorphologyGraphTest
    {
        public static string WCFEndpoint = "https://webdev.connectomes.utah.edu/RC1Test/Annotation/Annotate.svc";
        public static string ODataEndpoint = "https://webdev.connectomes.utah.edu/RC1Test/OData/";
        public static string ExportEndpoint = "https://webdev.connectomes.utah.edu/RC1Test/Export/";
        private static System.Net.NetworkCredential userCredentials;

        public MorphologyGraphTest()
        {
            userCredentials = new System.Net.NetworkCredential("jamesan", "4%w%o06");
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        /// <summary>
        /// A Shared graph instance we can load once.  It should not be changed by tests
        /// </summary>
        static MorphologyGraph SharedGraph = null;
        // Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void InitializeSharedGraph(TestContext testContext)
        {
            
            SharedGraph = ODataMorphologyFactory.FromOData(new long[] { 180 }, true, new Uri(MorphologyGraphTest.ODataEndpoint));
            Assert.IsNotNull(SharedGraph);
            Assert.IsTrue(SharedGraph.Subgraphs.Count > 0);

            
            SharedGraph.ConnectIsolatedSubgraphs();
            var subgraphs = MorphologyGraph.IsolatedSubgraphs(SharedGraph.Subgraphs.Values.First());
            Assert.IsTrue(subgraphs.Count == 1);
            
        }

        public static Geometry.Scale DefaultScale()
        {
            return new Geometry.Scale(new Geometry.AxisUnits(2.18, "nm"),
                                      new Geometry.AxisUnits(2.18, "nm"),
                                      new Geometry.AxisUnits(90, "nm"));
        }

        [TestMethod]
        public void GenerateWCFMorphologyGraph()
        {
            StructureMorphologyColorMap colormap = AnnotationVizLibTests.TestUtils.LoadColorMap("Resources/ExportColorMapping");

            AnnotationVizLib.MorphologyGraph graph = WCFMorphologyFactory.FromWCF(new long[] { 180 }, true, WCFEndpoint, userCredentials);
            Assert.IsNotNull(graph);
            Assert.IsTrue(graph.Subgraphs.Count > 0);

            graph.ConnectIsolatedSubgraphs();

            var subgraphs = MorphologyGraph.IsolatedSubgraphs(graph.Subgraphs.Values.First());
            Assert.IsTrue(subgraphs.Count == 1);

            MorphologyTLPView tlpGraph = AnnotationVizLib.MorphologyTLPView.ToTLP(graph, DefaultScale(), colormap, ExportEndpoint);

            string TLPFileFullPath = "C:\\Temp\\180_WCF.tlp";

            tlpGraph.SaveTLP(TLPFileFullPath);
            //dotGraph.SaveDOT(DotFileFullPath);

            //string[] Types = new string[] { "svg" };

            //MotifDOTView.Convert("dot", DotFileFullPath, Types);
        }

        [TestMethod]
        public void GenerateODataMorphologyGraph()
        {
            StructureMorphologyColorMap colormap = AnnotationVizLibTests.TestUtils.LoadColorMap("Resources/ExportColorMapping");

            Assert.IsTrue(SharedGraph.Subgraphs.First().Value.Subgraphs.Count > 0);

            MorphologyTLPView tlpGraph = AnnotationVizLib.MorphologyTLPView.ToTLP(SharedGraph, DefaultScale(), colormap, ExportEndpoint);
            
            string TLPFileFullPath = "C:\\Temp\\180_OData.tlp";

            tlpGraph.SaveTLP(TLPFileFullPath);
        }

        [TestMethod]
        public void TestMorphologyGraphBoundingBox()
        {
            Geometry.GridBox bbox = SharedGraph.BoundingBox;

            //Ensure the bbox contains all of the centers of the morphology nodes
            GridVector3[] centers = SharedGraph.Subgraphs.First().Value.Nodes.Select(n => n.Value.Center).ToArray();
            GridBox node_center_bbox = GridBox.GetBoundingBox(centers);

            Assert.IsTrue(bbox.Contains(node_center_bbox));
        }

        /// <summary>
        /// Test measuring distance along a process, or distance between two types of child graphs.
        /// </summary>
        [TestMethod]
        public void TestBrandTerminalProcessSelection()
        {
            //Find all of the terminals
            SortedSet<ulong> branchIDs = new SortedSet<ulong>(SharedGraph.Subgraphs.First().Value.GetBranchPoints());
            Assert.IsTrue(branchIDs.Count > 0);
            SortedSet<ulong> terminalIDs = new SortedSet<ulong>(SharedGraph.Subgraphs.First().Value.GetTerminals());
            Assert.IsTrue(terminalIDs.Count > 0);
            SortedSet<ulong> processIDs = new SortedSet<ulong>(SharedGraph.Subgraphs.First().Value.GetProcess());
            Assert.IsTrue(processIDs.Count > 0);

            SortedSet<ulong> intersection = new SortedSet<ulong>(branchIDs.Intersect(terminalIDs));
            Assert.IsTrue(intersection.Count == 0);
            intersection = new SortedSet<ulong>(terminalIDs.Intersect(branchIDs));
            Assert.IsTrue(intersection.Count == 0);

            intersection = new SortedSet<ulong>(terminalIDs.Intersect(processIDs));
            Assert.IsTrue(intersection.Count == 0);
            intersection = new SortedSet<ulong>(processIDs.Intersect(terminalIDs));
            Assert.IsTrue(intersection.Count == 0);

            intersection = new SortedSet<ulong>(branchIDs.Intersect(processIDs));
            Assert.IsTrue(intersection.Count == 0);
            intersection = new SortedSet<ulong>(processIDs.Intersect(branchIDs));
            Assert.IsTrue(intersection.Count == 0);
        }

        [TestMethod]
        public void TestStickFigureMorphologyGraph()
        {
            StructureMorphologyColorMap colormap = AnnotationVizLibTests.TestUtils.LoadColorMap("Resources/ExportColorMapping");

            MorphologyGraph graph = ODataMorphologyFactory.FromOData(new long[] { 180 }, true, new Uri(MorphologyGraphTest.ODataEndpoint));
            Assert.IsNotNull(graph);

            graph.ConnectIsolatedSubgraphs();
            graph.ToStickFigure();

            MorphologyTLPView tlpGraph = AnnotationVizLib.MorphologyTLPView.ToTLP(graph, DefaultScale(), colormap, ExportEndpoint);

            string TLPFileFullPath = "C:\\Temp\\Stick180.tlp";

            tlpGraph.SaveTLP(TLPFileFullPath);
        }

        private bool NodeContainsStructureOfType(MorphologyNode node, SortedSet<ulong> TypeIDs)
        {
            return node.Subgraphs.Where(n => TypeIDs.Contains(n.structureType.ID)).Any();
        }

        [TestMethod]
        public void TestDistanceMeasurement()
        {
            MorphologyGraph cell_graph = SharedGraph.Subgraphs.Values.First();

            double[] distances = DistancesToDesmosomesForSubgraph(cell_graph);
 
            double avg_distance = distances.Average();
            double max_distance = distances.Max();
            Console.WriteLine("Avg distance to synapse component: {0}", avg_distance); 
        }

        [TestMethod]
        public void TestBulkDistanceMeasurement()
        {
            ODataClient.ConnectomeODataV4.Container container = new ODataClient.ConnectomeODataV4.Container(new Uri(MorphologyGraphTest.ODataEndpoint));

            Structure[] cells = container.Structures.Where(s => s.Label.ToLower().Contains("CBb5")).ToArray();
            long[] IDs = cells.Select(s => s.ID).ToArray();

            MorphologyGraph graph = ODataMorphologyFactory.FromOData(IDs, true, new Uri(MorphologyGraphTest.ODataEndpoint));

            List<double> accumulated_distances = new List<double>();
            foreach (MorphologyGraph cell_graph in graph.Subgraphs.Values)
            {
                double[] distances = DistancesToDesmosomesForSubgraph(cell_graph);
                accumulated_distances.AddRange(distances);
            }

            double avg_distance = accumulated_distances.Average();
            double max_distance = accumulated_distances.Max();
            Console.WriteLine("Avg distance to synapse component: {0}", avg_distance);
        }

        /// <summary>
        /// The distance between two substructures in a cell
        /// </summary>
        /// <param name="path_between"></param>
        /// <param name="SourceStructureID"></param>
        /// <param name="TargetStructureID"></param>
        /// <returns></returns>
        private double DistanceBetweenSubstructures(MorphologyGraph graph, IList<ulong> path_between, ulong SourceStructureID, ulong TargetStructureID)
        {
            if(path_between.Count <= 2)
            {
                //Measure the direct distance between the structures because there is a direct line between the two
                MorphologyGraph source = graph.Subgraphs[SourceStructureID];
                MorphologyGraph target = graph.Subgraphs[TargetStructureID];

                return MorphologyGraph.GraphDistance(source, target);
            }

            double path_distance = graph.PathLength(path_between);

            double SourceToPathDistance;
            ulong nearest_node_to_source = graph.NearestNode(graph.Subgraphs[SourceStructureID], out SourceToPathDistance);
            double TargetToPathDistance;
            ulong nearest_node_to_target = graph.NearestNode(graph.Subgraphs[TargetStructureID], out TargetToPathDistance);
            
            return path_distance + SourceToPathDistance + TargetToPathDistance;
        }

        private double[] DistancesToDesmosomesForSubgraph(MorphologyGraph cell_graph)
        {
            List<ulong> desmosome_ids = cell_graph.Subgraphs.Where(sg => sg.Value.structureType.Name.ToLower().Contains("adherens")).Select(sg => sg.Key).ToList();
            //Assert.IsTrue(desmosome_ids.Count > 0);
            if (desmosome_ids.Count == 0)
                return new double[0];

            var nodes_with_desmosome_subgraphs = desmosome_ids.Select(id => new { Node = cell_graph.NearestNodeToSubgraph[id], StructureID = id }).ToList();
            Assert.IsTrue(nodes_with_desmosome_subgraphs.Count > 0);

            SortedSet<ulong> TypesToMatch = new SortedSet<ulong>(new ulong[] { 34, 35, 73 });

            SortedDictionary<ulong, PathData> paths_for_desmosomes = new SortedDictionary<ulong, PathData>();

            //Find the nearest synapse
            foreach (var desmosome in nodes_with_desmosome_subgraphs)
            {
                IList<ulong> path_to_synapse = MorphologyGraph.Path(cell_graph, desmosome.Node, (n) => NodeContainsStructureOfType(n, TypesToMatch));
                if (path_to_synapse == null)
                    continue;

                //Find the substructure on the final node of the path
                MorphologyNode destination = cell_graph.Nodes[path_to_synapse.Last()];
                ulong TargetStructureID = destination.Subgraphs.Where(s => TypesToMatch.Contains(s.structureType.ID)).Select(s => s.StructureID).First();

                paths_for_desmosomes[desmosome.Node] = new PathData
                {
                    Path = path_to_synapse,
                    SourceStructureID = desmosome.StructureID,
                    TargetStructureID = TargetStructureID
                };
            }
            
            int[] hops = paths_for_desmosomes.Select(p => p.Value.Path.Count).ToArray();
            double avg_hops = paths_for_desmosomes.Select(p => p.Value.Path.Count).Average();
            Console.WriteLine("Avg number of hops to synapse component: {0}", avg_hops);

            double[] distances = paths_for_desmosomes.Values.Select(p => DistanceBetweenSubstructures(cell_graph, p.Path, p.SourceStructureID, p.TargetStructureID)).ToArray();

            return distances;
        }
    }

    public struct PathData
    {
        public IList<ulong> Path;
        public ulong SourceStructureID;
        public ulong TargetStructureID;
    }
}
