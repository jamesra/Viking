using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AnnotationVizLib;

namespace AnnotationVizLibTests
{
    [TestClass]
    public class NeuronGraphTest
    {
        public static string WCFEndpoint = "https://webdev.connectomes.utah.edu/RC1Test/Annotation/Annotate.svc";
        public static string ODataEndpoint = "https://webdev.connectomes.utah.edu/RC1Test/OData/";
        public static string ExportEndpoint = "https://webdev.connectomes.utah.edu/RC1Test/Export/";
        private System.Net.NetworkCredential userCredentials;

        public NeuronGraphTest()
        {
            userCredentials = new System.Net.NetworkCredential("jamesan", "4%w%o06");
        }

        [TestMethod]
        public void GenerateWCFNeuronGraph()
        {
            AnnotationVizLib.NeuronGraph graph = WCFNeuronFactory.BuildGraph(new long[] {476}, 2, WCFEndpoint, this.userCredentials);

            System.Diagnostics.Debug.Assert(graph != null);

            string JSONPath = "C:\\Temp\\WCFNeuron476.json";

            NeuronJSONView JSONView = NeuronJSONView.ToJSON(graph);
            string JSON = JSONView.ToString(); 
            JSONView.SaveJSON(JSONPath);

            NeuronGMLView gmlGraph = AnnotationVizLib.NeuronGMLView.ToGML(graph, "", true);

            string gmlPath = "C:\\Temp\\WCFNeuron476.gml";
            gmlGraph.SaveGML(gmlPath);

            NeuronDOTView dotGraph = AnnotationVizLib.NeuronDOTView.ToDOT(graph, true);

            string dotPath = "C:\\Temp\\WCFNeuron476.dot";
            dotGraph.SaveDOT(dotPath);

            //string[] Types = new string[] {"svg"};
            //NeuronDOTView.Convert("dot", dotPath, Types);

            string tlpPath = "C:\\Temp\\WCFNeuron476.tlp";

            NeuronTLPView tlpGraph = AnnotationVizLib.NeuronTLPView.ToTLP(graph, ODataEndpoint, true);
            tlpGraph.SaveTLP(tlpPath);
        }

        [TestMethod]
        public void GenerateHugeWCFNeuronGraph()
        {
            AnnotationVizLib.NeuronGraph graph = WCFNeuronFactory.BuildGraph(new long[] { 476 }, 9, WCFEndpoint, this.userCredentials);

            System.Diagnostics.Debug.Assert(graph != null);

            string JSONPath = "C:\\Temp\\NeuronWCF476_9Hops.json";

            NeuronJSONView JSONView = NeuronJSONView.ToJSON(graph);
            string JSON = JSONView.ToString();
            JSONView.SaveJSON(JSONPath);

            NeuronGMLView gmlGraph = AnnotationVizLib.NeuronGMLView.ToGML(graph, "", true);

            string gmlPath = "C:\\Temp\\NeuronWCF476_9Hops.gml";
            gmlGraph.SaveGML(gmlPath);

            NeuronDOTView dotGraph = AnnotationVizLib.NeuronDOTView.ToDOT(graph, true);

            string dotPath = "C:\\Temp\\NeuronWCF476_9Hops.dot";
            dotGraph.SaveDOT(dotPath);
             
            string tlpPath = "C:\\Temp\\NeuronWCF476_9Hops.tlp";

            NeuronTLPView tlpGraph = AnnotationVizLib.NeuronTLPView.ToTLP(graph, ODataEndpoint, true);
            tlpGraph.SaveTLP(tlpPath);
        }

        [TestMethod]
        public void GenerateODataNeuronGraph()
        {
            AnnotationVizLib.NeuronGraph graph = ODataNeuronFactory.FromOData(new long[] { 180, 476 }, 2, new Uri(ODataEndpoint));

            System.Diagnostics.Debug.Assert(graph != null);

            string JSONPath = "C:\\Temp\\NeuronOData476.json";

            NeuronJSONView JSONView = NeuronJSONView.ToJSON(graph);
            string JSON = JSONView.ToString();
            JSONView.SaveJSON(JSONPath);

            NeuronGMLView gmlGraph = AnnotationVizLib.NeuronGMLView.ToGML(graph, "", true);

            string gmlPath = "C:\\Temp\\NeuronOData476.gml";
            gmlGraph.SaveGML(gmlPath);

            NeuronDOTView dotGraph = AnnotationVizLib.NeuronDOTView.ToDOT(graph, true);

            string dotPath = "C:\\Temp\\NeuronOData476.dot";
            dotGraph.SaveDOT(dotPath);

            string[] Types = new string[] { "svg" };

            NeuronDOTView.Convert("dot", dotPath, Types);

            string tlpPath = "C:\\Temp\\NeuronOData476.tlp";

            NeuronTLPView tlpGraph = AnnotationVizLib.NeuronTLPView.ToTLP(graph, ODataEndpoint, true);
            tlpGraph.SaveTLP(tlpPath);
        }

        [TestMethod]
        public void GenerateSimpleODataNeuronGraph()
        {
            AnnotationVizLib.NeuronGraph graph = SimpleODataNeuronFactory.FromOData(new long[] { 476 }, 9, new Uri(ODataEndpoint));

            System.Diagnostics.Debug.Assert(graph != null);

            string JSONPath = "C:\\Temp\\NeuronSimpleOData476.json";

            NeuronJSONView JSONView = NeuronJSONView.ToJSON(graph);
            string JSON = JSONView.ToString();
            JSONView.SaveJSON(JSONPath);
            
            string tlpPath = "C:\\Temp\\NeuronSimpleOData476.tlp";

            NeuronTLPView tlpGraph = AnnotationVizLib.NeuronTLPView.ToTLP(graph, ODataEndpoint, true);
            tlpGraph.SaveTLP(tlpPath);

            NeuronGMLView gmlGraph = AnnotationVizLib.NeuronGMLView.ToGML(graph, "", true);

            string gmlPath = "C:\\Temp\\NeuronSimpleOData476.gml";
            gmlGraph.SaveGML(gmlPath);

            NeuronDOTView dotGraph = AnnotationVizLib.NeuronDOTView.ToDOT(graph, true);

            string dotPath = "C:\\Temp\\NeuronSimpleOData476.dot";
            dotGraph.SaveDOT(dotPath);

            string[] Types = new string[] { "svg" };

            //NeuronDOTView.Convert("dot", dotPath, Types);
        }
    }
}
