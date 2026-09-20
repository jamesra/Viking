using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GeometryTests
{
    [TestClass]
    public class CurveFitting
    {
        [TestMethod]
        public void ZeroInterpolationClosedCurveRepeatsFirstPoint()
        {
            GridVector2[] points =
            [
                new(0, 0),
                new(10, 0),
                new(10, 10),
                new(0, 10)
            ];

            GridVector2[] closed = points.CalculateCurvePoints(0, true);
            GridVector2[] open = points.CalculateCurvePoints(0, false);

            Assert.AreEqual(points[0], closed[closed.Length - 1]);
            Assert.AreEqual(points.Length + 1, closed.Length);
            Assert.AreEqual(points.Length, open.Length);
            Assert.AreNotEqual(open[0], open[open.Length - 1]);
        }

        [TestMethod]
        public void FitPointsWithLagrange()
        {
            GridVector2[] points = [new(0,7),
                                    new(3,5),
                                    new(0,4)];

            GridVector2[] Output = Geometry.Lagrange.FitCurve(points, 5);


            Assert.AreEqual(Output[0], points[0]);
            Assert.AreEqual(Output[2], points[1]);
            Assert.AreEqual(Output[4], points[2]);
        }
        /*
        [TestMethod]
        public void FitClosedCurvePointsWithCatmull()
        {
            GridVector2[] points = {new GridVector2(0,7),
                                    new GridVector2(1,6),
                                    new GridVector2(1,5),
                                    new GridVector2(0,4),
                                    new GridVector2(-3,5),
                                    new GridVector2(-5,7),
                                    new GridVector2(0,7)};

            GridVector2[] Output = Geometry.CatmullRom.FitCurve(points, 5, true);

            //TODO: I need a better test for recursive curve fitting that is better than a list of points that changes as I update the curve algorithm
           
            Assert.IsTrue(GridVector2.Distance(Output[0], new GridVector2(0.00, 7.00)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[1], new GridVector2(0.29, 6.85)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[2], new GridVector2(0.54, 6.66)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[3], new GridVector2(0.73, 6.45)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[4], new GridVector2(0.89, 6.22)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[5], new GridVector2(1.00, 6.00)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[6], new GridVector2(1.06, 5.81)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[7], new GridVector2(1.09, 5.60)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[8], new GridVector2(1.09, 5.40)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[9], new GridVector2(1.06, 5.19)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[10], new GridVector2(1.00, 5.00)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[11], new GridVector2(0.88, 4.77)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[12], new GridVector2(0.72, 4.52)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[13], new GridVector2(0.51, 4.29)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[14], new GridVector2(0.27, 4.11)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[15], new GridVector2(0.00, 4.00)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[16], new GridVector2(-0.50, 3.99)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[17], new GridVector2(-1.11, 4.13)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[18], new GridVector2(-1.78, 4.37)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[19], new GridVector2(-2.43, 4.68)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[20], new GridVector2(-3.00, 5.00)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[21], new GridVector2(-3.53, 5.36)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[22], new GridVector2(-4.10, 5.81)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[23], new GridVector2(-4.60, 6.27)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[24], new GridVector2(-4.93, 6.69)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[25], new GridVector2(-5.00, 7.00)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[26], new GridVector2(-4.49, 7.23)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[27], new GridVector2(-3.43, 7.34)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[28], new GridVector2(-2.13, 7.32)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[29], new GridVector2(-0.88, 7.21)) <= 0.01);
        }

        [TestMethod]
        public void FitOpenCurvePointsWithCatmull()
        {
            GridVector2[] points = {new GridVector2(0,7),
                                    new GridVector2(1,6),
                                    new GridVector2(1,5),
                                    new GridVector2(0,4),
                                    new GridVector2(-3,5),
                                    new GridVector2(-5,7)};

            GridVector2[] Output = Geometry.CatmullRom.FitCurve(points, 5, false);

            Assert.IsTrue(GridVector2.Distance(Output[0], new GridVector2(0.00, 7.00)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[1], new GridVector2(0.29, 6.85)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[2], new GridVector2(0.54, 6.66)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[3], new GridVector2(0.73, 6.45)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[4], new GridVector2(0.89, 6.22)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[5], new GridVector2(1.00, 6.00)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[6], new GridVector2(1.06, 5.81)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[7], new GridVector2(1.09, 5.60)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[8], new GridVector2(1.09, 5.40)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[9], new GridVector2(1.06, 5.19)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[10], new GridVector2(1.00, 5.00)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[11], new GridVector2(0.88, 4.77)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[12], new GridVector2(0.72, 4.52)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[13], new GridVector2(0.51, 4.29)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[14], new GridVector2(0.27, 4.11)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[15], new GridVector2(0.00, 4.00)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[16], new GridVector2(-0.50, 3.99)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[17], new GridVector2(-1.11, 4.13)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[18], new GridVector2(-1.78, 4.37)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[19], new GridVector2(-2.43, 4.68)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[20], new GridVector2(-3.00, 5.00)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[21], new GridVector2(-3.53, 5.36)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[22], new GridVector2(-4.10, 5.81)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[23], new GridVector2(-4.60, 6.27)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[24], new GridVector2(-4.93, 6.69)) <= 0.01);
            Assert.IsTrue(GridVector2.Distance(Output[25], new GridVector2(-5.00, 7.00)) <= 0.01); 
        }
        
        [TestMethod]
        public void RecursivelyFitPointsWithCatmull()
        {

        }
        */

        [TestMethod]
        public void RibbonTangentsClampSquareCornersBelowMiterLimit()
        {
            GridVector2[] square =
            [
                new(0, 0),
                new(10, 0),
                new(10, 10),
                new(0, 10),
                new(0, 0)
            ];

            double[] tangents = GridVector2.CalculateRibbonTangents(square, closed: true);
            AssertAdjacentTangentStepsWithinLimit(tangents);
        }

        [TestMethod]
        public void RibbonTangentsUnwrapCircleMonotonically()
        {
            const int samples = 32;
            GridVector2[] circle = new GridVector2[samples + 1];
            for (int i = 0; i < samples; i++)
            {
                double t = 2 * Math.PI * i / samples;
                circle[i] = new GridVector2(Math.Cos(t), Math.Sin(t));
            }

            circle[samples] = circle[0];

            double[] tangents = GridVector2.CalculateRibbonTangents(circle, closed: true);
            AssertAdjacentTangentStepsWithinLimit(tangents);

            for (int i = 1; i < tangents.Length; i++)
            {
                Assert.IsTrue(tangents[i] > tangents[i - 1],
                    $"CCW circle tangent[{i}]={tangents[i]} should increase from {tangents[i - 1]} after unwrap");
            }
        }

        private static void AssertAdjacentTangentStepsWithinLimit(double[] tangents)
        {
            for (int i = 1; i < tangents.Length; i++)
            {
                double delta = Math.Abs(tangents[i] - tangents[i - 1]);
                Assert.IsTrue(delta <= GridVector2.DefaultMaxRibbonTangentDelta + 1e-9,
                    $"Adjacent tangent step {delta} at {i} exceeds {GridVector2.DefaultMaxRibbonTangentDelta}");
            }
        }
    }
}
