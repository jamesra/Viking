using FsCheck;
using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace GeometryTests.Algorithms
{
    /// <summary>
    /// Summary description for MeshCutTest
    /// </summary>
    [TestClass]
    public class MeshCutTest
    {
        public MeshCutTest()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void TestMeshCut()
        {
            Arb.Register<GridVector2Generators>();

            Action<GridVector2[]> SplitFunc = GenAndCutMesh;

            Prop.ForAll<GridVector2[]>(SplitFunc).QuickCheckThrowOnFailure();
        }

        public void GenAndCutMesh(GridVector2[] points)
        {
            if (points.Length < 2)
            {
                return;
            }

            TriangulationMesh<Vertex2D> mesh = new TriangulationMesh<Vertex2D>();

            Vertex2D[] InputVerts = points.Select(p => new Vertex2D(p)).ToArray();
            mesh.AddVerticies(InputVerts);

            MeshCut vertSet = new MeshCut(mesh.XSorted, mesh.YSorted, Geometry.CutDirection.HORIZONTAL, mesh.BoundingBox);

            RecursivelyCutMesh(mesh, vertSet);
        }

        void RecursivelyCutMesh(TriangulationMesh<Vertex2D> mesh, MeshCut vertSet)
        {
            if (vertSet.Verticies.Count < 2)
            {
                //Can't divide zero or one points
                return;
            }

            //////////////////////////////////////
            //Ensure vert sorting order is correct
            for (int i = 0; i < vertSet.Count - 1; i++)
            {
                int v1 = (int)vertSet.SortedAlongCutAxisVertSet[i];
                int v2 = (int)vertSet.SortedAlongCutAxisVertSet[i + 1];

                GridVector2 p1 = mesh[v1].Position;
                GridVector2 p2 = mesh[v2].Position;

                if (vertSet.CutAxis == CutDirection.HORIZONTAL)
                {
                    Assert.IsTrue(p1.X <= p2.X);
                    if (p1.X == p2.X)
                    {
                        Assert.IsTrue(vertSet.XSecondAxisAscending ? p1.Y < p2.Y : p2.Y < p1.Y);
                    }
                }
                else
                {
                    Assert.IsTrue(p1.Y <= p2.Y);
                    if (p1.Y == p2.Y)
                    {
                        Assert.IsTrue(vertSet.YSecondAxisAscending ? p1.X < p2.X : p2.X < p1.X);
                    }
                }
            }
            ///////////////////////////////////////

            vertSet.SplitIntoHalves(mesh.Verticies, out MeshCut LowerSubset, out MeshCut UpperSubset);

            Assert.AreEqual(LowerSubset.Verticies.Union(UpperSubset.Verticies).Count(), vertSet.Verticies.Count);
            Assert.AreEqual(LowerSubset.CutAxis, UpperSubset.CutAxis);

            CutDirection cutDir = LowerSubset.CutAxis;

            Assert.IsFalse((LowerSubset.BoundingBox.IntersectionType(UpperSubset.BoundingBox) & (OverlapType.INTERSECTING | OverlapType.CONTAINED)) > 0);

            if (LowerSubset.CutAxis == CutDirection.HORIZONTAL)
            {
                Assert.IsTrue(mesh[(int)LowerSubset.Verticies[0]].Position.Y <= mesh[(int)UpperSubset.Verticies[0]].Position.Y);
            }
            else
            {
                Assert.IsTrue(mesh[(int)LowerSubset.Verticies[0]].Position.X <= mesh[(int)UpperSubset.Verticies[0]].Position.X);
            }

            RecursivelyCutMesh(mesh, LowerSubset);
            RecursivelyCutMesh(mesh, UpperSubset);
        }

        static Property PropertyEachPointInOrder(TriangulationMesh<Vertex2D> mesh)
        {
            for (int iVert = 0; iVert < mesh.Verticies.Count; iVert++)
            {
                long iA = mesh.XSorted[iVert];
                long iB = mesh.YSorted[iVert];

                GridVector2 A = mesh[iA].Position;
                GridVector2 B = mesh[iB].Position;

                if (A.X > B.X)
                    return false.Label(string.Format("X is not sorted {0} > {1}", A.X, B.X));
                if (A.X == B.X && A.Y > B.Y)
                    return false.Label(string.Format("Equal X is not sorted on Y {0} > {1}", A, B));
            }

            return true.Label("Mesh verticies properly sorted");
        }
    }
}
