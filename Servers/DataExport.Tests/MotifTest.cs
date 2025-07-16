using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationVizLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Specialized;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DataExport.Tests
{
    [TestClass]
    public class MotifTest
    {
        [TestInitialize]
        public void TestInit()
        { 
        }

        private MotifGraph CreateMotifGraph()
        {
            MotifGraph graph = new MotifGraph();

            MotifNode BC_node = new MotifNode("BC", new IStructureReadOnly[0]);
            MotifNode ACII_node = new MotifNode("ACII", new IStructureReadOnly[0]);
            MotifNode ACI_node = new MotifNode("ACI", new IStructureReadOnly[0]);
            MotifNode GC_node = new MotifNode("GC", new IStructureReadOnly[0]);

            MotifEdge BC_GC_edge = new MotifEdge("BC", "GC", "RIBBON SYNAPSE");
            MotifEdge BC_AC_edge = new MotifEdge("BC", "ACII", "RIBBON SYNAPSE");
            MotifEdge ACII_ACI_edge = new MotifEdge("ACII", "ACI", "CONVENTIONAL");
            MotifEdge ACII_BC_edge = new MotifEdge("ACII", "BC", "GAP JUNCTION");
            MotifEdge AC_GC_edge = new MotifEdge("ACI", "GC", "CONVENTIONAL");

            graph.AddNode(BC_node);
            graph.AddNode(ACI_node);
            graph.AddNode(ACII_node);
            graph.AddNode(GC_node);

            graph.AddEdge(BC_GC_edge);
            graph.AddEdge(BC_AC_edge);
            graph.AddEdge(ACII_ACI_edge);
            graph.AddEdge(ACII_BC_edge);
            graph.AddEdge(AC_GC_edge);

            return graph;
        }

        [TestMethod]
        public void TestMotifGraphs()
        { 
            MotifGraph motifGraph = CreateMotifGraph();
            MotifTLPView TlpGraph = MotifTLPView.ToTLP(motifGraph, "http://localhost/");
            TlpGraph.SaveTLP("C:\\Temp\\motif.tlp");
        }

        [TestMethod]
        public async void TestMorphologyGraphs()
        {
            NameValueCollection queryParams = new NameValueCollection
            {
                { "id", "180;476" }
            };

            // Create mocks
            var mockedHttpContext = new DefaultHttpContext();
            var mockedHttpRequest = mockedHttpContext.Request;
            mockedHttpRequest.QueryString = new QueryString("?id=180;476");
             
            // Provide a mock IWebHostEnvironment
            var mockEnv = new Moq.Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            DataExport.Controllers.MorphologyController controller = new Controllers.MorphologyController(mockEnv.Object);
            controller.ControllerContext = new ControllerContext()
            {
                HttpContext = mockedHttpContext
            };
            
            IActionResult result = await controller.GetTLP();
            // Check for FileResult instead of FilePathResult
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(result is FileResult);

        }
    }
}
