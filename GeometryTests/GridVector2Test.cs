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
            GridVector2 A = new(5, 0);
            GridVector2 B = new(2.5, 2.5);

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

            GridVector2 Origin = new(0, 0);
            GridVector2 A = new(1, 0);
            GridVector2 B = new(0, 1);
            GridVector2 C = new(-1, 0);
            GridVector2 D = new(0, -1);

            //Check angles not on the axis
            GridVector2 E = new(0.5, 0.5);
            GridVector2 F = new(-0.5, 0.5);
            GridVector2 G = new(-0.5, -0.5);
            GridVector2 H = new(0.5, -0.5);

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
            Assert.AreEqual(-Pi2, Degree90);

            Degree90 = GridVector2.ArcAngle(Origin, B, A);
            Assert.AreEqual(Pi2, Degree90);

            double Degree180 = GridVector2.ArcAngle(Origin, A, C);
            Assert.AreEqual(Math.PI, Degree180);

            double BD_Degree180 = GridVector2.ArcAngle(Origin, D, B);
            Assert.AreEqual(Math.PI, BD_Degree180);

            double Degree0 = GridVector2.Angle(Origin, A);
            Assert.AreEqual(0, Degree0);

            Degree90 = GridVector2.Angle(Origin, B);
            Assert.AreEqual(Pi2, Degree90);

            //Check angles not on the axis

            Degree90 = GridVector2.ArcAngle(Origin, E, F);
            Assert.AreEqual(-Pi2, Degree90);

            Degree90 = GridVector2.ArcAngle(Origin, F, E);
            Assert.AreEqual(Pi2, Degree90);

            Degree90 = GridVector2.ArcAngle(Origin, F, G);
            Assert.AreEqual(-Pi2, Degree90);

            Degree90 = GridVector2.ArcAngle(Origin, G, H);
            Assert.AreEqual(-Pi2, Degree90);

            //Check 45 degree angles
            double Degree45 = GridVector2.ArcAngle(Origin, E, B);
            Assert.AreEqual(-Pi4, Degree45);

            Degree45 = GridVector2.ArcAngle(Origin, E, A);
            Assert.AreEqual(Pi4, Degree45);

            Degree45 = GridVector2.ArcAngle(Origin, H, A);
            Assert.AreEqual(-Pi4, Degree45);

            //Check 135 degree angles
            double Degree135 = GridVector2.ArcAngle(Origin, E, C);
            Assert.AreEqual(-(Pi4 + Pi2), Degree135);

            Degree135 = GridVector2.ArcAngle(Origin, C, E);
            Assert.AreEqual((Pi4 + Pi2), Degree135);

            Degree135 = GridVector2.ArcAngle(Origin, G, A);
            Assert.AreEqual(-(Pi4 + Pi2), Degree135);

            Degree135 = GridVector2.ArcAngle(Origin, B, G);
            Assert.AreEqual(-(Pi4 + Pi2), Degree135);

            Degree135 = GridVector2.ArcAngle(Origin, G, B);
            Assert.AreEqual((Pi4 + Pi2), Degree135);

            //Check 180 degree angles off-axis

            Degree180 = GridVector2.ArcAngle(Origin, F, H);
            Assert.AreEqual(Math.PI, Math.Abs(Degree180));

            Degree180 = GridVector2.ArcAngle(Origin, H, F);
            Assert.AreEqual(Math.PI, Math.Abs(Degree180));

            Degree180 = GridVector2.ArcAngle(Origin, E, G);
            Assert.AreEqual(Math.PI, Math.Abs(Degree180));

            Degree180 = GridVector2.ArcAngle(Origin, G, E);
            Assert.AreEqual(Math.PI, Math.Abs(Degree180));

            //Translate the vectors slightly and ensure angles are unchanged
            GridVector2 offset = new(5, 2.5);
            Origin += offset;
            A += offset;
            B += offset;
            C += offset;

            Degree90 = GridVector2.ArcAngle(Origin, A, B);
            Assert.AreEqual(-Pi2, Degree90);

            Degree90 = GridVector2.ArcAngle(Origin, B, A);
            Assert.AreEqual(Pi2, Degree90);

            Degree180 = GridVector2.ArcAngle(Origin, A, C);
            Assert.AreEqual(Math.PI, Degree180);

            Degree0 = GridVector2.Angle(Origin, A);
            Assert.AreEqual(0, Degree0);

            Degree90 = GridVector2.Angle(Origin, B);
            Assert.AreEqual(Pi2, Degree90);
        }

        [TestMethod]
        public void TestAngle3()
        {
            double Pi4 = Math.PI / 4.0;
            //double Pi2 = Math.PI / 2.0;

            GridVector2 Origin = new(0, 0);
            GridVector2 A = new(1, 0);
            GridVector2 B = new(0, 1);
            GridVector2 C = new(-1, 0);
            GridVector2 D = new(Math.Cos(Pi4), Math.Sin(Pi4));

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
            GridVector2 A = new(0, 0);
            GridVector2 B = new(2.5, 2.5);

            GridLine line = new(A, GridVector2.UnitX);

            double angle = GridVector2.AbsArcAngle(line, B, false);

            double PI4 = Math.PI / 4;
            Assert.IsTrue(angle - Global.Epsilon < PI4 &&
                          angle + Global.Epsilon > PI4);

            double angle2 = GridVector2.AbsArcAngle(line, B, true);
            Assert.IsTrue(angle2 - Global.Epsilon < (7.0 * PI4) &&
                         angle2 + Global.Epsilon > (7.0 * PI4));

            GridLine lineY = new(A, GridVector2.UnitY);

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
            GridVector2 A = new(0, 0);

            Vector<double> v = Vector<double>.Build.Dense([A.X, A.Y, 0, 1]);

            GridVector2 Offset = new(1, 2);

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
            GridVector2 N = new(1, 2);
            GridVector2 S = new(1, 0);
            GridVector2 E = new(2, 1);
            GridVector2 W = new(0, 1);

            GridVector2 Centroid = new(1, 1);

            GridVector2[] points = [N, S, E, W];

            GridVector2 calculatedCentroid = points.Average();

            Assert.AreEqual(Centroid, calculatedCentroid);

            GridVector2[] pointsToRotate = [N, W, S, E];
            GridVector2[] rotatedPoints = [.. pointsToRotate.Rotate(Math.PI / 2, Centroid)];

            Assert.AreEqual(rotatedPoints[0], W);
            Assert.AreEqual(rotatedPoints[1], S);
            Assert.AreEqual(rotatedPoints[2], E);
            Assert.AreEqual(rotatedPoints[3], N);
        }

        [TestMethod]
        public void ToFromMatrix()
        {
            GridVector2 A = new(1, 2);
            GridVector2 B = new(1, 0);
            GridVector2 C = new(2, 1);
            GridVector2 D = new(0, 1);

            GridVector2[] points = [A, B, C, D];

            Matrix<double> m = points.ToMatrix();
            GridVector2[] convertedPoints = [.. m.ToGridVector2()];

            Assert.AreEqual(points.Length, convertedPoints.Length);

            for (int i = 0; i < points.Length; i++)
            {
                Assert.AreEqual(points[i], convertedPoints[i]);
            }
        }

        [TestMethod]
        public void AreClockwiseTest()
        {
            GridVector2 W = new(-1, 0);
            GridVector2 N = new(0, 1);
            GridVector2 E = new(1, 0);
            GridVector2 S = new(0, -1);
            GridVector2 O = GridVector2.Zero;

            GridVector2[] WNE_Points = [W, N, E];
            GridVector2[] ENW_Points = [E, N, W];

            Assert.IsTrue(WNE_Points.AreClockwise());
            Assert.AreEqual(RotationDirection.CLOCKWISE, WNE_Points.Winding());
            Assert.AreEqual(RotationDirection.CLOCKWISE, W.Winding(N, E));

            Assert.IsFalse(ENW_Points.AreClockwise());
            Assert.AreEqual(RotationDirection.COUNTERCLOCKWISE, ENW_Points.Winding());
            Assert.AreEqual(RotationDirection.COUNTERCLOCKWISE, E.Winding(N, W));

            Assert.AreNotEqual(WNE_Points.AreClockwise(), ENW_Points.AreClockwise());


            GridVector2[] NES_Points = [N, E, S];
            GridVector2[] SEN_Points = [S, E, N];

            Assert.IsTrue(NES_Points.AreClockwise());
            Assert.AreEqual(RotationDirection.CLOCKWISE, NES_Points.Winding());
            Assert.AreEqual(RotationDirection.CLOCKWISE, N.Winding(E, S));

            Assert.IsFalse(SEN_Points.AreClockwise());
            Assert.AreEqual(RotationDirection.COUNTERCLOCKWISE, SEN_Points.Winding());
            Assert.AreEqual(RotationDirection.COUNTERCLOCKWISE, S.Winding(E, N));

            Assert.AreNotEqual(NES_Points.AreClockwise(), SEN_Points.AreClockwise());


            GridVector2[] NES_Points_Translated = NES_Points.Translate(new GridVector2(10, 10));
            GridVector2[] SEN_Points_Translated = SEN_Points.Translate(new GridVector2(10, 10));

            Assert.IsTrue(NES_Points_Translated.AreClockwise());
            Assert.AreEqual(RotationDirection.CLOCKWISE, NES_Points_Translated.Winding());
            Assert.AreEqual(RotationDirection.CLOCKWISE, NES_Points_Translated[0].Winding(NES_Points_Translated[1], NES_Points_Translated[2]));

            Assert.IsFalse(SEN_Points_Translated.AreClockwise());
            Assert.AreEqual(RotationDirection.COUNTERCLOCKWISE, SEN_Points_Translated.Winding());
            Assert.AreEqual(RotationDirection.COUNTERCLOCKWISE, SEN_Points_Translated[0].Winding(SEN_Points_Translated[1], SEN_Points_Translated[2]));

            Assert.AreNotEqual(NES_Points_Translated.AreClockwise(), SEN_Points_Translated.AreClockwise());

            //Colinear
            GridVector2[] WOE_Points = [W, GridVector2.Zero, E];
            GridVector2[] SON_Points = [S, GridVector2.Zero, N];

            Assert.AreEqual(RotationDirection.COLINEAR, WOE_Points.Winding());
            Assert.AreEqual(RotationDirection.COLINEAR, W.Winding(O, E));

            Assert.AreEqual(RotationDirection.COLINEAR, SON_Points.Winding());
            Assert.AreEqual(RotationDirection.COLINEAR, S.Winding(O, N));
        }

        [TestMethod]
        public void ConvexHullTest()
        {
            GridVector2[] points = [ new(-10,-10),
                                                       new(-10, 10),
                                                       new(10,10),
                                                       new(10,-10)];

            GridVector2[] ConvexHullPoints = points.ConvexHull(out int[] original_idx);
            Assert.AreEqual(points.Length + 1, ConvexHullPoints.Length);

            GridPolygon poly = new(ConvexHullPoints);
            Assert.IsTrue(poly.BoundingBox == points.BoundingBox());

            GridVector2 Centroid = ConvexHullPoints.Average();
            Assert.IsTrue(Centroid == new GridVector2(0, 0));

            points = points.Translate(new GridVector2(-20, 20));
            ConvexHullPoints = points.ConvexHull(out original_idx);

            Assert.AreEqual(points.Length + 1, ConvexHullPoints.Length);
        }

        [TestMethod]
        public void ConvexHullTest2()
        {
            //Colinear points on the convex hull
            GridVector2[] points = [ new(-10,-10),
                new(-10, 10),
                new(10,10),
                new(10,-10),
                new(-10, 0),
                new(0, 10),
                new(0, -10),
                new(10, 0)
            ];

            GridVector2[] ConvexHullPoints = points.ConvexHull(out int[] original_idx);
            Assert.AreEqual(points.Length + 1, ConvexHullPoints.Length);

            GridPolygon poly = new(ConvexHullPoints);
            Assert.IsTrue(poly.BoundingBox == points.BoundingBox());

            GridVector2 Centroid = ConvexHullPoints.Average();
            Assert.IsTrue(Centroid == new GridVector2(0, 0));

            points = points.Translate(new GridVector2(-20, 20));
            ConvexHullPoints = points.ConvexHull(out original_idx);

            Assert.AreEqual(points.Length + 1, ConvexHullPoints.Length);
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

            GridVector2 p = new(0, 10);
            GridVector2 q = new(5, 0);
            GridVector2 r = new(10, 10);

            GridVector2 left = new(5, 5);
            GridVector2 right = new(5, -5);

            GridVector2[] pqr = [p, q, r];

            Assert.AreEqual(1, GridVector2.IsLeftSide(left, pqr));
            Assert.AreEqual(-1, GridVector2.IsLeftSide(right, pqr));

            right = new GridVector2(-5, 0);
            Assert.AreEqual(-1, GridVector2.IsLeftSide(right, pqr));

            right = new GridVector2(-5, 1);
            Assert.AreEqual(-1, GridVector2.IsLeftSide(right, pqr));

            right = new GridVector2(15, 1);
            Assert.AreEqual(-1, GridVector2.IsLeftSide(right, pqr));
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
            GridVector2 p = new(0, 0);
            GridVector2 q = new(5, 0);
            GridVector2 r = new(10, 10);

            GridVector2 left = new(1, 1);
            GridVector2 right = new(1, -1);
            GridVector2 on = q;

            GridVector2[] pqr = [p, q, r];

            Assert.AreEqual(1, GridVector2.IsLeftSide(left, pqr));
            Assert.AreEqual(-1, GridVector2.IsLeftSide(right, pqr));
            Assert.AreEqual(0, GridVector2.IsLeftSide(on, pqr));

            left = new GridVector2(6, 7);
            right = new GridVector2(-5, -1);
            on = new GridVector2(-5, 0);

            Assert.AreEqual(1, GridVector2.IsLeftSide(left, pqr));
            Assert.AreEqual(-1, GridVector2.IsLeftSide(right, pqr));
            Assert.AreEqual(0, GridVector2.IsLeftSide(on, pqr));

            left = new GridVector2(-5, 1);
            right = new GridVector2(7.5, 1);
            on = new GridVector2(7.5, 5);
            Assert.AreEqual(1, GridVector2.IsLeftSide(left, pqr));
            Assert.AreEqual(-1, GridVector2.IsLeftSide(right, pqr));
            Assert.AreEqual(0, GridVector2.IsLeftSide(on, pqr));

            left = new GridVector2(5, 2);
            right = new GridVector2(25, 1);
            on = r;
            Assert.AreEqual(1, GridVector2.IsLeftSide(left, pqr));
            Assert.AreEqual(-1, GridVector2.IsLeftSide(right, pqr));
            Assert.AreEqual(0, GridVector2.IsLeftSide(on, pqr));
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
