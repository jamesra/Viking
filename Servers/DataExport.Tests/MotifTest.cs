using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationVizLib;

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
        MotifGraph graph = new();

        MotifNode bcNode = new("BC", Array.Empty<IStructureReadOnly>());
        MotifNode aciiNode = new("ACII", Array.Empty<IStructureReadOnly>());
        MotifNode aciNode = new("ACI", Array.Empty<IStructureReadOnly>());
        MotifNode gcNode = new("GC", Array.Empty<IStructureReadOnly>());

        MotifEdge bcGcEdge = new("BC", "GC", "RIBBON SYNAPSE");
        MotifEdge bcAcEdge = new("BC", "ACII", "RIBBON SYNAPSE");
        MotifEdge aciiAciEdge = new("ACII", "ACI", "CONVENTIONAL");
        MotifEdge aciiBcEdge = new("ACII", "BC", "GAP JUNCTION");
        MotifEdge acGcEdge = new("ACI", "GC", "CONVENTIONAL");

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
        MotifTLPView tlpGraph = MotifTLPView.ToTLP(motifGraph, "https://vpn.codepharm.net/RC1Test/OData");

        string outputPath = TestOutputHelper.GetOutputPath("Motif", "motif-graph.tlp");
        tlpGraph.SaveTLP(outputPath);
        Console.WriteLine($"Test output saved to: {outputPath}");
    }

}
