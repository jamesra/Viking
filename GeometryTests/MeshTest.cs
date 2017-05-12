using System;
using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class MeshTest
    {
        [TestMethod]
        public void TestEdgeOperations()
        {
            Edge A1 = new Edge(1, 2);
            Edge A2 = new Edge(2, 1);

            Assert.AreEqual(A1, A2);

            Edge B = new Edge(2, 3);
            Assert.AreNotEqual(A1, B);

            Assert.IsTrue(A1.CompareTo(B) < 0);
        }

        [TestMethod]
        public void TestFaceOperations()
        {
            Face A1 = new Face(1, 2, 3);
            Face A2 = new Face(2, 1, 3);

            Assert.AreEqual(A1, A2);

            Face B = new Face(2, 3, 4);
            Assert.AreNotEqual(A1, B);

            Assert.IsTrue(A1.CompareTo(B) < 0);
        }

        private Vertex[] CreateTetrahedronVerts()
        {
            return new Vertex[] {new Vertex(new GridVector3(0, 0, 0), new GridVector3(0, 0, 0)),
                                     new Vertex(new GridVector3(0, 1, 0), new GridVector3(0, 1, 0)),
                                     new Vertex(new GridVector3(0, 0, 1), new GridVector3(0, 0, 1)),
                                     new Vertex(new GridVector3(1, 0, 0), new GridVector3(1, 0, 0)) };
        }

        private Face[] CreateTetrahedronFaces()
        {
            return new Face[] {new Face(0,1,2),
                               new Face(0,3,1),
                               new Face(0,2,3),
                               new Face(1,3,2) };
        }
        
        [TestMethod]
        public void CreateTetrahedronWithPoints()
        {

            DynamicRenderMesh mesh = new DynamicRenderMesh();

            Vertex[] verts = CreateTetrahedronVerts();

            int iFirstIndex = mesh.AddVertex(verts);
            Assert.AreEqual(0, iFirstIndex);

            Face[] faces = CreateTetrahedronFaces();

            foreach (Face f in faces)
            {
                foreach (EdgeKey e in f.Edges)
                {
                    if(!mesh.Edges.ContainsKey(e))
                        mesh.AddEdge(e);
                }
            }

            foreach(Face f in faces)
            {
                mesh.AddFace(f);
            }
        }

        [TestMethod]
        public void CreateTetrahedronWithFaces()
        {
            DynamicRenderMesh mesh = new DynamicRenderMesh();

            Vertex[] verts = CreateTetrahedronVerts();

            foreach (Vertex v in verts)
            {
                mesh.AddVertex(v);
            }

            Face[] faces = CreateTetrahedronFaces();

            foreach(Face f in faces)
            {
                mesh.AddFace(f); 
            }

            foreach(Face f in faces)
            {
                foreach(EdgeKey e in f.Edges)
                {
                    Assert.IsTrue(mesh.Edges.ContainsKey(e));
                }
            }
        }
    }
}
