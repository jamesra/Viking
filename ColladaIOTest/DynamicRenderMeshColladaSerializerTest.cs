using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Viking.Common;

namespace ColladaIOTest
{
    [TestClass]
    public class DynamicRenderMeshColladaSerializerTest
    {
        /// <summary>
        /// This test is a workaround to generate meshes for specific cells on request from the lab
        /// </summary>
        [TestMethod]
        public void TestDAESerializationForSpecificCell()
        {
            MorphologyMesh.MorphologyColladaView view = CreateView(new ulong[] { 2713 }, Endpoint.RPC1);
            //MorphologyMesh.MorphologyColladaView view = CreateView(new long[] { 142, 180}, Endpoint.INFERIORMONKEY);

            ColladaIO.DynamicRenderMeshColladaSerializer.SerializeToFile(view, "TestDAESerialization.dae");

            ColladaIO.DynamicRenderMeshColladaSerializer.SerializeToFolder(view, "Output");
        }

        [TestMethod]
        public void TestDAESerialization()
        {
            MorphologyMesh.MorphologyColladaView view = CreateView(new ulong[] { 180, 172 }, Endpoint.RC1);
            //MorphologyMesh.MorphologyColladaView view = CreateView(new long[] { 142, 180}, Endpoint.INFERIORMONKEY);

            ColladaIO.DynamicRenderMeshColladaSerializer.SerializeToFile(view, "TestDAESerialization.dae");

            ColladaIO.DynamicRenderMeshColladaSerializer.SerializeToFolder(view, "Output");
        }

        /// <summary>
        /// Create a tube of circles offset slighty each section
        /// </summary>
        public static MorphologyMesh.MorphologyColladaView CreateView(ICollection<ulong> CellIDs, Endpoint endpoint)
        {
            AnnotationVizLib.StructureMorphologyColorMap colorMap = TestUtils.LoadColorMap("Resources\\RC1ColorMapping");

            AnnotationVizLib.MorphologyGraph graph = null;
            if (CellIDs != null)
            {
                List<long> longIDs = [];
                foreach (var id in CellIDs)
                    longIDs.Add((long)id);
                graph = AnnotationVizLib.OData.ODataMorphologyFactory.FromOData(longIDs, true, ODataEndpointCatalog.EndpointMap[endpoint]);
            }
            else
            {
                graph = AnnotationVizLib.OData.ODataMorphologyFactory.FromOData([], true, ODataEndpointCatalog.EndpointMap[endpoint]);
            }

            graph.ConnectIsolatedSubgraphs();

            MorphologyMesh.MorphologyColladaView view = new(graph.scale, colorMap);
            view.Add(graph);

            return view;
        }

        [TestMethod]
        public void TestAllCellsDAESerialization()
        {
            MorphologyMesh.MorphologyColladaView view = CreateView(null, Endpoint.INFERIORMONKEY);
            //MorphologyMesh.MorphologyColladaView view = CreateView(new long[] { 142, 180}, Endpoint.INFERIORMONKEY);

            ColladaIO.DynamicRenderMeshColladaSerializer.SerializeToFile(view, "TestAllCellsDAESerialization.dae");
        }

    }
}
