using AnnotationVizLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace AnnotationVizLibTests
{
    [TestClass]
    public class NeuronGraphTest
    {
        public static string ODataEndpoint = "https://webdev.connectomes.utah.edu/RC1Test/OData/";
        public static string ExportEndpoint = "https://webdev.connectomes.utah.edu/RC1Test/Export/";

        [TestMethod]
        public void GenerateODataNeuronGraph()
        {
            AnnotationVizLib.NeuronGraph graph = AnnotationVizLib.OData.ODataNeuronFactory.FromOData(new long[] { 180, 476 }, 2, new Uri(ODataEndpoint));

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

            string[] Types = ["svg"];

            NeuronDOTView.Convert("dot", dotPath, Types);

            string tlpPath = "C:\\Temp\\NeuronOData476.tlp";

            NeuronTLPView tlpGraph = AnnotationVizLib.NeuronTLPView.ToTLP(graph, ODataEndpoint, true);
            tlpGraph.SaveTLP(tlpPath);
        }

    }
}
