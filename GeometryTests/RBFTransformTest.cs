using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Geometry;
using Geometry.Transforms;
using System.Diagnostics;
using MathNet;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace GeometryTests
{
    [TestClass]
    public class RBFTransformTest
    {
        [TestMethod]
        public void TestMethod1()
        {
            GridVector2[] ControlPoints = { new GridVector2(104.8445,  75.1144), 
                                                           new GridVector2(102.7622,   163.9576), 
                                                           new GridVector2(257.5437,  79.9730),
                                                           new GridVector2(258.2378,  168.1221)};

            GridVector2[] MappedPoints = { new GridVector2(68.7519, 127.1710), 
                                                           new GridVector2(87.4923,   199.3560), 
                                                           new GridVector2(263.7905, 77.8907),
                                                           new GridVector2(281.1427, 149.3817)};


            Matrix<float> BetaMatrix = Geometry.Transforms.RBFTransform.CreateBetaMatrixWithLinear(ControlPoints, 
                                                                                               Geometry.Transforms.RBFTransform.StandardBasisFunction);

            float[] SolutionMatrix = Geometry.Transforms.RBFTransform.CreateSolutionMatrixWithLinear(MappedPoints);

            //double[] Weights = GridMatrix.LinSolve(BetaMatrix, SolutionMatrix); 
            float[] Weights = RBFTransform.CalculateRBFWeights(MappedPoints, ControlPoints, RBFTransform.StandardBasisFunction);

            MappingGridVector2[] Points = new MappingGridVector2[ControlPoints.Length]; 
            for(int i = 0; i < ControlPoints.Length; i++)
            {
                Points[i] = new MappingGridVector2(ControlPoints[i], MappedPoints[i]); 
            }

            RBFTransform transform = new RBFTransform(Points, new TransformInfo());

            for (int i = 0; i < ControlPoints.Length; i++)
            {
                GridVector2 tPoint = transform.Transform(MappedPoints[i]);
                Trace.WriteLine(tPoint.ToString() + " should equal " + ControlPoints[i]);
                Debug.Assert(GridVector2.Distance(tPoint, ControlPoints[i]) < 1.0); 
            }

            for (int i = 0; i < ControlPoints.Length; i++)
            {
                GridVector2 tPoint = transform.InverseTransform(ControlPoints[i]);
                Trace.WriteLine(tPoint.ToString() + " should equal " + MappedPoints[i]);
                Debug.Assert(GridVector2.Distance(tPoint, MappedPoints[i]) < 1.0);
            }

            

        }
    }
}
