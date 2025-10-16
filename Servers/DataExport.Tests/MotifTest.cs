using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationVizLib;
using DataExport.Controllers;

namespace DataExport.Tests;

[TestClass]
public class MotifTest
{
    [TestInitialize]
    public void TestInit()
    {
    }

    private static MotifGraph CreateMotifGraph()
    {
        var graph = new MotifGraph();

        var bcNode = new MotifNode("BC", Array.Empty<IStructureReadOnly>());
        var aciiNode = new MotifNode("ACII", Array.Empty<IStructureReadOnly>());
        var aciNode = new MotifNode("ACI", Array.Empty<IStructureReadOnly>());
        var gcNode = new MotifNode("GC", Array.Empty<IStructureReadOnly>());

        var bcGcEdge = new MotifEdge("BC", "GC", "RIBBON SYNAPSE");
        var bcAcEdge = new MotifEdge("BC", "ACII", "RIBBON SYNAPSE");
        var aciiAciEdge = new MotifEdge("ACII", "ACI", "CONVENTIONAL");
        var aciiBcEdge = new MotifEdge("ACII", "BC", "GAP JUNCTION");
        var acGcEdge = new MotifEdge("ACI", "GC", "CONVENTIONAL");

        graph.AddNode(bcNode);
        graph.AddNode(aciNode);
        graph.AddNode(aciiNode);
        graph.AddNode(gcNode);

        graph.AddEdge(bcGcEdge);
        graph.AddEdge(bcAcEdge);
        graph.AddEdge(aciiAciEdge);
        graph.AddEdge(aciiBcEdge);
        graph.AddEdge(acGcEdge);

        return graph;
    }

    [TestMethod]
    public void TestMotifGraphs()
    {
        MotifGraph motifGraph = CreateMotifGraph();
        MotifTLPView tlpGraph = MotifTLPView.ToTLP(motifGraph, "http://localhost/");
        tlpGraph.SaveTLP("C:\\Temp\\motif.tlp");
    }

    [TestMethod]
    public async Task TestMorphologyGraphs()
    {
        // Create mocks
        var mockedHttpContext = new DefaultHttpContext();
        mockedHttpContext.Request.QueryString = new QueryString("?id=180;476");

        // Provide a mock IWebHostEnvironment
        var mockEnv = new Mock<IWebHostEnvironment>();
        var controller = new MorphologyController(mockEnv.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = mockedHttpContext
            }
        };

        IActionResult result = await controller.GetTLP();
        
        // Check for FileResult instead of FilePathResult
        Assert.IsTrue(result is FileResult);
    }
}
