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
    /// Summary description for GridVector3Test
    /// </summary>
    [TestClass]
    public class GridVector3Test
    {
        public GridVector3Test()
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
            GridVector3 A = new GridVector3(5, 0, 0);
            GridVector3 B = new GridVector3(2.5, 2.5,0);

            double PI4 = Math.PI / 4;

            double angle = GridVector3.Angle(A, B);
            Assert.IsTrue(angle - Global.Epsilon < (3.0 * PI4) &&
                         angle + Global.Epsilon > (3.0 * PI4));

            A = new GridVector3(5, 0,0);
            B = new GridVector3(2.5, -2.5,0);

            angle = GridVector3.Angle(A, B);
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

            GridVector3 Origin = new GridVector3(0, 0,0);
            GridVector3 A = new GridVector3(1, 0,0);
            GridVector3 B = new GridVector3(0, 1,0);
            GridVector3 C = new GridVector3(-1, 0,0);

            double Degree90 = GridVector3.ArcAngle(Origin, A, B);
            Assert.AreEqual(Degree90, Pi2);

            Degree90 = GridVector3.ArcAngle(Origin, B, A);
            Assert.AreEqual(Degree90, -Pi2);

            double Degree180 = GridVector3.ArcAngle(Origin, A, C);
            Assert.AreEqual(Degree180, Math.PI);

            double Degree0 = GridVector3.Angle(Origin, A);
            Assert.AreEqual(Degree0, 0);

            Degree90 = GridVector3.Angle(Origin, B);
            Assert.AreEqual(Degree90, Pi2);
        }

        [TestMethod]
        public void TestAngle3()
        {
            double Pi4 = Math.PI / 4.0;
            double Pi2 = Math.PI / 2.0;

            GridVector3 Origin = new GridVector3(0, 0,0);
            GridVector3 A = new GridVector3(1, 0,0);
            GridVector3 B = new GridVector3(0, 1,0);
            GridVector3 C = new GridVector3(-1, 0,0);
            GridVector3 D = new GridVector3(Math.Cos(Pi4), Math.Sin(Pi4),0);

            double degree45 = GridVector3.ArcAngle(Origin, A, D);
            double result = GridVector3.Angle(Origin, D);
            Assert.AreEqual(degree45, Pi4);
            Assert.AreEqual(result, Pi4);
        }

        [TestMethod]
        public void TestTranslate()
        {
            GridVector3 A = new GridVector3(0, 0,0);

            Vector<double> v = Vector<double>.Build.Dense(new double[] { A.X, A.Y, 0, 1 });

            GridVector3 Offset = new GridVector3(1, 2,0);

            Matrix<double> translationMatrix = GeometryMathNetNumerics.CreateTranslationMatrix(Offset);
            Vector<double> translated = translationMatrix * v;

            GridVector3 translatedPoint = translated.ToGridVector3();
            Assert.AreEqual(translatedPoint, A + Offset);

            Matrix<double> p = A.ToMatrix();
            Matrix<double> translatedMatrix = translationMatrix * p;

            ICollection<GridVector3> translatedPoints = translatedMatrix.ToGridVector3();
            Assert.AreEqual(translatedPoints.First(), A + Offset);
        }
        
        [TestMethod]
        public void ToFromMatrix()
        {
            GridVector3 A = new GridVector3(1, 2,0);
            GridVector3 B = new GridVector3(1, 0,0);
            GridVector3 C = new GridVector3(2, 1,0);
            GridVector3 D = new GridVector3(0, 1,0);

            GridVector3[] points = new GridVector3[] { A, B, C, D };

            Matrix<double> m = points.ToMatrix();
            GridVector3[] convertedPoints = m.ToGridVector3().ToArray();

            Assert.AreEqual(points.Length, convertedPoints.Length);

            for (int i = 0; i < points.Length; i++)
            {
                Assert.AreEqual(points[i], convertedPoints[i], "Output of matrix conversion does not match input");
                Assert.AreEqual(points[i].coords.Length, 3, "Expect a GridVector3 to have coords array of length 3");
            }
        }
    }
}
