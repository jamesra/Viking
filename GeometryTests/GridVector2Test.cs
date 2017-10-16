using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting; 
using Geometry;
using MathNet.Numerics.LinearAlgebra;

namespace UtilitiesTests
{
    /// <summary>
    /// Summary description for GridVector2Test
    /// </summary>
    [TestClass]
    public class GridVector2Test
    {
        public GridVector2Test()
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
        public void TestAngle()
        {
            GridVector2 A = new GridVector2(5, 0);
            GridVector2 B = new GridVector2(2.5, 2.5); 

            double PI4 = Math.PI / 4;

            double angle = GridVector2.Angle(A,B);
            Assert.IsTrue(angle - Global.Epsilon < (3.0 * PI4) &&
                         angle + Global.Epsilon > (3.0 * PI4)); 

            A = new GridVector2(5, 0);
            B = new GridVector2(2.5, -2.5); 

            angle = GridVector2.Angle(A,B);
            Assert.IsTrue(angle - Global.Epsilon < (-3.0 * PI4) &&
                         angle + Global.Epsilon > (-3.0 * PI4)); 
            
            //
            // TODO: Add test logic	here
            //
        }

        [TestMethod]
        public void TestAngle2()
        {
            double Pi4 = Math.PI / 4.0;
            double Pi2 = Math.PI / 2.0;

            GridVector2 Origin = new GridVector2(0, 0);
            GridVector2 A = new GridVector2(1, 0);
            GridVector2 B = new GridVector2(0, 1);
            GridVector2 C = new GridVector2(-1, 0);

            double Degree90 = GridVector2.ArcAngle(Origin, A, B);
            Assert.AreEqual(Degree90, Pi2);

            Degree90 = GridVector2.ArcAngle(Origin, B, A);
            Assert.AreEqual(Degree90, -Pi2);
             
            double Degree180 = GridVector2.ArcAngle(Origin, A, C);
            Assert.AreEqual(Degree180, Math.PI);
             
            double Degree0 = GridVector2.Angle(Origin, A);
            Assert.AreEqual(Degree0, 0); 

            Degree90 = GridVector2.Angle(Origin, B);
            Assert.AreEqual(Degree90, Pi2);

            //Translate the vectors slightly and ensure angles are unchanged
            GridVector2 offset = new GridVector2(5, 2.5);
            Origin += offset;
            A += offset;
            B += offset;
            C += offset;

            Degree90 = GridVector2.ArcAngle(Origin, A, B);
            Assert.AreEqual(Degree90, Pi2);

            Degree90 = GridVector2.ArcAngle(Origin, B, A);
            Assert.AreEqual(Degree90, -Pi2);

            Degree180 = GridVector2.ArcAngle(Origin, A, C);
            Assert.AreEqual(Degree180, Math.PI);

            Degree0 = GridVector2.Angle(Origin, A);
            Assert.AreEqual(Degree0, 0);

            Degree90 = GridVector2.Angle(Origin, B);
            Assert.AreEqual(Degree90, Pi2);
        }

        [TestMethod]
        public void TestAngle3()
        {
            double Pi4 = Math.PI / 4.0;
            double Pi2 = Math.PI / 2.0;

            GridVector2 Origin = new GridVector2(0, 0);
            GridVector2 A = new GridVector2(1, 0);
            GridVector2 B = new GridVector2(0, 1);
            GridVector2 C = new GridVector2(-1, 0);
            GridVector2 D = new GridVector2(Math.Cos(Pi4), Math.Sin(Pi4));

            double degree45 = GridVector2.ArcAngle(Origin, A, D);
            double result = GridVector2.Angle(Origin, D);
            Assert.AreEqual(degree45, Pi4);
            Assert.AreEqual(result,  Pi4);
        }

        [TestMethod]
        public void TestTranslate()
        {
            GridVector2 A = new GridVector2(0, 0);

            Vector<double> v = Vector<double>.Build.Dense(new double[] { A.X, A.Y, 0, 1 });

            GridVector2 Offset = new GridVector2(1, 2);

             Matrix<double> translationMatrix = GeometryMathNetNumerics.CreateTranslationMatrix(Offset);
            Vector<double> translated = translationMatrix * v;

            GridVector2 translatedPoint = translated.ToGridVector2();
            Assert.AreEqual(translatedPoint, A + Offset);

            Matrix<double> p = A.ToMatrix();
            Matrix<double> translatedMatrix = translationMatrix * p;

            ICollection<GridVector2> translatedPoints = translatedMatrix.ToGridVector2();
            Assert.AreEqual(translatedPoints.First(), A + Offset);
        }

        [TestMethod]
        public void TestRotate()
        {
            GridVector2 N = new GridVector2(1, 2);
            GridVector2 S = new GridVector2(1, 0);
            GridVector2 E = new GridVector2(2, 1);
            GridVector2 W = new GridVector2(0, 1);

            GridVector2 Centroid = new GridVector2(1, 1);

            GridVector2[] points = new GridVector2[] { N, S, E, W };

            GridVector2 calculatedCentroid = points.Average();

            Assert.AreEqual(Centroid ,  calculatedCentroid);

            GridVector2[] pointsToRotate = new GridVector2[] { N, W, S, E };
            GridVector2[] rotatedPoints = pointsToRotate.Rotate(Math.PI / 2, Centroid).ToArray();

            Assert.AreEqual(rotatedPoints[0], W);
            Assert.AreEqual(rotatedPoints[1], S);
            Assert.AreEqual(rotatedPoints[2], E);
            Assert.AreEqual(rotatedPoints[3], N);
        }

        [TestMethod]
        public void ToFromMatrix()
        {
            GridVector2 A = new GridVector2(1, 2);
            GridVector2 B = new GridVector2(1, 0);
            GridVector2 C = new GridVector2(2, 1);
            GridVector2 D = new GridVector2(0, 1);
              
            GridVector2[] points = new GridVector2[] { A, B, C, D };

            Matrix<double> m = points.ToMatrix();
            GridVector2[] convertedPoints = m.ToGridVector2().ToArray();

            Assert.AreEqual(points.Length, convertedPoints.Length);

            for(int i = 0; i < points.Length; i++)
            {
                Assert.AreEqual(points[i], convertedPoints[i]);
            }
        }

        [TestMethod]
        public void AreClockwiseTest()
        {
            GridVector2 W = new GridVector2(-1, 0);
            GridVector2 N = new GridVector2(0, 1);
            GridVector2 E = new GridVector2(1, 0);
            GridVector2 S = new GridVector2(0, -1);

            GridVector2[] WNE_Points = new GridVector2[] { W, N, E};
            GridVector2[] ENW_Points = new GridVector2[] { E, N, W };

            Assert.IsTrue(WNE_Points.AreClockwise());
            Assert.IsFalse(ENW_Points.AreClockwise());
            Assert.AreNotEqual(WNE_Points.AreClockwise(), ENW_Points.AreClockwise());
            

            GridVector2[] NES_Points = new GridVector2[] { N, E, S };
            GridVector2[] SEN_Points = new GridVector2[] { S, E, N };

            Assert.IsTrue(NES_Points.AreClockwise());
            Assert.IsFalse(SEN_Points.AreClockwise());
            Assert.AreNotEqual(NES_Points.AreClockwise(), SEN_Points.AreClockwise());
            

            GridVector2[] NES_Points_Translated = NES_Points.Translate(new GridVector2(10, 10));
            GridVector2[] SEN_Points_Translated = SEN_Points.Translate(new GridVector2(10, 10));

            Assert.IsTrue(NES_Points_Translated.AreClockwise());
            Assert.IsFalse(SEN_Points_Translated.AreClockwise());
            Assert.AreNotEqual(NES_Points_Translated.AreClockwise(), SEN_Points_Translated.AreClockwise()); 
        }

        [TestMethod]
        public void ConvexHullTest()
        {
            GridVector2[] points = new GridVector2[] { new GridVector2(-10,-10),
                                                       new GridVector2(-10, 10),
                                                       new GridVector2(10,10),
                                                       new GridVector2(10,-10)};

            int[] original_idx;
            GridVector2[] ConvexHullPoints = points.ConvexHull(out original_idx);
            Assert.IsTrue(ConvexHullPoints.Length == 5);

            GridPolygon poly = new GridPolygon(ConvexHullPoints);
            Assert.IsTrue(poly.BoundingBox == points.BoundingBox());

            GridVector2 Centroid = ConvexHullPoints.Average();
            Assert.IsTrue(Centroid == new GridVector2(0, 0));

            points = points.Translate(new GridVector2(-20, 20));
            ConvexHullPoints = points.ConvexHull(out original_idx);

            Assert.IsTrue(ConvexHullPoints.Length == 5);
        }

    }
}
