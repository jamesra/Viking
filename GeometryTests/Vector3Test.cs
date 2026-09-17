using Geometry;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UtilitiesTests
{
    /// <summary>
    /// Summary description for Vector3Test
    /// </summary>
    [TestClass]
    public class Vector3Test
    {
        public Vector3Test()
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
            get => testContextInstance;
            set => testContextInstance = value;
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
            Vector3 A = new(5, 0, 0);
            Vector3 B = new(2.5, 2.5, 0);

            double PI4 = Math.PI / 4;

            double angle = Vector3.Angle(A, B);
            Assert.IsTrue(angle - Global.Epsilon < (1.0 * PI4) &&
                         angle + Global.Epsilon > (1.0 * PI4));

            A = new Vector3(5, 0, 0);
            B = new Vector3(2.5, -2.5, 0);

            angle = Vector3.Angle(A, B);
            Assert.IsTrue(angle - Global.Epsilon < (1.0 * PI4) &&
                         angle + Global.Epsilon > (1.0 * PI4));

            //
            // TODO: Add test logic	here
            //
        }
        /*
        [TestMethod]
        public void TestAngle2()
        {
            double Pi4 = Math.PI / 4.0;
            double Pi2 = Math.PI / 2.0;

            Vector3 Origin = new Vector3(0, 0, 0);
            Vector3 A = new Vector3(1, 0, 0);
            Vector3 B = new Vector3(0, 1, 0);
            Vector3 C = new Vector3(-1, 0, 0);

            double Degree90 = Vector3.ArcAngle(Origin, A, B);
            Assert.AreEqual(Degree90, Pi2);

            Degree90 = Vector3.ArcAngle(Origin, B, A);
            Assert.AreEqual(Degree90, -Pi2);

            double Degree180 = Vector3.ArcAngle(Origin, A, C);
            Assert.AreEqual(Degree180, Math.PI);

            double Degree0 = Vector3.Angle(Origin, A);
            Assert.AreEqual(Degree0, 0);

            Degree90 = Vector3.Angle(Origin, B);
            Assert.AreEqual(Degree90, Pi2);
        }
        
        [TestMethod]
        public void TestAngle3()
        {
            double Pi4 = Math.PI / 4.0;
            double Pi2 = Math.PI / 2.0;

            Vector3 Origin = new Vector3(0, 0, 0);
            Vector3 A = new Vector3(1, 0, 0);
            Vector3 B = new Vector3(0, 1, 0);
            Vector3 C = new Vector3(-1, 0, 0);
            Vector3 D = new Vector3(Math.Cos(Pi4), Math.Sin(Pi4), 0);

            double degree45 = Vector3.ArcAngle(Origin, A, D);
            double result = Vector3.Angle(Origin, D);
            Assert.AreEqual(degree45, Pi4);
            Assert.AreEqual(result, Pi4);
        }*/

        [TestMethod]
        public void TestArcAngle()
        {
            Vector3 O = new(0, 0, 0);
            Vector3 A = new(5, 0, 0);
            Vector3 B = new(2.5, 2.5, 0);

            double PI4 = Math.PI / 4;

            double angle = Vector3.ArcAngle(O, A, B);
            Assert.IsTrue(angle - Global.Epsilon < (1.0 * PI4) &&
                         angle + Global.Epsilon > (1.0 * PI4));

            A = new Vector3(5, 0, 0);
            B = new Vector3(-2.5, -2.5, 0);

            angle = Vector3.ArcAngle(O, A, B);
            Assert.IsTrue(angle - Global.Epsilon < (3.0 * PI4) &&
                         angle + Global.Epsilon > (3.0 * PI4));

            //
            // TODO: Add test logic	here
            //
        }

        [TestMethod]
        public void TestTranslate()
        {
            Vector3 A = new(0, 0, 0);

            Vector<double> v = Vector<double>.Build.Dense([A.X, A.Y, 0, 1]);

            Vector3 Offset = new(1, 2, 0);

            Matrix<double> translationMatrix = GeometryMathNetNumerics.CreateTranslationMatrix(Offset);
            Vector<double> translated = translationMatrix * v;

            Vector3 translatedPoint = translated.ToVector3();
            Assert.AreEqual(translatedPoint, A + Offset);

            Matrix<double> p = A.ToMatrix();
            Matrix<double> translatedMatrix = translationMatrix * p;

            ICollection<Vector3> translatedPoints = translatedMatrix.ToVector3();
            Assert.AreEqual(translatedPoints.First(), A + Offset);
        }

        [TestMethod]
        public void ToFromMatrix()
        {
            Vector3 A = new(1, 2, 0);
            Vector3 B = new(1, 0, 0);
            Vector3 C = new(2, 1, 0);
            Vector3 D = new(0, 1, 0);

            Vector3[] points = [A, B, C, D];

            Matrix<double> m = points.ToMatrix();
            Vector3[] convertedPoints = [.. m.ToVector3()];

            Assert.AreEqual(points.Length, convertedPoints.Length);

            for (int i = 0; i < points.Length; i++)
            {
                Assert.AreEqual(points[i], convertedPoints[i], "Output of matrix conversion does not match input");
                Assert.AreEqual(3, points[i].Coords.Length, "Expect a Vector3 to have coords array of length 3");
            }
        }
    }
}
