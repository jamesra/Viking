using System;
using System.Diagnostics; 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Geometry;
using System.Runtime.Serialization;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;

namespace Geometry.Transforms
{
    [Serializable]
    public class RBFTransform : ReferencePointBasedTransform
    {
        public delegate double BasisFunctionDelegate(double distance);
        BasisFunctionDelegate BasisFunction = new BasisFunctionDelegate(StandardBasisFunction);
          
        private double[] _ControlToMappedSpaceWeights = null; 
        private double[] ControlToMappedSpaceWeights
        {
            get
            {
                if (_ControlToMappedSpaceWeights == null)
                {
                    lock (this)
                    {
                        if (_ControlToMappedSpaceWeights != null)
                            return _ControlToMappedSpaceWeights; 

                        //double[,] BetaMatrixMappedToControl = CreateBetaMatrixWithLinear(MappingGridVector2.ControlPoints(this.MapPoints), this.BasisFunction);
                        //double[] ResultMatrixMappedToControl = CreateSolutionMatrixWithLinear(MappingGridVector2.MappedPoints(this.MapPoints));
                        //_ControlToMappedSpaceWeights = GridMatrix.LinSolve(BetaMatrixMappedToControl, ResultMatrixMappedToControl);

                        _ControlToMappedSpaceWeights = CalculateRBFWeights(MappingGridVector2.ControlPoints(this.MapPoints),
                                                                           MappingGridVector2.MappedPoints(this.MapPoints),
                                                                           null); 
                    }
                }

                return _ControlToMappedSpaceWeights; 
            }
        }

        private double[] _MappedToControlSpaceWeights = null;
        private double[] MappedToControlSpaceWeights
        {
            get
            {
                if (_MappedToControlSpaceWeights == null)
                {
                    lock (this)
                    {
                        if (_MappedToControlSpaceWeights != null)
                            return _MappedToControlSpaceWeights; 

                        //double[,] BetaMatrixControlToMapped = CreateBetaMatrixWithLinear(MappingGridVector2.MappedPoints(this.MapPoints), this.BasisFunction);
                        //double[] ResultMatrixControlToMapped = CreateSolutionMatrixWithLinear(MappingGridVector2.ControlPoints(this.MapPoints));
                        //_MappedToControlSpaceWeights = GridMatrix.LinSolve(BetaMatrixControlToMapped, ResultMatrixControlToMapped);
                        _MappedToControlSpaceWeights = CalculateRBFWeights(MappingGridVector2.MappedPoints(this.MapPoints),
                                                                           MappingGridVector2.ControlPoints(this.MapPoints),
                                                                           null); 
                    }
                }

                return _MappedToControlSpaceWeights;
            }
        }

        public static double StandardBasisFunction(double distance)
        {
            if (distance == 0)
                return 0; 

            return distance * distance * Math.Log(distance); 
        }

        public RBFTransform(MappingGridVector2[] points, TransformInfo info)
            : base(points, info)
        {
        }

        protected RBFTransform(SerializationInfo info, StreamingContext context) : base(info, context)
        { 
            if (info == null)
                throw new ArgumentNullException();

            _ControlToMappedSpaceWeights = info.GetValue("_ControlToMappedSpaceWeights", typeof(double[])) as double[];
            _MappedToControlSpaceWeights = info.GetValue("_MappedToControlSpaceWeights", typeof(double[])) as double[];
        }
    

        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("_ControlToMappedSpaceWeights", _ControlToMappedSpaceWeights);
            info.AddValue("_MappedToControlSpaceWeights", _MappedToControlSpaceWeights);  
        }

        public override bool CanTransform(GridVector2 Point)
        {
            return true;
        }

        public static GridVector2 Transform(GridVector2 Point, double[] Weights, GridVector2[] ControlPoints, BasisFunctionDelegate BasisFunction)
        {
            if (ControlPoints == null || Weights == null || BasisFunction == null)
                throw new ArgumentNullException();

            int nPoints = ControlPoints.Length;
            double[] distances = new double[nPoints];
            double[] functionValues = new double[nPoints];

            double WeightSumX = 0;
            double WeightSumY = 0;
            
            for (int i = 0; i < distances.Length; i++)
            {
                double dist = GridVector2.Distance(ControlPoints[i], Point);
                double funcVal = BasisFunction(dist);
                distances[i] = dist;
                functionValues[i] = funcVal;

                WeightSumX = WeightSumX + (Weights[i] * funcVal);
                WeightSumY = WeightSumY + (Weights[i + 3 + nPoints] * funcVal);
            }

            double X = WeightSumX + (Point.Y * Weights[nPoints]) + (Point.X * Weights[nPoints + 1]) + Weights[nPoints + 2];
            double Y = WeightSumY + (Point.Y * Weights[nPoints + 3 + nPoints]) + (Point.X * Weights[nPoints + nPoints + 3 + 1]) + Weights[nPoints + nPoints + 3 + 2];

            return new GridVector2(X, Y); 
        }

        public override GridVector2 Transform(GridVector2 Point)
        {
            return RBFTransform.Transform(Point, MappedToControlSpaceWeights, MappingGridVector2.MappedPoints(this.MapPoints), this.BasisFunction); 
        }

        public override GridVector2[] Transform(GridVector2[] Points)
        {
            var Output = from Point in Points.AsParallel().AsOrdered() select RBFTransform.Transform(Point, MappedToControlSpaceWeights, MappingGridVector2.MappedPoints(this.MapPoints), this.BasisFunction);
            return Output.ToArray();
        }

        public override bool TryTransform(GridVector2 Point, out GridVector2 v)
        {
            v = Transform(Point);
            return true;
        }
        public override bool[] TryTransform(GridVector2[] Points, out GridVector2[] Output)
        {
            Output = this.Transform(Points);
            return Points.Select(p => true).ToArray();
        }

        public override bool CanInverseTransform(GridVector2 Point)
        {
            return true; 
        }

        public override GridVector2 InverseTransform(GridVector2 Point)
        {
            return RBFTransform.Transform(Point, ControlToMappedSpaceWeights, MappingGridVector2.ControlPoints(this.MapPoints), this.BasisFunction); 
        }

        public override GridVector2[] InverseTransform(GridVector2[] Points)
        {
            var Output = from Point in Points.AsParallel().AsOrdered() select RBFTransform.Transform(Point, ControlToMappedSpaceWeights, MappingGridVector2.ControlPoints(this.MapPoints), this.BasisFunction);
            return Output.ToArray();
        }

        public override bool TryInverseTransform(GridVector2 Point, out GridVector2 v)
        {
            v = InverseTransform(Point);
            return true;
        }

        public override bool[] TryInverseTransform(GridVector2[] Points, out GridVector2[] Output)
        {
            Output = this.InverseTransform(Points);
            return Points.Select(p => true).ToArray();
        }

        public static double[] CreateSolutionMatrixWithLinear(GridVector2[] ControlPoints)
        {
            if (ControlPoints == null)
                throw new ArgumentNullException();

            int NumPts = ControlPoints.Length;

            double[] ResultMatrix = new double[(NumPts + 3) * 2];

            for (int i = 0; i < NumPts; i++)
            {
                ResultMatrix[i + 3] = ControlPoints[i].X;
                ResultMatrix[(i + 3) + (NumPts+3)] = ControlPoints[i].Y; 
            }

            return ResultMatrix; 
        }

        public static double[] CreateSolutionMatrix_X_WithLinear(GridVector2[] ControlPoints)
        {
            if (ControlPoints == null)
                throw new ArgumentNullException();

            int NumPts = ControlPoints.Length;

            double[] ResultMatrix = new double[(NumPts + 3)];

            for (int i = 0; i < NumPts; i++)
            {
                ResultMatrix[i + 3] = ControlPoints[i].X;
            }

            return ResultMatrix;
        }

        public static double[] CreateSolutionMatrix_Y_WithLinear(GridVector2[] ControlPoints)
        {
            if (ControlPoints == null)
                throw new ArgumentNullException();

            int NumPts = ControlPoints.Length;

            double[] ResultMatrix = new double[(NumPts + 3)];

            for (int i = 0; i < NumPts; i++)
            {
                ResultMatrix[i + 3] = ControlPoints[i].Y;
            }

            return ResultMatrix;
        }

        /// <summary>
        /// Populates matrix by applying basis function to control points and filling a matrix [B 0; 0 B];
        /// </summary>
        /// <param name="ControlPoints"></param>
        /// <param name="BasisFunction"></param>
        /// <returns></returns>
        public static double[,] CreateBetaMatrixWithLinear(GridVector2[] ControlPoints, BasisFunctionDelegate BasisFunction)
        {
            if (ControlPoints == null)
                throw new ArgumentNullException(); 

            int NumPts = ControlPoints.Length;

            double[,] BetaMatrix = new double[NumPts+3, NumPts+3];

            for (int iRow = 3; iRow < NumPts + 3; iRow++)
            {
                int iPointA = iRow - 3;

                for (int iCol = iPointA+1; iCol < NumPts; iCol++)
                {
                    int iPointB = iCol;
                    double value;
                    if (BasisFunction != null)
                    {
                        double dist = GridVector2.Distance(ControlPoints[iPointA], ControlPoints[iPointB]);
                        value = BasisFunction(dist);
                    }
                    else
                    {
                        double dist_squared = GridVector2.DistanceSquared(ControlPoints[iPointA], ControlPoints[iPointB]);
                        value = dist_squared * (Math.Log(dist_squared) / 2.0); // = distance^2 * log(distance).
                    }
                    BetaMatrix[iRow, iCol] = value;
                    BetaMatrix[iCol+3, iRow-3] = value;
                }

                BetaMatrix[iRow, NumPts] = ControlPoints[iPointA].Y;
                BetaMatrix[iRow, NumPts + 1] = ControlPoints[iPointA].X;
                BetaMatrix[iRow, NumPts + 2] = 1; 
            }

            for (int iCol = 0; iCol < NumPts; iCol++)
            {
                BetaMatrix[0, iCol] = ControlPoints[iCol].X;
                BetaMatrix[1, iCol] = ControlPoints[iCol].Y;
                BetaMatrix[2, iCol] = 1;
            }
            
            return BetaMatrix; 
        }

        public static double[] CalculateRBFWeights(GridVector2[] MappedPoints, GridVector2[] ControlPoints, BasisFunctionDelegate BasisFunction)
        {
            if (MappedPoints == null || ControlPoints == null)
                throw new ArgumentNullException();
            
            Debug.Assert(MappedPoints.Length == ControlPoints.Length); 
             
            Matrix<double> NumericsBetaMatrix = Matrix<double>.Build.DenseOfArray(CreateBetaMatrixWithLinear(MappedPoints, BasisFunction));
            Vector<double> NumericsSolutionMatrix_X = Vector<double>.Build.DenseOfArray(CreateSolutionMatrix_X_WithLinear(ControlPoints));
            Vector<double> NumericsSolutionMatrix_Y = Vector<double>.Build.DenseOfArray(CreateSolutionMatrix_Y_WithLinear(ControlPoints));
             
            //double[] WeightsX = GridMatrix.LinSolve(BetaMatrix, SolutionMatrix_X);
            //double[] WeightsY = GridMatrix.LinSolve(BetaMatrix, SolutionMatrix_Y);

            double[] WeightsX = NumericsBetaMatrix.Solve(NumericsSolutionMatrix_X).ToArray();
            double[] WeightsY = NumericsBetaMatrix.Solve(NumericsSolutionMatrix_Y).ToArray();

            double[] Weights = new double[WeightsX.Length + WeightsY.Length];

            Array.Copy(WeightsX, Weights, WeightsX.Length);
            Array.Copy(WeightsY, 0, Weights, WeightsX.Length, WeightsY.Length);

            return Weights; 
        }
    }
}
