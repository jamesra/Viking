using Geometry;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

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

            double angle = GridVector2.Angle(A, B);
            Assert.IsTrue(angle - Global.Epsilon < (3.0 * PI4) &&
                         angle + Global.Epsilon > (3.0 * PI4));

            A = new GridVector2(5, 0);
            B = new GridVector2(2.5, -2.5);

            angle = GridVector2.Angle(A, B);
            Assert.IsTrue(angle - Global.Epsilon < (-3.0 * PI4) &&
                         angle + Global.Epsilon > (-3.0 * PI4));

            //
            // TODO: Add test logic	here
            //
        }

        [TestMethod]
        public void TestAngle2()
        {
            const double Pi4 = Math.PI / 4.0;
            const double Pi2 = Math.PI / 2.0;

            GridVector2 Origin = new GridVector2(0, 0);
            GridVector2 A = new GridVector2(1, 0);
            GridVector2 B = new GridVector2(0, 1);
            GridVector2 C = new GridVector2(-1, 0);
            GridVector2 D = new GridVector2(0, -1);

            //Check angles not on the axis
            GridVector2 E = new GridVector2(0.5, 0.5);
            GridVector2 F = new GridVector2(-0.5, 0.5);
            GridVector2 G = new GridVector2(-0.5, -0.5);
            GridVector2 H = new GridVector2(0.5, -0.5);

            //     X = -1          X = 1
            //
            // Y = 1         B
            //          F    |    E
            //               |
            //       C---------------A
            //               |
            //          G    |    H
            // Y = -1        D
            //

            //Start by testing angles on the axis
            double Degree90 = GridVector2.ArcAngle(Origin, A, B);
            Assert.AreEqual(Degree90, -Pi2);

            Degree90 = GridVector2.ArcAngle(Origin, B, A);
            Assert.AreEqual(Degree90, Pi2);

            double Degree180 = GridVector2.ArcAngle(Origin, A, C);
            Assert.AreEqual(Degree180, Math.PI);

            double BD_Degree180 = GridVector2.ArcAngle(Origin, D, B);
            Assert.AreEqual(BD_Degree180, Math.PI);

            double Degree0 = GridVector2.Angle(Origin, A);
            Assert.AreEqual(Degree0, 0);

            Degree90 = GridVector2.Angle(Origin, B);
            Assert.AreEqual(Degree90, Pi2);

            //Check angles not on the axis

            Degree90 = GridVector2.ArcAngle(Origin, E, F);
            Assert.AreEqual(Degree90, -Pi2);

            Degree90 = GridVector2.ArcAngle(Origin, F, E);
            Assert.AreEqual(Degree90, Pi2);

            Degree90 = GridVector2.ArcAngle(Origin, F, G);
            Assert.AreEqual(Degree90, -Pi2);

            Degree90 = GridVector2.ArcAngle(Origin, G, H);
            Assert.AreEqual(Degree90, -Pi2);

            //Check 45 degree angles
            double Degree45 = GridVector2.ArcAngle(Origin, E, B);
            Assert.AreEqual(Degree45, -Pi4);

            Degree45 = GridVector2.ArcAngle(Origin, E, A);
            Assert.AreEqual(Degree45, Pi4);

            Degree45 = GridVector2.ArcAngle(Origin, H, A);
            Assert.AreEqual(Degree45, -Pi4);

            //Check 135 degree angles
            double Degree135 = GridVector2.ArcAngle(Origin, E, C);
            Assert.AreEqual(Degree135, -(Pi4 + Pi2));

            Degree135 = GridVector2.ArcAngle(Origin, C, E);
            Assert.AreEqual(Degree135, (Pi4 + Pi2));

            Degree135 = GridVector2.ArcAngle(Origin, G, A);
            Assert.AreEqual(Degree135, -(Pi4 + Pi2));

            Degree135 = GridVector2.ArcAngle(Origin, B, G);
            Assert.AreEqual(Degree135, -(Pi4 + Pi2));

            Degree135 = GridVector2.ArcAngle(Origin, G, B);
            Assert.AreEqual(Degree135, (Pi4 + Pi2));

            //Check 180 degree angles off-axis

            Degree180 = GridVector2.ArcAngle(Origin, F, H);
            Assert.AreEqual(Math.Abs(Degree180), Math.PI);

            Degree180 = GridVector2.ArcAngle(Origin, H, F);
            Assert.AreEqual(Math.Abs(Degree180), Math.PI);

            Degree180 = GridVector2.ArcAngle(Origin, E, G);
            Assert.AreEqual(Math.Abs(Degree180), Math.PI);

            Degree180 = GridVector2.ArcAngle(Origin, G, E);
            Assert.AreEqual(Math.Abs(Degree180), Math.PI);

            //Translate the vectors slightly and ensure angles are unchanged
            GridVector2 offset = new GridVector2(5, 2.5);
            Origin += offset;
            A += offset;
            B += offset;
            C += offset;

            Degree90 = GridVector2.ArcAngle(Origin, A, B);
            Assert.AreEqual(Degree90, -Pi2);

            Degree90 = GridVector2.ArcAngle(Origin, B, A);
            Assert.AreEqual(Degree90, Pi2);

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

            //Measure from D to A, which is on X-Axis
            double degree45 = GridVector2.ArcAngle(Origin, D, A);

            //Measure angle to D from origin, which is also on X-Axis
            double result = GridVector2.Angle(Origin, D);

            Assert.AreEqual(result, degree45);
            Assert.AreEqual(degree45, Pi4);
            Assert.AreEqual(result, Pi4);

        }

        [TestMethod]
        public void TestAbsAngle()
        {
            GridVector2 A = new GridVector2(0, 0);
            GridVector2 B = new GridVector2(2.5, 2.5);

            GridLine line = new GridLine(A, GridVector2.UnitX);

            double angle = GridVector2.AbsArcAngle(line, B, false);

            double PI4 = Math.PI / 4;
            Assert.IsTrue(angle - Global.Epsilon < PI4 &&
                          angle + Global.Epsilon > PI4);

            double angle2 = GridVector2.AbsArcAngle(line, B, true);
            Assert.IsTrue(angle2 - Global.Epsilon < (7.0 * PI4) &&
                         angle2 + Global.Epsilon > (7.0 * PI4));

            GridLine lineY = new GridLine(A, GridVector2.UnitY);

            double angle3 = GridVector2.AbsArcAngle(lineY, B, true);

            Assert.IsTrue(angle3 - Global.Epsilon < PI4 &&
                          angle3 + Global.Epsilon > PI4);

            double angle4 = GridVector2.AbsArcAngle(lineY, B, false);
            Assert.IsTrue(angle4 - Global.Epsilon < (7.0 * PI4) &&
                          angle4 + Global.Epsilon > (7.0 * PI4));
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

            Assert.AreEqual(Centroid, calculatedCentroid);

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

            for (int i = 0; i < points.Length; i++)
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
            GridVector2 O = GridVector2.Zero;

            GridVector2[] WNE_Points = new GridVector2[] { W, N, E };
            GridVector2[] ENW_Points = new GridVector2[] { E, N, W };

            Assert.IsTrue(WNE_Points.AreClockwise());
            Assert.IsTrue(WNE_Points.Winding() == RotationDirection.CLOCKWISE);
            Assert.IsTrue(W.Winding(N,E) == RotationDirection.CLOCKWISE);

            Assert.IsFalse(ENW_Points.AreClockwise());
            Assert.IsTrue(ENW_Points.Winding() == RotationDirection.COUNTERCLOCKWISE);
            Assert.IsTrue(E.Winding(N, W) == RotationDirection.COUNTERCLOCKWISE);
            
            Assert.AreNotEqual(WNE_Points.AreClockwise(), ENW_Points.AreClockwise());


            GridVector2[] NES_Points = new GridVector2[] { N, E, S };
            GridVector2[] SEN_Points = new GridVector2[] { S, E, N };

            Assert.IsTrue(NES_Points.AreClockwise());
            Assert.IsTrue(NES_Points.Winding() == RotationDirection.CLOCKWISE);
            Assert.IsTrue(N.Winding(E, S) == RotationDirection.CLOCKWISE);

            Assert.IsFalse(SEN_Points.AreClockwise());
            Assert.IsTrue(SEN_Points.Winding() == RotationDirection.COUNTERCLOCKWISE);
            Assert.IsTrue(S.Winding(E, N) == RotationDirection.COUNTERCLOCKWISE);

            Assert.AreNotEqual(NES_Points.AreClockwise(), SEN_Points.AreClockwise());


            GridVector2[] NES_Points_Translated = NES_Points.Translate(new GridVector2(10, 10));
            GridVector2[] SEN_Points_Translated = SEN_Points.Translate(new GridVector2(10, 10));

            Assert.IsTrue(NES_Points_Translated.AreClockwise());
            Assert.IsTrue(NES_Points_Translated.Winding() == RotationDirection.CLOCKWISE);
            Assert.IsTrue(NES_Points_Translated[0].Winding(NES_Points_Translated[1], NES_Points_Translated[2]) == RotationDirection.CLOCKWISE);

            Assert.IsFalse(SEN_Points_Translated.AreClockwise());
            Assert.IsTrue(SEN_Points_Translated.Winding() == RotationDirection.COUNTERCLOCKWISE);
            Assert.IsTrue(SEN_Points_Translated[0].Winding(SEN_Points_Translated[1], SEN_Points_Translated[2]) == RotationDirection.COUNTERCLOCKWISE);

            Assert.AreNotEqual(NES_Points_Translated.AreClockwise(), SEN_Points_Translated.AreClockwise());

            //Colinear
            GridVector2[] WOE_Points = new GridVector2[] { W, GridVector2.Zero, E };
            GridVector2[] SON_Points = new GridVector2[] { S, GridVector2.Zero, N };

            Assert.IsTrue(WOE_Points.Winding() == RotationDirection.COLINEAR);
            Assert.IsTrue(W.Winding(O, E) == RotationDirection.COLINEAR);

            Assert.IsTrue(SON_Points.Winding() == RotationDirection.COLINEAR);
            Assert.IsTrue(S.Winding(O, N) == RotationDirection.COLINEAR);
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
            Assert.IsTrue(ConvexHullPoints.Length == points.Length + 1);

            GridPolygon poly = new GridPolygon(ConvexHullPoints);
            Assert.IsTrue(poly.BoundingBox == points.BoundingBox());

            GridVector2 Centroid = ConvexHullPoints.Average();
            Assert.IsTrue(Centroid == new GridVector2(0, 0));

            points = points.Translate(new GridVector2(-20, 20));
            ConvexHullPoints = points.ConvexHull(out original_idx);

            Assert.IsTrue(ConvexHullPoints.Length == points.Length + 1);
        }

        [TestMethod]
        public void ConvexHullTest2()
        {
            //Colinear points on the convex hull
            GridVector2[] points = new GridVector2[] { new GridVector2(-10,-10),
                new GridVector2(-10, 10),
                new GridVector2(10,10),
                new GridVector2(10,-10),
                new GridVector2(-10, 0),
                new GridVector2(0, 10),
                new GridVector2(0, -10),
                new GridVector2(10, 0)
            };

            int[] original_idx;
            GridVector2[] ConvexHullPoints = points.ConvexHull(out original_idx);
            Assert.IsTrue(ConvexHullPoints.Length == points.Length + 1);

            GridPolygon poly = new GridPolygon(ConvexHullPoints);
            Assert.IsTrue(poly.BoundingBox == points.BoundingBox());

            GridVector2 Centroid = ConvexHullPoints.Average();
            Assert.IsTrue(Centroid == new GridVector2(0, 0));

            points = points.Translate(new GridVector2(-20, 20));
            ConvexHullPoints = points.ConvexHull(out original_idx);

            Assert.IsTrue(ConvexHullPoints.Length == points.Length+1);
        }


        [TestMethod]
        public void TestIsLeft()
        {
            //Is a point to the left when standing at A looking at B 

            //
            //    p     r
            //     \   /
            //      \ /
            //       q

            GridVector2 p = new GridVector2(0, 10);
            GridVector2 q = new GridVector2(5, 0);
            GridVector2 r = new GridVector2(10, 10);

            GridVector2 left = new GridVector2(5, 5);
            GridVector2 right = new GridVector2(5, -5);

            GridVector2[] pqr = new GridVector2[] { p, q, r };

            Assert.AreEqual(GridVector2.IsLeftSide(left, pqr), 1);
            Assert.AreEqual(GridVector2.IsLeftSide(right, pqr), -1);

            right = new GridVector2(-5, 0);
            Assert.AreEqual(GridVector2.IsLeftSide(right, pqr), -1);

            right = new GridVector2(-5, 1);
            Assert.AreEqual(GridVector2.IsLeftSide(right, pqr), -1);

            right = new GridVector2(15, 1);
            Assert.AreEqual(GridVector2.IsLeftSide(right, pqr), -1);
        }


        [TestMethod]
        public void TestIsLeft2()
        {
            //Is a point to the left of both line segments pq & qr
            //
            //         r
            //        /
            //       /
            // p----q
            //
            GridVector2 p = new GridVector2(0, 0);
            GridVector2 q = new GridVector2(5, 0);
            GridVector2 r = new GridVector2(10, 10);

            GridVector2 left = new GridVector2(1, 1);
            GridVector2 right = new GridVector2(1, -1);
            GridVector2 on = q;

            GridVector2[] pqr = new GridVector2[] { p, q, r };

            Assert.AreEqual(GridVector2.IsLeftSide(left, pqr), 1);
            Assert.AreEqual(GridVector2.IsLeftSide(right, pqr), -1);
            Assert.AreEqual(GridVector2.IsLeftSide(on, pqr), 0);

            left = new GridVector2(6, 7);
            right = new GridVector2(-5, -1);
            on = new GridVector2(-5, 0);

            Assert.AreEqual(GridVector2.IsLeftSide(left, pqr), 1);
            Assert.AreEqual(GridVector2.IsLeftSide(right, pqr), -1);
            Assert.AreEqual(GridVector2.IsLeftSide(on, pqr), 0);

            left = new GridVector2(-5, 1);
            right = new GridVector2(7.5, 1);
            on = new GridVector2(7.5, 5);
            Assert.AreEqual(GridVector2.IsLeftSide(left, pqr), 1);
            Assert.AreEqual(GridVector2.IsLeftSide(right, pqr), -1);
            Assert.AreEqual(GridVector2.IsLeftSide(on, pqr), 0);

            left = new GridVector2(5, 2);
            right = new GridVector2(25, 1);
            on = r;
            Assert.AreEqual(GridVector2.IsLeftSide(left, pqr), 1);
            Assert.AreEqual(GridVector2.IsLeftSide(right, pqr), -1);
            Assert.AreEqual(GridVector2.IsLeftSide(on, pqr), 0);
        }
        /*
        static bool IsLeftTest(GridVector2 t, GridVector2[] pqr)
        {
            int result = GridVector2.IsLeftSide(t, pqr);
        }

        [TestMethod]
        public void FsCheckIsLeft()
        {
            //My first experimental foray into fscheck
            //Is a point to the left when standing at p looking at q
            //
            //         r
            //        /
            //       /
            // p----q
            //
            //Start with cases where the point is always left of the line

            Func<GridVector2, GridVector2[], int> leftIsLeft = GridVector2.IsLeftSide;

            Prop.GivenleftIsLeft.QuickCheck();

         }
         */
    }
}
