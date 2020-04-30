using System;
using System.Runtime.Serialization.Formatters;
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

    public static class GraphTestShared
    {
        public static string WCFEndpoint = "https://webdev.connectomes.utah.edu/RC1Test/Annotation/Annotate.svc";
        public static string ODataEndpoint = "https://webdev.connectomes.utah.edu/RC1Test/OData/";
        public static string ExportEndpoint = "https://webdev.connectomes.utah.edu/RC1Test/Export/";
        public static System.Net.NetworkCredential userCredentials;

        static GraphTestShared()
        {
            userCredentials = new System.Net.NetworkCredential("jamesan", "4%w%o06");
        }


        internal static double[] DistancesToDesmosomesForSubgraph(MorphologyGraph cell_graph)
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
                IList<ulong> path_to_synapse = MorphologyGraph.ShortestPath(cell_graph, desmosome.Node, (n) => n.NodeContainsStructureOfType(TypesToMatch));
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

            double[] distances = paths_for_desmosomes.Values.Select(p => MorphologyGraph.DistanceBetweenSubstructures(cell_graph, p.Path, p.SourceStructureID, p.TargetStructureID)).ToArray();

            return distances;
        }


        /// <summary>
        /// Test measuring distance along a process, or distance between two types of child graphs.
        /// </summary>
        public static void TestBranchAndTerminalProcessSelection(MorphologyGraph graph)
        {
            //Find all of the terminals
            SortedSet<ulong> branchIDs = new SortedSet<ulong>(graph.GetBranchPointIDs());
            Assert.IsTrue(branchIDs.Count > 0);
            SortedSet<ulong> terminalIDs = new SortedSet<ulong>(graph.GetTerminalIDs());
            Assert.IsTrue(terminalIDs.Count > 0);
            SortedSet<ulong> processIDs = new SortedSet<ulong>(graph.GetProcessIDs());
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

            List<ulong[]> processes = graph.Processes();
            Assert.IsTrue(processes.Count > 1);
            Assert.IsTrue(processes.Select(p => p.Length).Sum() >= processIDs.Count + terminalIDs.Count);
        }

        public static void TestMorphologyGraphBoundingBox(MorphologyGraph graph)
        {
            Geometry.GridBox bbox = graph.BoundingBox;

            //Ensure the bbox contains all of the centers of the morphology nodes
            GridVector3[] centers = graph.Nodes.Select(n => n.Value.Center).ToArray();
            GridBox node_center_bbox = GridBox.GetBoundingBox(centers);

            Assert.IsTrue(bbox.Contains(node_center_bbox));
        } 

        public static void SaveGraph(string Filename, MorphologyGraph graph)
        {

            using (System.IO.FileStream fileStream = System.IO.File.OpenWrite(Filename))
            {
                var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                formatter.Serialize(fileStream, graph);
                fileStream.Close();
            }
        }

        public static MorphologyGraph LoadGraph(string Filename)
        { 
            using (System.IO.FileStream fileStream = System.IO.File.OpenRead(Filename))
            {
                var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                MorphologyGraph graph = (MorphologyGraph)formatter.Deserialize(fileStream);
                fileStream.Close();
                return graph;
            }
        }
    } 
     
    /// <summary>
    /// Tests higher level operations on Morphology graphs
    /// </summary>
    [TestClass]
    public class MorphologyGraphTest
    {
        public MorphologyGraphTest()
        {
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
            
        }

        [TestMethod]
        public void TestSaveLoadGraph()
        {
            SharedGraph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 180 }, true, new Uri(GraphTestShared.ODataEndpoint));
            Assert.IsNotNull(SharedGraph);
            Assert.IsTrue(SharedGraph.Subgraphs.Count > 0);

            string SavedGraphFullPath = "C:\\Temp\\180.bin";

            GraphTestShared.SaveGraph(SavedGraphFullPath, SharedGraph);

            MorphologyGraph loadedGraph = GraphTestShared.LoadGraph(SavedGraphFullPath);

            Assert.IsNotNull(loadedGraph);
            Assert.Equals(SharedGraph.Nodes.Count, loadedGraph.Nodes.Count);
            Assert.Equals(SharedGraph.Edges.Count, loadedGraph.Edges.Count);

            foreach(MorphologyNode n in SharedGraph.Nodes.Values)
            {
                Assert.IsTrue(loadedGraph.Nodes.ContainsKey(n.Key));
                Assert.IsTrue(loadedGraph.Nodes[n.Key].Edges.Count == n.Edges.Count);
            }
        }
         
        [TestMethod]
        public void TestDistanceMeasurement()
        {
            SharedGraph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 180 }, true, new Uri(GraphTestShared.ODataEndpoint));
            Assert.IsNotNull(SharedGraph);
            Assert.IsTrue(SharedGraph.Subgraphs.Count > 0);

            SharedGraph.ConnectIsolatedSubgraphs();
            var subgraphs = MorphologyGraph.IsolatedSubgraphs(SharedGraph.Subgraphs.Values.First());
            Assert.IsTrue(subgraphs.Count == 1);

            MorphologyGraph cell_graph = SharedGraph.Subgraphs.Values.First();

            double[] distances = GraphTestShared.DistancesToDesmosomesForSubgraph(cell_graph);

            double avg_distance = distances.Average();
            double max_distance = distances.Max();
            Console.WriteLine("Avg distance to synapse component: {0}", avg_distance);
        }

        [TestMethod]
        public void TestBulkDistanceMeasurement()
        {
            var client = new Simple.OData.Client.ODataClient(GraphTestShared.ODataEndpoint);

            var T = client.FindEntriesAsync("Structures/ConnectomeODataV4.DistinctLabels");
            
            T.Wait();

            List<string> labels = new List<string>(T.Result.Count());

            foreach (IDictionary<string, object> dict in T.Result)
            {
                labels.AddRange(dict.Values.Select(v => v as string));
            }

            //string[] labels = T.Result;

            string OutputPath = "C:\\Temp\\DistanceToAdherensResults.m";

            if (System.IO.File.Exists(OutputPath))
                System.IO.File.Delete(OutputPath);

            string[] distinctLabels = labels.ToArray(); //.Distinct().ToArray();

            Dictionary<string, IDictionary<ulong, double[]>> LabelDict = new Dictionary<string, IDictionary<ulong, double[]>>();
            
            foreach (string label in distinctLabels)
            {
                if(label != null)
                    LabelDict[label] = BulkMeasureForLabel(label);
            }


        }

        public static void ConvertDictionaryToMatlab(IDictionary<string, IDictionary<ulong, double[]>> dict, string OutputFile)
        {
            
        }

        public static IDictionary<ulong, double[]> BulkMeasureForLabel(string Label)
        {
            Dictionary<ulong, double[]> distanceForLabel = new Dictionary<ulong, double[]>();
            if (Label == null)
                return distanceForLabel;

            string LowerLabel = Label.ToLower();

            ODataClient.ConnectomeODataV4.Container container = new ODataClient.ConnectomeODataV4.Container(new Uri(GraphTestShared.ODataEndpoint));
            //var IDsAndLabels = container.Structures.Select(s => new { ID = s.ID, Label = s.Label }).Where(s => s.Label.ToLower().Equals(Label.ToLower()));
            long[] IDs = container.Structures.Where(s => s.Label == Label).AsEnumerable().Select(s => s.ID).ToArray();
            
            MorphologyGraph graph = AnnotationVizLib.OData.ODataMorphologyFactory.FromOData(IDs, true, new Uri(GraphTestShared.ODataEndpoint));

            SortedSet<ulong> TargetTypes = new SortedSet<ulong>(new ulong[] { 85 }); //Adherens
            SortedSet<ulong> SourceTypes = new SortedSet<ulong>(new ulong[] { 28,34,35,73 });
            
            foreach (ulong TargetType in TargetTypes)
            {
                double[] Distances;
                SortedSet<ulong> T = new SortedSet<ulong>();
                T.Add(TargetType);
                Distances = MeasureDistances(graph, SourceTypes, TargetTypes);

                distanceForLabel[TargetType] = Distances;
            //    WriteDistanceResultsToMatlabFile(Label + "_T" + TargetType.ToString(), Distances, OutputPath);
            }

            return distanceForLabel;
        } 

        public static double[] MeasureDistances(MorphologyGraph graph, SortedSet<ulong> SourceTypes, SortedSet<ulong> TargetTypes)
        {
            List<double> accumulated_distances = new List<double>();
            foreach (MorphologyGraph cell_graph in graph.Subgraphs.Values)
            {
                double[] distances = MorphologyGraph.DistancesBetweenSubgraphsByType(cell_graph, SourceTypes, TargetTypes).Select(p => p.Distance).ToArray();
                accumulated_distances.AddRange(distances);
            }

            return accumulated_distances.ToArray();
        }

        /// <summary>
        /// Create or append a matlab file defining a variable containing the distance array
        /// </summary>
        /// <param name="Variable"></param>
        /// <param name="distances"></param>
        /// <param name="Filename"></param>
        public static void WriteDistanceResultsToMatlabFile(string Variable, double[] distances, string Filename)
        {
            System.IO.File.AppendAllText(Filename, string.Format("\n{0} = {1};\n", Variable, distances.ToMatlab()));
        }

    }

    /// <summary>
    /// Summary description for MotifGraphTest
    /// </summary>
    [TestClass]
    public class ODataMorphologyGraphTest
    { 
        public ODataMorphologyGraphTest()
        { 
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
            SqlServerTypes.Utilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);

            SharedGraph = AnnotationVizLib.OData.ODataMorphologyFactory.FromOData(new long[] { 180 }, true, new Uri(GraphTestShared.ODataEndpoint));
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
        public void GenerateODataMorphologyGraph()
        {
            StructureMorphologyColorMap colormap = AnnotationVizLibTests.TestUtils.LoadColorMap("Resources/ExportColorMapping");

            Assert.IsTrue(SharedGraph.Subgraphs.First().Value.Subgraphs.Count > 0);

            MorphologyTLPView tlpGraph = AnnotationVizLib.MorphologyTLPView.ToTLP(SharedGraph, DefaultScale(), colormap, GraphTestShared.ExportEndpoint);
            
            string TLPFileFullPath = "C:\\Temp\\180_OData.tlp";

            tlpGraph.SaveTLP(TLPFileFullPath);
        }
        
        /// <summary>
        /// Test measuring distance along a process, or distance between two types of child graphs.
        /// </summary>
        [TestMethod]
        public void TestBranchTerminalProcessSelection()
        {
            GraphTestShared.TestBranchAndTerminalProcessSelection(SharedGraph.Subgraphs.First().Value);
        }

        [TestMethod]
        public void TestMorphologyGraphBoundingBox()
        {
            GraphTestShared.TestMorphologyGraphBoundingBox(SharedGraph.Subgraphs.First().Value);
        }

        [TestMethod]
        public void TestStickFigureMorphologyGraph()
        {
            StructureMorphologyColorMap colormap = AnnotationVizLibTests.TestUtils.LoadColorMap("Resources/ExportColorMapping");

            MorphologyGraph graph = AnnotationVizLib.OData.ODataMorphologyFactory.FromOData(new long[] { 180 }, true, new Uri(GraphTestShared.ODataEndpoint));
            Assert.IsNotNull(graph);

            graph.ConnectIsolatedSubgraphs();
            graph.ToStickFigure();

            MorphologyTLPView tlpGraph = AnnotationVizLib.MorphologyTLPView.ToTLP(graph, DefaultScale(), colormap, GraphTestShared.ExportEndpoint);

            string TLPFileFullPath = "C:\\Temp\\Stick180_OData.tlp";

            tlpGraph.SaveTLP(TLPFileFullPath);

            //Ensure the graph only has endpoints and branches
            SortedSet<ulong> branchIDs = new SortedSet<ulong>(graph.Subgraphs.First().Value.GetBranchPointIDs());
            SortedSet<ulong> terminalIDs = new SortedSet<ulong>(graph.Subgraphs.First().Value.GetTerminalIDs());
            SortedSet<ulong> processIDs = new SortedSet<ulong>(graph.Subgraphs.First().Value.GetProcessIDs());
            Assert.IsTrue(terminalIDs.Count + branchIDs.Count == graph.Subgraphs.First().Value.Nodes.Count);
            Assert.IsTrue(processIDs.Count  == 0);
        }
         
        
    }
     
    /// <summary>
    /// Summary description for MotifGraphTest
    /// </summary>
    [TestClass]
    public class SimpleODataMorphologyGraphTest
    {
        public SimpleODataMorphologyGraphTest()
        {
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

            SharedGraph = AnnotationVizLib.OData.ODataMorphologyFactory.FromOData(new long[] { 180 }, true, new Uri(GraphTestShared.ODataEndpoint));
            Assert.IsNotNull(SharedGraph);
            Assert.IsTrue(SharedGraph.Subgraphs.Count > 0);

            SharedGraph.ConnectIsolatedSubgraphs();
            var subgraphs = MorphologyGraph.IsolatedSubgraphs(SharedGraph.Subgraphs.Values.First());
            Assert.IsTrue(subgraphs.Count == 1);
             
            Assert.IsTrue(SharedGraph.Subgraphs.First().Value.Subgraphs.Count > 0);

        }
         
        [TestMethod]
        public void GenerateSimpleODataMorphologyGraph()
        {
            StructureMorphologyColorMap colormap = AnnotationVizLibTests.TestUtils.LoadColorMap("Resources/ExportColorMapping");
             
            MorphologyTLPView tlpGraph = AnnotationVizLib.MorphologyTLPView.ToTLP(SharedGraph, SharedGraph.scale, colormap, GraphTestShared.ExportEndpoint);

            string TLPFileFullPath = "C:\\Temp\\180_SimpleOData.tlp";

            tlpGraph.SaveTLP(TLPFileFullPath);
        }

        /// <summary>
        /// Test measuring distance along a process, or distance between two types of child graphs.
        /// </summary>
        [TestMethod]
        public void TestBranchTerminalProcessSelection()
        {
            GraphTestShared.TestBranchAndTerminalProcessSelection(SharedGraph.Subgraphs.First().Value);
        }

        [TestMethod]
        public void TestMorphologyGraphBoundingBox()
        {
            GraphTestShared.TestMorphologyGraphBoundingBox(SharedGraph.Subgraphs.First().Value);
        }

        /*
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

            MorphologyGraph graph = ODataMorphologyFactory.FromOData(new long[] { 180 }, true, new Uri(GraphTestShared.ODataEndpoint));
            Assert.IsNotNull(graph);

            graph.ConnectIsolatedSubgraphs();
            graph.ToStickFigure();

            MorphologyTLPView tlpGraph = AnnotationVizLib.MorphologyTLPView.ToTLP(graph, DefaultScale(), colormap, GraphTestShared.ExportEndpoint);

            string TLPFileFullPath = "C:\\Temp\\Stick180.tlp";

            tlpGraph.SaveTLP(TLPFileFullPath);
        }


        [TestMethod]
        public void TestDistanceMeasurement()
        {
            MorphologyGraph cell_graph = SharedGraph.Subgraphs.Values.First();

            double[] distances = GraphTestShared.DistancesToDesmosomesForSubgraph(cell_graph);

            double avg_distance = distances.Average();
            double max_distance = distances.Max();
            Console.WriteLine("Avg distance to synapse component: {0}", avg_distance);
        }

        [TestMethod]
        public void TestBulkDistanceMeasurement()
        {
            ODataClient.ConnectomeODataV4.Container container = new ODataClient.ConnectomeODataV4.Container(new Uri(GraphTestShared.ODataEndpoint));

            Structure[] cells = container.Structures.Where(s => s.Label.ToLower().Contains("CBb5")).ToArray();
            long[] IDs = cells.Select(s => s.ID).ToArray();

            MorphologyGraph graph = ODataMorphologyFactory.FromOData(IDs, true, new Uri(GraphTestShared.ODataEndpoint));

            List<double> accumulated_distances = new List<double>();
            foreach (MorphologyGraph cell_graph in graph.Subgraphs.Values)
            {
                double[] distances = GraphTestShared.DistancesToDesmosomesForSubgraph(cell_graph);
                accumulated_distances.AddRange(distances);
            }

            double avg_distance = accumulated_distances.Average();
            double max_distance = accumulated_distances.Max();
            Console.WriteLine("Avg distance to synapse component: {0}", avg_distance);
        }
        */
    }

    /// <summary>
    /// Summary description for MotifGraphTest
    /// </summary>
    [TestClass]
    public class WCFMorphologyGraphTest
    {
        public WCFMorphologyGraphTest()
        {
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

            SharedGraph = AnnotationVizLib.WCFClient.WCFMorphologyFactory.FromWCF(new long[] { 180 }, true, GraphTestShared.WCFEndpoint, GraphTestShared.userCredentials);
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

            MorphologyTLPView tlpGraph = AnnotationVizLib.MorphologyTLPView.ToTLP(SharedGraph, DefaultScale(), colormap, GraphTestShared.ExportEndpoint);

            string TLPFileFullPath = "C:\\Temp\\180_WCF.tlp";

            tlpGraph.SaveTLP(TLPFileFullPath);
        }

        /// <summary>
        /// Test measuring distance along a process, or distance between two types of child graphs.
        /// </summary>
        [TestMethod]
        public void TestBranchTerminalProcessSelection()
        {
            GraphTestShared.TestBranchAndTerminalProcessSelection(SharedGraph.Subgraphs.First().Value);
        }

        [TestMethod]
        public void TestMorphologyGraphBoundingBox()
        {
            GraphTestShared.TestMorphologyGraphBoundingBox(SharedGraph.Subgraphs.First().Value);
        }



    }


}
