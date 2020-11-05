using System;
using AnnotationVizLib;
using AnnotationVizLib.SimpleOData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Geometry;
using Geometry.Meshing;
using UnitsAndScale;

namespace ColladaIOTest
{
    public enum ENDPOINT
    {
        TEST,
        RC1,
        RC2,
        RPC1,
        TEMPORALMONKEY,
        INFERIORMONKEY
    }

    [TestClass]
    public class DynamicRenderMeshColladaSerializerTest
    {
        private static Dictionary<ENDPOINT, Uri> EndpointMap = new Dictionary<ENDPOINT, Uri> { { ENDPOINT.TEST, new Uri("http://webdev.connectomes.utah.edu/RC1Test/OData") },
                                                                                               { ENDPOINT.RC1, new Uri("http://websvc1.connectomes.utah.edu/RC1/OData") },
                                                                                               { ENDPOINT.RC2, new Uri("http://websvc1.connectomes.utah.edu/RC2/OData") },
                                                                                               { ENDPOINT.RPC1, new Uri("http://websvc1.connectomes.utah.edu/RPC1/OData") },
                                                                                               { ENDPOINT.TEMPORALMONKEY, new Uri("http://websvc1.connectomes.utah.edu/NeitzTemporalMonkey/OData") },
                                                                                               { ENDPOINT.INFERIORMONKEY, new Uri("http://websvc1.connectomes.utah.edu/NeitzInferiorMonkey/OData") }};

        /// <summary>
        /// This test is a workaround to generate meshes for specific cells on request from the lab
        /// </summary>
        [TestMethod]
        public void TestDAESerializationForSpecificCell()
        {
            MorphologyMesh.MorphologyColladaView view = CreateView(new ulong[] { 2713 }, ENDPOINT.RPC1);
            //MorphologyMesh.MorphologyColladaView view = CreateView(new long[] { 142, 180}, ENDPOINT.INFERIORMONKEY);

            ColladaIO.DynamicRenderMeshColladaSerializer.SerializeToFile(view, "TestDAESerialization.dae");

            ColladaIO.DynamicRenderMeshColladaSerializer.SerializeToFolder(view, "Output");
        }

        [TestMethod]
        public void TestDAESerialization()
        { 
            MorphologyMesh.MorphologyColladaView view = CreateView(new ulong[] { 180, 172 }, ENDPOINT.RC1);
            //MorphologyMesh.MorphologyColladaView view = CreateView(new long[] { 142, 180}, ENDPOINT.INFERIORMONKEY);

            ColladaIO.DynamicRenderMeshColladaSerializer.SerializeToFile(view, "TestDAESerialization.dae");

            ColladaIO.DynamicRenderMeshColladaSerializer.SerializeToFolder(view, "Output");
        }

        /// <summary>
        /// Create a tube of circles offset slighty each section
        /// </summary>
        public MorphologyMesh.MorphologyColladaView CreateView(ICollection<ulong> CellIDs, ENDPOINT endpoint)
        {
            AnnotationVizLib.StructureMorphologyColorMap colorMap = TestUtils.LoadColorMap("Resources\\RC1ColorMapping");

            AnnotationVizLib.MorphologyGraph graph = null;
            if (CellIDs != null)
            {
                graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(CellIDs, true, EndpointMap[endpoint]);
            }
            else
            {
                graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(EndpointMap[endpoint], true);
            }

            graph.ConnectIsolatedSubgraphs();

            MorphologyMesh.MorphologyColladaView view = new MorphologyMesh.MorphologyColladaView(graph.scale, colorMap);
            view.Add(graph);
             
            return view;
        }

        [TestMethod]
        public void TestAllCellsDAESerialization()
        {
            MorphologyMesh.MorphologyColladaView view = CreateView(null, ENDPOINT.INFERIORMONKEY);
            //MorphologyMesh.MorphologyColladaView view = CreateView(new long[] { 142, 180}, ENDPOINT.INFERIORMONKEY);

            ColladaIO.DynamicRenderMeshColladaSerializer.SerializeToFile(view, "TestAllCellsDAESerialization.dae");
        }
        
    }
}
