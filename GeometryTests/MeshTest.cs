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

        [TestMethod]
        public void TestFaceAdjacenyOperations()
        {
            DynamicRenderMesh mesh = new DynamicRenderMesh();

            Vertex3D[] verts = CreateTetrahedronVerts();

            foreach (Vertex3D v in verts)
            {
                mesh.AddVertex(v);
            }

            Face A = new Face(0, 1, 2);
            Face B = new Face(0, 2, 3);
            Assert.AreNotEqual(A, B);

            mesh.AddFace(A);
            mesh.AddFace(B);
            EdgeKey zero_two_key = new EdgeKey(0, 2);
            Assert.IsTrue(mesh.Contains(zero_two_key));

            IEdge zero_two = mesh.Edges[zero_two_key];
            Assert.AreEqual(zero_two.Faces.Count, 2); //After adding both faces the edge count should be 2 for the shared edge
        }



        private Vertex3D[] CreateTetrahedronVerts()
        {
            return new Vertex3D[] {new Vertex3D(new GridVector3(0, 0, 0), new GridVector3(0, 0, 0)),
                                     new Vertex3D(new GridVector3(0, 1, 0), new GridVector3(0, 1, 0)),
                                     new Vertex3D(new GridVector3(0, 0, 1), new GridVector3(0, 0, 1)),
                                     new Vertex3D(new GridVector3(1, 0, 0), new GridVector3(1, 0, 0)) };
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

            Vertex3D[] verts = CreateTetrahedronVerts();

            int iFirstIndex = mesh.AddVerticies(verts);
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

            Vertex3D[] verts = CreateTetrahedronVerts();

            foreach (Vertex3D v in verts)
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
