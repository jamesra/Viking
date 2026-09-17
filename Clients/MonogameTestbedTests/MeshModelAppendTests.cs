using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VikingXNAGraphics;

namespace MonogameTestbedTests
{
    /// <summary>
    /// Covers the append paths on MeshModel, which meshing threads use to grow the arrays the draw thread reads.
    /// </summary>
    [TestClass]
    public class MeshModelAppendTests
    {
        private static VertexPositionColor Vertex(int i) =>
            new(new Vector3(i, i * 2, i * 3), new Color(i, i, i));

        private static List<VertexPositionColor> Vertices(params int[] ids)
        {
            List<VertexPositionColor> list = new(ids.Length);
            foreach (int id in ids)
                list.Add(Vertex(id));

            return list;
        }

        private static void AssertVertices(MeshModel<VertexPositionColor> model, params int[] expectedIds)
        {
            Assert.AreEqual(expectedIds.Length, model.Vertices.Length);
            for (int i = 0; i < expectedIds.Length; i++)
                Assert.AreEqual(Vertex(expectedIds[i]), model.Vertices[i], $"Vertex {i} differs.");
        }

        [TestMethod]
        public void AppendVerticiesToEmptyModel()
        {
            MeshModel<VertexPositionColor> model = new();

            int iInsert = model.AppendVerticies(Vertices(1, 2, 3));

            Assert.AreEqual(0, iInsert);
            AssertVertices(model, 1, 2, 3);
        }

        [TestMethod]
        public void AppendVerticiesToPopulatedModel()
        {
            MeshModel<VertexPositionColor> model = new();
            model.AppendVerticies(Vertices(1, 2, 3));

            int iInsert = model.AppendVerticies(Vertices(4, 5));

            Assert.AreEqual(3, iInsert);
            AssertVertices(model, 1, 2, 3, 4, 5);
        }

        [TestMethod]
        public void AppendEmptyVerticiesLeavesModelUnchanged()
        {
            MeshModel<VertexPositionColor> model = new();
            model.AppendVerticies(Vertices(1, 2));

            int iInsert = model.AppendVerticies(Vertices());

            Assert.AreEqual(2, iInsert);
            AssertVertices(model, 1, 2);
        }

        [TestMethod]
        public void AppendEmptyVerticiesToEmptyModelYieldsEmptyArray()
        {
            MeshModel<VertexPositionColor> model = new();

            int iInsert = model.AppendVerticies(Vertices());

            Assert.AreEqual(0, iInsert);
            AssertVertices(model);
        }

        [TestMethod]
        public void AppendedVerticiesDoNotReuseThePublishedArray()
        {
            MeshModel<VertexPositionColor> model = new();
            model.AppendVerticies(Vertices(1, 2));
            VertexPositionColor[] published = model.Vertices;

            model.AppendVerticies(Vertices(3));

            //Readers holding the previous reference must keep seeing the geometry they were given.
            Assert.AreNotSame(published, model.Vertices);
            Assert.AreEqual(2, published.Length);
        }

        [TestMethod]
        public void AppendEdgesToEmptyModel()
        {
            MeshModel<VertexPositionColor> model = new();

            model.AppendEdges(new List<int> { 0, 1, 2 });

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, model.Edges);
        }

        [TestMethod]
        public void AppendEdgesToPopulatedModel()
        {
            MeshModel<VertexPositionColor> model = new();
            model.AppendEdges(new List<int> { 0, 1, 2 });

            model.AppendEdges(new List<int> { 3, 4, 5 });

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, model.Edges);
        }

        [TestMethod]
        public void AppendEmptyEdgesLeavesEdgesUnchanged()
        {
            MeshModel<VertexPositionColor> model = new();
            model.AppendEdges(new List<int> { 0, 1, 2 });

            model.AppendEdges(new List<int>());

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, model.Edges);
        }

        [TestMethod]
        public void AppendEmptyEdgesToEmptyModelYieldsEmptyArray()
        {
            MeshModel<VertexPositionColor> model = new();

            model.AppendEdges(new List<int>());

            Assert.IsNotNull(model.Edges);
            Assert.AreEqual(0, model.Edges.Length);
        }
    }
}
