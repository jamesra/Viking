using System;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Geometry;

namespace GeometryTests
{
    [TestClass]
    public class CurveFitting
    {
        [TestMethod]
        public void FitPointsWithLagrange()
        {
            GridVector2[] points = {new GridVector2(0,7),
                                    new GridVector2(3,5),
                                    new GridVector2(0,4)};

            GridVector2[] Output = Geometry.Lagrange.FitCurve(points, 5);


            Assert.AreEqual(Output[0], points[0]);
            Assert.AreEqual(Output[2], points[1]);
            Assert.AreEqual(Output[4], points[2]);
        }

        [TestMethod]
        public void FitPointsWithCatmull()
        {
            GridVector2[] points = {new GridVector2(0,7),
                                    new GridVector2(1,6),
                                    new GridVector2(1,5),
                                    new GridVector2(0,4),
                                    new GridVector2(-3,5),
                                    new GridVector2(-5,7)};

            GridVector2[] Output = Geometry.CatmullRom.FitCurve(points, 5, true);

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
        public void RecursivelyFitPointsWithCatmull()
        {

        }
    }
}
