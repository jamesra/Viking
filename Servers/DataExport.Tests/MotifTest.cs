using Annotation.Interfaces;
using AnnotationVizLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Specialized;
using System.Web;
using System.Web.Mvc;

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

            MotifNode BC_node = new MotifNode("BC", new IStructure[0]);
            MotifNode ACII_node = new MotifNode("ACII", new IStructure[0]);
            MotifNode ACI_node = new MotifNode("ACI", new IStructure[0]);
            MotifNode GC_node = new MotifNode("GC", new IStructure[0]);

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
            NameValueCollection queryParams = new NameValueCollection();
            queryParams.Add("id", "180;476");

            // Create mocks
            var mockedhttpContext = new Mock<HttpContextBase>();
            var mockedHttpRequest = new Mock<HttpRequestBase>();

            mockedHttpRequest.SetupGet(x => x.QueryString).Returns(queryParams);
            mockedhttpContext.SetupGet(x => x.Request).Returns(mockedHttpRequest.Object);
             
            HttpContext.Current = new HttpContext(new HttpRequest("", "http://tempuri.org", "id=180;476"), new HttpResponse(new System.IO.StringWriter()));
            DataExport.Controllers.MorphologyController controller = new Controllers.MorphologyController();
            controller.ControllerContext = new ControllerContext(mockedhttpContext.Object, new System.Web.Routing.RouteData(), controller);
            
            ActionResult result = await controller.GetTLP();
            Assert.IsTrue(result.GetType() == typeof(FilePathResult));

        }
    }
}
