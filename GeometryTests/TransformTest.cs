using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Geometry;
using Geometry.Transforms; 
using System.Diagnostics; 

namespace GeometryTests
{
    /// <summary>
    /// Summary description for TransformTest
    /// </summary>
    [TestClass]
    public class TransformTest
    {
        public TransformTest()
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
        public void TransformTestMethodOne()
        {
            //
            // A simple test adding two transforms built from three points each
            //

            
            //
            //      *
            //      | \
            //      |  \
            //      *---*
            //
            
            
            GridVector2 fixedV1 = new GridVector2(0,0); 
            GridVector2 fixedV2 = new GridVector2(10,0); 
            GridVector2 fixedV3 = new GridVector2(0,10); 

            MappingGridVector2[] fixedPoints = new MappingGridVector2[] {new MappingGridVector2(fixedV1, fixedV1),
                                                                         new MappingGridVector2(fixedV2, fixedV2), 
                                                                         new MappingGridVector2(fixedV3, fixedV3)};

            //
            //     3----1
            //      \   |
            //       \  |
            //        \ |
            //          2
            //
            GridVector2 movingV1 = new GridVector2(2.5, 2.5);
            GridVector2 movingV2 = new GridVector2(2.5, -7.5);
            GridVector2 movingV3 = new GridVector2(-7.5, 2.5);

            MappingGridVector2[] movingPoints = new MappingGridVector2[] {new MappingGridVector2(movingV1, movingV1),
                                                                         new MappingGridVector2(movingV2, movingV2), 
                                                                         new MappingGridVector2(movingV3, movingV3)};

            MeshTransform fixedTransform = new MeshTransform(fixedPoints, new TransformInfo(DateTime.UtcNow));
            MeshTransform movingTransform = new MeshTransform(movingPoints, new TransformInfo(DateTime.UtcNow));

            TriangulationTransform result = TriangulationTransform.Transform(fixedTransform, movingTransform, null);

            MappingGridVector2[] newPoints = result.MapPoints;

            Debug.Assert(newPoints.Length == 4);
            Debug.Assert(newPoints[0].ControlPoint.X == 0 && newPoints[0].ControlPoint.Y == 0);
            Debug.Assert(newPoints[1].ControlPoint.X == 0 && newPoints[1].ControlPoint.Y == 2.5);
            Debug.Assert(newPoints[2].ControlPoint.X == 2.5 && newPoints[2].ControlPoint.Y == 0);
            Debug.Assert(newPoints[3].ControlPoint.X == 2.5 && newPoints[3].ControlPoint.Y == 2.5); 
        }

        [TestMethod]
        public void TransformTestMethodTwo()
        {
            //
            // A simple test adding two transforms built from three points each
            //

            GridVector2 fixedV1 = new GridVector2(0, 0);
            GridVector2 fixedV2 = new GridVector2(10, 0);
            GridVector2 fixedV3 = new GridVector2(0, 10);
            GridVector2 fixedV4 = new GridVector2(10, 10);

            MappingGridVector2[] fixedPoints = new MappingGridVector2[] {new MappingGridVector2(fixedV1, fixedV1),
                                                                         new MappingGridVector2(fixedV2, fixedV2), 
                                                                         new MappingGridVector2(fixedV3, fixedV3),
                                                                         new MappingGridVector2(fixedV4, fixedV4)};


            GridVector2 movingV1 = new GridVector2(2.5, 2.5);
            GridVector2 movingV2 = new GridVector2(2.5, -7.5);
            GridVector2 movingV3 = new GridVector2(-7.5, 2.5);
            GridVector2 movingV4 = new GridVector2(-8.5, -8.5); //Point four should be removed by the transform

            MappingGridVector2[] movingPoints = new MappingGridVector2[] {new MappingGridVector2(movingV1, movingV1),
                                                                         new MappingGridVector2(movingV2, movingV2), 
                                                                         new MappingGridVector2(movingV3, movingV3),
                                                                         new MappingGridVector2(movingV4, movingV4)};

            MeshTransform fixedTransform = new MeshTransform(fixedPoints, new TransformInfo(DateTime.UtcNow));
            MeshTransform movingTransform = new MeshTransform(movingPoints, new TransformInfo(DateTime.UtcNow));

            TriangulationTransform result = TriangulationTransform.Transform(fixedTransform, movingTransform, null);

            MappingGridVector2[] newPoints = result.MapPoints;

            Debug.Assert(newPoints.Length == 4);
            Debug.Assert(newPoints[0].ControlPoint.X == 0 && newPoints[0].ControlPoint.Y == 0);
            Debug.Assert(newPoints[1].ControlPoint.X == 0 && newPoints[1].ControlPoint.Y == 2.5);
            Debug.Assert(newPoints[2].ControlPoint.X == 2.5 && newPoints[2].ControlPoint.Y == 0);
            Debug.Assert(newPoints[3].ControlPoint.X == 2.5 && newPoints[3].ControlPoint.Y == 2.5);
        }

        [TestMethod]
        public void TransformTestMethodThree()
        {
            //
            // A simple test adding two transforms built from three points each
            //
            
            GridVector2 fixedV1 = new GridVector2(0, 0);
            GridVector2 fixedV2 = new GridVector2(10, 0);
            GridVector2 fixedV3 = new GridVector2(0, 10);
            GridVector2 fixedV4 = new GridVector2(10, 10);

            MappingGridVector2[] fixedPoints = new MappingGridVector2[] {new MappingGridVector2(fixedV1, fixedV1),
                                                                         new MappingGridVector2(fixedV2, fixedV2), 
                                                                         new MappingGridVector2(fixedV3, fixedV3),
                                                                         new MappingGridVector2(fixedV4, fixedV4)};


           

            GridVector2 movingV1 = new GridVector2(2.5, 2.5);
            GridVector2 movingV2 = new GridVector2(2.5, 17.5);
            GridVector2 movingV3 = new GridVector2(17.5, 2.5);
            GridVector2 movingV4 = new GridVector2(17.5, 17.5);

            MappingGridVector2[] movingPoints = new MappingGridVector2[] {new MappingGridVector2(movingV1, movingV1),
                                                                         new MappingGridVector2(movingV2, movingV2), 
                                                                         new MappingGridVector2(movingV3, movingV3),
                                                                         new MappingGridVector2(movingV4, movingV4)};

            

            MeshTransform fixedTransform = new MeshTransform(fixedPoints, new TransformInfo(DateTime.UtcNow));
            MeshTransform movingTransform = new MeshTransform(movingPoints, new TransformInfo(DateTime.UtcNow));

            TriangulationTransform result = TriangulationTransform.Transform(fixedTransform, movingTransform, null);

            MappingGridVector2[] newPoints = result.MapPoints;

            Debug.Assert(newPoints.Length == 4);
            Debug.Assert(newPoints[0].ControlPoint.X == 2.5 && newPoints[0].ControlPoint.Y == 2.5);
            Debug.Assert(newPoints[1].ControlPoint.X == 2.5 && newPoints[1].ControlPoint.Y == 10);
            Debug.Assert(newPoints[2].ControlPoint.X == 10 && newPoints[2].ControlPoint.Y == 2.5);
            Debug.Assert(newPoints[3].ControlPoint.X == 10 && newPoints[3].ControlPoint.Y == 10);
        }

        [TestMethod]
        public void TransformTestMethodFour()
        {
            //
            // A simple test adding two transforms built from three points each
            //
            GridVector2 fixedV1 = new GridVector2(0, 0);
            GridVector2 fixedV2 = new GridVector2(10, 0);
            GridVector2 fixedV3 = new GridVector2(0, 10);
            GridVector2 fixedV4 = new GridVector2(10, 10);

            MappingGridVector2[] fixedPoints = new MappingGridVector2[] {new MappingGridVector2(fixedV1, fixedV1),
                                                                         new MappingGridVector2(fixedV2, fixedV2), 
                                                                         new MappingGridVector2(fixedV3, fixedV3),
                                                                         new MappingGridVector2(fixedV4, fixedV4)};


            

            GridVector2 movingV1 = new GridVector2(2.5, 2.5);
            GridVector2 movingV2 = new GridVector2(2.5, 17.5);
            GridVector2 movingV3 = new GridVector2(17.5, 2.5);
            GridVector2 movingV4 = new GridVector2(18.5, 18.5); //Point four should be removed by the transform

            MappingGridVector2[] movingPoints = new MappingGridVector2[] {new MappingGridVector2(movingV1, GridVector2.Scale(movingV1,10)),
                                                                         new MappingGridVector2(movingV2, GridVector2.Scale(movingV2,10)), 
                                                                         new MappingGridVector2(movingV3, GridVector2.Scale(movingV3,10)),
                                                                         new MappingGridVector2(movingV4, GridVector2.Scale(movingV4,10))};

            MeshTransform fixedTransform = new MeshTransform(fixedPoints, new TransformInfo(DateTime.UtcNow));
            MeshTransform movingTransform = new MeshTransform(movingPoints, new TransformInfo(DateTime.UtcNow));

            TriangulationTransform result = TriangulationTransform.Transform(fixedTransform, movingTransform, null);

            MappingGridVector2[] newPoints = result.MapPoints;

            Debug.Assert(newPoints.Length == 3);
            Debug.Assert(newPoints[0].ControlPoint.X == 2.5 && newPoints[0].ControlPoint.Y == 2.5);
            Debug.Assert(newPoints[1].ControlPoint.X == 2.5 && newPoints[1].ControlPoint.Y == 10);
            Debug.Assert(newPoints[2].ControlPoint.X == 10 && newPoints[2].ControlPoint.Y == 2.5);

            Debug.Assert(newPoints[0].MappedPoint.X == 25 && newPoints[0].MappedPoint.Y == 25);
            Debug.Assert(newPoints[1].MappedPoint.X == 25 && newPoints[1].MappedPoint.Y == 100);
            Debug.Assert(newPoints[2].MappedPoint.X == 100 && newPoints[2].MappedPoint.Y == 25);
        }

        [TestMethod]
        public void TransformTestMethodFive()
        {
            //
            // A simple test adding two transforms built from three points each
            //


            GridVector2[] fixedCtrlPoints = new GridVector2[]{ new GridVector2(5, 5),
                                                              new GridVector2(5, 10),
                                                              new GridVector2(5, 15),
                                                              new GridVector2(15, 5),
                                                              new GridVector2(15, 10),
                                                              new GridVector2(15, 15)};
                                        //                      new GridVector2(25, 5),
                                        //                      new GridVector2(25, 10),
                                        //                      new GridVector2(25, 15)};

            GridVector2[] fixedMapPoints = new GridVector2[]{new GridVector2(50, 50),
                                                              new GridVector2(50, 100),
                                                              new GridVector2(50, 150),
                                                              new GridVector2(150, 50),
                                                              new GridVector2(150, 100),
                                                              new GridVector2(150, 150)};
                                        ///                      new GridVector2(250, 50),
                                        ///                      new GridVector2(250, 100),
                                        //                      new GridVector2(250, 150)};

            GridVector2[] movingCtrlPoints = new GridVector2[]{new GridVector2(100, 75),
                                                              new GridVector2(100, 125),
                                                              new GridVector2(100, 175),
                                                              new GridVector2(200, 75),
                                                              new GridVector2(200, 125),
                                                              new GridVector2(200, 175)};
                                     //                         new GridVector2(250, 50),
                                     //                         new GridVector2(250, 100),
                                     //                         new GridVector2(250, 150)};

            GridVector2[] movingMapPoints = new GridVector2[]{ new GridVector2(5, 5),
                                                              new GridVector2(5, 10),
                                                              new GridVector2(5, 15),
                                                              new GridVector2(15, 5),
                                                              new GridVector2(15, 10),
                                                              new GridVector2(15, 15)};
                                     //                         new GridVector2(25, 5),
                                     //                         new GridVector2(25, 10),
                                     //                         new GridVector2(25, 15)};

            List<MappingGridVector2> fixedPoints = new List<MappingGridVector2>(fixedMapPoints.Length);
            List<MappingGridVector2> movingPoints = new List<MappingGridVector2>(movingMapPoints.Length);

            for (int iFixed = 0; iFixed < fixedMapPoints.Length; iFixed++)
            {
                fixedPoints.Add(new MappingGridVector2(fixedCtrlPoints[iFixed], fixedMapPoints[iFixed]));
            }

            for (int iMapped = 0; iMapped < fixedMapPoints.Length; iMapped++)
            {
                movingPoints.Add(new MappingGridVector2(movingCtrlPoints[iMapped], movingMapPoints[iMapped]));
            }

            MeshTransform fixedTransform = new MeshTransform(fixedPoints.ToArray(), new TransformInfo(DateTime.UtcNow));
            MeshTransform movingTransform = new MeshTransform(movingPoints.ToArray(), new TransformInfo(DateTime.UtcNow));

            TriangulationTransform result = TriangulationTransform.Transform(fixedTransform, movingTransform, null);

            MappingGridVector2[] newPoints = result.MapPoints;

            Debug.Assert(result.MapPoints.Length == 7); 
        }

        [TestMethod]
        public void TransformTestMethodSix()
        {
            //
            // A simple test adding two transforms built from three points each
            //

            GridVector2[] fixedCtrlPoints = new GridVector2[]{ new GridVector2(5, 5),
                                                              new GridVector2(5, 10),
                                                              new GridVector2(5, 15),
                                                              new GridVector2(15, 5),
                                                              new GridVector2(15, 10),
                                                              new GridVector2(15, 15)};
            //                      new GridVector2(25, 5),
            //                      new GridVector2(25, 10),
            //                      new GridVector2(25, 15)};

            GridVector2[] fixedMapPoints = new GridVector2[]{new GridVector2(50, 50),
                                                              new GridVector2(50, 100),
                                                              new GridVector2(50, 150),
                                                              new GridVector2(150, 50),
                                                              new GridVector2(150, 100),
                                                              new GridVector2(150, 150)};
            ///                      new GridVector2(250, 50),
            ///                      new GridVector2(250, 100),
            //                      new GridVector2(250, 150)};

            GridVector2[] movingCtrlPoints = new GridVector2[]{new GridVector2(100, 75),
                                                              new GridVector2(100, 125),
                                                              new GridVector2(100, 175),
                                                              new GridVector2(200, 75),
                                                              new GridVector2(200, 125),
                                                              new GridVector2(200, 175)};
            //                         new GridVector2(250, 50),
            //                         new GridVector2(250, 100),
            //                         new GridVector2(250, 150)};

            GridVector2[] movingMapPoints = new GridVector2[]{ new GridVector2(5, 5),
                                                              new GridVector2(5, 10),
                                                              new GridVector2(5, 15),
                                                              new GridVector2(15, 5),
                                                              new GridVector2(15, 10),
                                                              new GridVector2(15, 15)};
            //                         new GridVector2(25, 5),
            //                         new GridVector2(25, 10),
            //                         new GridVector2(25, 15)};

            List<MappingGridVector2> fixedPoints = new List<MappingGridVector2>(fixedMapPoints.Length);
            List<MappingGridVector2> movingPoints = new List<MappingGridVector2>(movingMapPoints.Length);

            for (int iFixed = 0; iFixed < fixedMapPoints.Length; iFixed++)
            {
                fixedPoints.Add(new MappingGridVector2(fixedCtrlPoints[iFixed], fixedMapPoints[iFixed]));
            }

            for (int iMapped = 0; iMapped < fixedMapPoints.Length; iMapped++)
            {
                movingPoints.Add(new MappingGridVector2(movingCtrlPoints[iMapped], movingMapPoints[iMapped]));
            }

            MeshTransform fixedTransform = new MeshTransform(fixedPoints.ToArray(), new TransformInfo(DateTime.UtcNow));
            MeshTransform movingTransform = new MeshTransform(movingPoints.ToArray(), new TransformInfo(DateTime.UtcNow));

            TriangulationTransform result = TriangulationTransform.Transform(fixedTransform, movingTransform, null);

            MappingGridVector2[] newPoints = result.MapPoints;

            Debug.Assert(result.MapPoints.Length == 7);
        } 

         [TestMethod]
        public void GridTransformTestMethodOne()
        {
            //
            // A simple test adding two transforms built from three points each
            // 
            GridVector2 fixedV1 = new GridVector2(0,0); 
            GridVector2 fixedV2 = new GridVector2(10,0); 
            GridVector2 fixedV3 = new GridVector2(0,10);
            GridVector2 fixedV4 = new GridVector2(10, 10); 

            MappingGridVector2[] fixedPoints = new MappingGridVector2[] {new MappingGridVector2(fixedV1, fixedV1),
                                                                         new MappingGridVector2(fixedV2, fixedV2), 
                                                                         new MappingGridVector2(fixedV3, fixedV3),
                                                                         new MappingGridVector2(fixedV4, fixedV4)};

            GridVector2 movingV1 = new GridVector2(2.5, 2.5);
            GridVector2 movingV2 = new GridVector2(2.5, -7.5);
            GridVector2 movingV3 = new GridVector2(-7.5, 2.5);

            MappingGridVector2[] movingPoints = new MappingGridVector2[] {new MappingGridVector2(movingV1, movingV1),
                                                                         new MappingGridVector2(movingV2, movingV2), 
                                                                         new MappingGridVector2(movingV3, movingV3)};

            GridTransform fixedTransform = new GridTransform(fixedPoints, new GridRectangle(fixedV1, 10, 10), 2, 2, new TransformInfo(DateTime.UtcNow));
            MeshTransform movingTransform = new MeshTransform(movingPoints, new TransformInfo(DateTime.UtcNow));

             

            TriangulationTransform result = TriangulationTransform.Transform(fixedTransform, movingTransform, null);

            MappingGridVector2[] newPoints = result.MapPoints;

            Debug.Assert(newPoints[0].ControlPoint.X == 0 && newPoints[0].ControlPoint.Y == 0);
            Debug.Assert(newPoints[1].ControlPoint.X == 0 && newPoints[1].ControlPoint.Y == 2.5);
            Debug.Assert(newPoints[2].ControlPoint.X == 2.5 && newPoints[2].ControlPoint.Y == 0);
            Debug.Assert(newPoints[3].ControlPoint.X == 2.5 && newPoints[3].ControlPoint.Y == 2.5); 
        }

         [TestMethod]
         public void GridTransformTestMethodTwo()
         {
             //
             // A simple test adding two transforms built from three points each
             // 
             GridVector2 fixedV1 = new GridVector2(0, 0);
             GridVector2 fixedV2 = new GridVector2(10, 0);
             GridVector2 fixedV3 = new GridVector2(0, 10);
             GridVector2 fixedV4 = new GridVector2(10, 10);

             MappingGridVector2[] fixedPoints = new MappingGridVector2[] {new MappingGridVector2(fixedV1, fixedV1),
                                                                         new MappingGridVector2(fixedV2, fixedV2), 
                                                                         new MappingGridVector2(fixedV3, fixedV3),
                                                                         new MappingGridVector2(fixedV4, fixedV4)};

             GridVector2 movingV1 = new GridVector2(2.5, 2.5);
             GridVector2 movingV2 = new GridVector2(2.5, 12.5);
             GridVector2 movingV3 = new GridVector2(12.5, 12.5);
             GridVector2 movingV4 = new GridVector2(12.5, 2.5);

             MappingGridVector2[] movingPoints = new MappingGridVector2[] {new MappingGridVector2(movingV1, movingV1),
                                                                         new MappingGridVector2(movingV2, movingV2), 
                                                                         new MappingGridVector2(movingV3, movingV3),
                                                                         new MappingGridVector2(movingV4, movingV4)};

             GridTransform fixedTransform = new GridTransform(fixedPoints, new GridRectangle(fixedV1, 10, 10), 2, 2, new TransformInfo(DateTime.UtcNow));
             MeshTransform movingTransform = new MeshTransform(movingPoints, new TransformInfo(DateTime.UtcNow));



             TriangulationTransform result = TriangulationTransform.Transform(fixedTransform, movingTransform, null);

             MappingGridVector2[] newPoints = result.MapPoints;

             Debug.Assert(newPoints[0].ControlPoint.X == 2.5 && newPoints[0].ControlPoint.Y == 2.5);
             Debug.Assert(newPoints[1].ControlPoint.X == 2.5 && newPoints[1].ControlPoint.Y == 10);
             Debug.Assert(newPoints[2].ControlPoint.X == 10 && newPoints[2].ControlPoint.Y == 2.5);
             Debug.Assert(newPoints[3].ControlPoint.X == 10 && newPoints[3].ControlPoint.Y == 10);
         }

        [TestMethod]
        public void GridTransformTestMethodThree()
        {
            //
            // A simple test adding two transforms built from three points each
            // 
            GridVector2 fixedV1 = new GridVector2(0, 0);
            GridVector2 fixedV2 = new GridVector2(10, 0);
            GridVector2 fixedV3 = new GridVector2(0, 10);
            GridVector2 fixedV4 = new GridVector2(10, 10);


            GridVector2 movingV1 = new GridVector2(2.5, 2.5);
            GridVector2 movingV2 = new GridVector2(2.5, 12.5);
            GridVector2 movingV3 = new GridVector2(12.5, 12.5);
            GridVector2 movingV4 = new GridVector2(12.5, 2.5);

            MappingGridVector2[] transformPoints = new MappingGridVector2[] {new MappingGridVector2(fixedV1, movingV1),
                                                                         new MappingGridVector2(fixedV2, movingV2),
                                                                         new MappingGridVector2(fixedV3, movingV3),
                                                                         new MappingGridVector2(fixedV4, movingV4)};
             
            GridTransform fixedTransform = new GridTransform(transformPoints, new GridRectangle(fixedV1, 10, 10), 2, 2, new TransformInfo(DateTime.UtcNow));

            GridVector2[] PointsToTransform = new GridVector2[] { new GridVector2(2.5, 2.5),
                                                                new GridVector2(2.5, 7.5),
                                                                new GridVector2(7.5, 2.5),
                                                                new GridVector2(7.5, 7.5) };

            GridVector2[] PointsToInverseTransform = fixedTransform.Transform(PointsToTransform);

            GridVector2[] RevertedPoints = fixedTransform.InverseTransform(PointsToInverseTransform);

            for (int i = 0; i < PointsToTransform.Length; i++)
            {
                Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(PointsToTransform[i], RevertedPoints[i]);
            } 
        } 
    }
}
