using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AnnotationVizLib;
using GraphLib;
using Geometry;

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

            graph.ConnectIsolatedSubgraphs();

            var subgraphs = MorphologyGraph.IsolatedSubgraphs(graph.Subgraphs.Values.First());
            Assert.IsTrue(subgraphs.Count == 1);

            MorphologyTLPView tlpGraph = AnnotationVizLib.MorphologyTLPView.ToTLP(graph, DefaultScale(), colormap, ExportEndpoint);

            string TLPFileFullPath = "C:\\Temp\\180.tlp";

            tlpGraph.SaveTLP(TLPFileFullPath);
            //dotGraph.SaveDOT(DotFileFullPath);

            //string[] Types = new string[] { "svg" };

            //MotifDOTView.Convert("dot", DotFileFullPath, Types);
        }

        [TestMethod]
        public void GenerateODataMorphologyGraph()
        {
            StructureMorphologyColorMap colormap = AnnotationVizLibTests.TestUtils.LoadColorMap("Resources/ExportColorMapping");
            
            MorphologyTLPView tlpGraph = AnnotationVizLib.MorphologyTLPView.ToTLP(SharedGraph, DefaultScale(), colormap, ExportEndpoint);

            string TLPFileFullPath = "C:\\Temp\\180.tlp";

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
        public void TestDistanceMeasurements()
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
    }
}
