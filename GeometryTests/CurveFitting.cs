using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class CurveFitting
    {
        [TestMethod]
        public void FitPointsWithLagrange()
        {
            Vector2[] points = [new(0,7),
                                    new(3,5),
                                    new(0,4)];

            Vector2[] Output = Geometry.Lagrange.FitCurve(points, 5);


            Assert.AreEqual(Output[0], points[0]);
            Assert.AreEqual(Output[2], points[1]);
            Assert.AreEqual(Output[4], points[2]);
        }
        /*
        [TestMethod]
        public void FitClosedCurvePointsWithCatmull()
        {
            Vector2[] points = {new Vector2(0,7),
                                    new Vector2(1,6),
                                    new Vector2(1,5),
                                    new Vector2(0,4),
                                    new Vector2(-3,5),
                                    new Vector2(-5,7),
                                    new Vector2(0,7)};

            Vector2[] Output = Geometry.CatmullRom.FitCurve(points, 5, true);

            //TODO: I need a better test for recursive curve fitting that is better than a list of points that changes as I update the curve algorithm
           
            Assert.IsTrue(Vector2.Distance(Output[0], new Vector2(0.00, 7.00)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[1], new Vector2(0.29, 6.85)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[2], new Vector2(0.54, 6.66)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[3], new Vector2(0.73, 6.45)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[4], new Vector2(0.89, 6.22)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[5], new Vector2(1.00, 6.00)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[6], new Vector2(1.06, 5.81)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[7], new Vector2(1.09, 5.60)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[8], new Vector2(1.09, 5.40)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[9], new Vector2(1.06, 5.19)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[10], new Vector2(1.00, 5.00)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[11], new Vector2(0.88, 4.77)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[12], new Vector2(0.72, 4.52)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[13], new Vector2(0.51, 4.29)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[14], new Vector2(0.27, 4.11)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[15], new Vector2(0.00, 4.00)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[16], new Vector2(-0.50, 3.99)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[17], new Vector2(-1.11, 4.13)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[18], new Vector2(-1.78, 4.37)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[19], new Vector2(-2.43, 4.68)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[20], new Vector2(-3.00, 5.00)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[21], new Vector2(-3.53, 5.36)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[22], new Vector2(-4.10, 5.81)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[23], new Vector2(-4.60, 6.27)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[24], new Vector2(-4.93, 6.69)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[25], new Vector2(-5.00, 7.00)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[26], new Vector2(-4.49, 7.23)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[27], new Vector2(-3.43, 7.34)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[28], new Vector2(-2.13, 7.32)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[29], new Vector2(-0.88, 7.21)) <= 0.01);
        }

        [TestMethod]
        public void FitOpenCurvePointsWithCatmull()
        {
            Vector2[] points = {new Vector2(0,7),
                                    new Vector2(1,6),
                                    new Vector2(1,5),
                                    new Vector2(0,4),
                                    new Vector2(-3,5),
                                    new Vector2(-5,7)};

            Vector2[] Output = Geometry.CatmullRom.FitCurve(points, 5, false);

            Assert.IsTrue(Vector2.Distance(Output[0], new Vector2(0.00, 7.00)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[1], new Vector2(0.29, 6.85)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[2], new Vector2(0.54, 6.66)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[3], new Vector2(0.73, 6.45)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[4], new Vector2(0.89, 6.22)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[5], new Vector2(1.00, 6.00)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[6], new Vector2(1.06, 5.81)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[7], new Vector2(1.09, 5.60)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[8], new Vector2(1.09, 5.40)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[9], new Vector2(1.06, 5.19)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[10], new Vector2(1.00, 5.00)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[11], new Vector2(0.88, 4.77)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[12], new Vector2(0.72, 4.52)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[13], new Vector2(0.51, 4.29)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[14], new Vector2(0.27, 4.11)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[15], new Vector2(0.00, 4.00)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[16], new Vector2(-0.50, 3.99)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[17], new Vector2(-1.11, 4.13)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[18], new Vector2(-1.78, 4.37)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[19], new Vector2(-2.43, 4.68)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[20], new Vector2(-3.00, 5.00)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[21], new Vector2(-3.53, 5.36)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[22], new Vector2(-4.10, 5.81)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[23], new Vector2(-4.60, 6.27)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[24], new Vector2(-4.93, 6.69)) <= 0.01);
            Assert.IsTrue(Vector2.Distance(Output[25], new Vector2(-5.00, 7.00)) <= 0.01); 
        }
        
        [TestMethod]
        public void RecursivelyFitPointsWithCatmull()
        {

        }
        */
    }
}
