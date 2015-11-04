using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AnnotationVizLib;

namespace AnnotationUtilsTests
{
    [TestClass]
    public class NeuronGraphTest
    {
        public string Endpoint = "https://connectomes.utah.edu/Services/RabbitBinary/Annotate.svc";
        private System.Net.NetworkCredential userCredentials;

        public NeuronGraphTest()
        {
            userCredentials = new System.Net.NetworkCredential("jamesan", "4%w%o06");
        }

        [TestMethod]
        public void GenerateNeuronGraph()
        {
           
            AnnotationVizLib.NeuronGraph graph = NeuronGraph.BuildGraph(new long[] {476}, 1, this.Endpoint, this.userCredentials);

            System.Diagnostics.Debug.Assert(graph != null);

            string JSONPath = "C:\\Temp\\Neuron476.json";

            NeuronJSONView JSONView = NeuronJSONView.ToJSON(graph);
            string JSON = JSONView.ToString(); 
            JSONView.SaveJSON(JSONPath);

            NeuronGMLView gmlGraph = AnnotationVizLib.NeuronGMLView.ToGML(graph, "", true);

            string gmlPath = "C:\\Temp\\Neuron476.gml";
            gmlGraph.SaveGML(gmlPath);

            NeuronDOTView dotGraph = AnnotationVizLib.NeuronDOTView.ToDOT(graph, true);

            string dotPath = "C:\\Temp\\Neuron476.dot";
            dotGraph.SaveDOT(dotPath);

            string[] Types = new string[] {"svg"};

            NeuronDOTView.Convert("dot", dotPath, Types);
             
        }
    }
}
