using MathNet.Numerics.LinearAlgebra;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Geometry.Transforms
{
    [Serializable]
    public readonly struct RBFTransformComponents(TransformBasicInfo info, float[] CtoM, float[] MtoC)
    {
        public readonly TransformBasicInfo Info = info;
        public readonly float[] ControlToMappedSpaceWeights = CtoM;
        public readonly float[] MappedToControlSpaceWeights = MtoC;
    }


    [Serializable]
    public class RBFTransform : ReferencePointBasedTransform, IContinuousTransform, IMemoryMinimization
    {
        public delegate double BasisFunctionDelegate(double distance);

        readonly BasisFunctionDelegate BasisFunction = new(StandardBasisFunction);

        private float[] _ControlToMappedSpaceWeights = null;
        private float[] ControlToMappedSpaceWeights
        {
            get
            {
                if (_ControlToMappedSpaceWeights is null)
                {
                    lock (this)
                    {
                        if (_ControlToMappedSpaceWeights != null)
                            return _ControlToMappedSpaceWeights;

                        _ControlToMappedSpaceWeights = CalculateRBFWeights(MappingVector2.ControlPoints(this.MapPoints),
                                                                           MappingVector2.MappedPoints(this.MapPoints),
                                                                           null);
                    }
                }

                return _ControlToMappedSpaceWeights;
            }
        }

        private float[] _MappedToControlSpaceWeights = null;
        private float[] MappedToControlSpaceWeights
        {
            get
            {
                if (_MappedToControlSpaceWeights is null)
                {
                    lock (this)
                    {
                        if (_MappedToControlSpaceWeights is not null)
                            return _MappedToControlSpaceWeights;

                        //double[,] BetaMatrixControlToMapped = CreateBetaMatrixWithLinear(MappingVector2.MappedPoints(this.MapPoints), this.BasisFunction);
                        //double[] ResultMatrixControlToMapped = CreateSolutionMatrixWithLinear(MappingVector2.ControlPoints(this.MapPoints));
                        //_MappedToControlSpaceWeights = GridMatrix.LinSolve(BetaMatrixControlToMapped, ResultMatrixControlToMapped);
                        _MappedToControlSpaceWeights = CalculateRBFWeights(MappingVector2.MappedPoints(this.MapPoints),
                                                                           MappingVector2.ControlPoints(this.MapPoints),
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

        public RBFTransform(MappingVector2[] points, TransformBasicInfo info)
            : base(points, info)
        {
        }

        protected RBFTransform(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));

            _ControlToMappedSpaceWeights = info.GetValue("_ControlToMappedSpaceWeights", typeof(float[])) as float[];
            _MappedToControlSpaceWeights = info.GetValue("_MappedToControlSpaceWeights", typeof(float[])) as float[];
        }


        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            info.AddValue("_ControlToMappedSpaceWeights", ControlToMappedSpaceWeights);
            info.AddValue("_MappedToControlSpaceWeights", MappedToControlSpaceWeights);

            base.GetObjectData(info, context);
        }

        public override bool CanTransform(in Vector2 Point) => true;

        public static Vector2 Transform(Vector2 Point, float[] Weights, Vector2[] ControlPoints, BasisFunctionDelegate BasisFunction)
        {
            if (ControlPoints is null)
                throw new ArgumentNullException(nameof(ControlPoints));
            if (Weights is null)
                throw new ArgumentNullException(nameof(Weights));
            if (BasisFunction is null)
                throw new ArgumentNullException(nameof(BasisFunction));

            int nPoints = ControlPoints.Length;
            double[] distances = new double[nPoints];
            double[] functionValues = new double[nPoints];

            double WeightSumX = 0;
            double WeightSumY = 0;

            for (int i = 0; i < distances.Length; i++)
            {
                double dist = Vector2.Distance(ControlPoints[i], Point);
                double funcVal = BasisFunction(dist);
                distances[i] = dist;
                functionValues[i] = funcVal;

                WeightSumX += (Weights[i] * funcVal);
                WeightSumY += (Weights[i + 3 + nPoints] * funcVal);
            }

            double X = WeightSumX + (Point.Y * Weights[nPoints]) + (Point.X * Weights[nPoints + 1]) + Weights[nPoints + 2];
            double Y = WeightSumY + (Point.Y * Weights[nPoints + 3 + nPoints]) + (Point.X * Weights[nPoints + nPoints + 3 + 1]) + Weights[nPoints + nPoints + 3 + 2];

            return new Vector2(X, Y).Round(Global.TransformSignificantDigits);
        }

        public override Vector2 Transform(in Vector2 Point) => RBFTransform.Transform(Point, MappedToControlSpaceWeights, MappingVector2.MappedPoints(this.MapPoints), this.BasisFunction);

        public override Vector2[] Transform(in Vector2[] Points)
        {
            var Output = from Point in Points.AsParallel().AsOrdered() select RBFTransform.Transform(Point, MappedToControlSpaceWeights, MappingVector2.MappedPoints(this.MapPoints), this.BasisFunction);
            return Output.ToArray();
        }

        public override bool TryTransform(in Vector2 Point, out Vector2 v)
        {
            v = Transform(Point);
            return true;
        }
        public override bool[] TryTransform(in Vector2[] Points, out Vector2[] Output)
        {
            Output = this.Transform(Points);
            return [.. Points.Select(p => true)];
        }

        public override bool CanInverseTransform(in Vector2 Point) => true;

        public override Vector2 InverseTransform(in Vector2 Point) => RBFTransform.Transform(Point, ControlToMappedSpaceWeights, MappingVector2.ControlPoints(this.MapPoints), this.BasisFunction);

        public override Vector2[] InverseTransform(in Vector2[] Points)
        {
            var Output = from Point in Points.AsParallel().AsOrdered() select RBFTransform.Transform(Point, ControlToMappedSpaceWeights, MappingVector2.ControlPoints(this.MapPoints), this.BasisFunction);
            return Output.ToArray();
        }

        public override bool TryInverseTransform(in Vector2 Point, out Vector2 v)
        {
            v = InverseTransform(Point);
            return true;
        }

        public override bool[] TryInverseTransform(in Vector2[] Points, out Vector2[] Output)
        {
            Output = this.InverseTransform(Points);
            return [.. Points.Select(p => true)];
        }

        public static float[] CreateSolutionMatrixWithLinear(Vector2[] ControlPoints)
        {
            if (ControlPoints is null)
                throw new ArgumentNullException(nameof(ControlPoints));

            int NumPts = ControlPoints.Length;

            float[] ResultMatrix = new float[(NumPts + 3) * 2];

            for (int i = 0; i < NumPts; i++)
            {
                ResultMatrix[i + 3] = (float)ControlPoints[i].X;
                ResultMatrix[(i + 3) + (NumPts + 3)] = (float)ControlPoints[i].Y;
            }

            return ResultMatrix;
        }

        public static Vector<float> CreateSolutionMatrix_X_WithLinear(Vector2[] ControlPoints)
        {
            if (ControlPoints is null)
                throw new ArgumentNullException(nameof(ControlPoints));

            int NumPts = ControlPoints.Length;

            Vector<float> ResultMatrix = Vector<float>.Build.Dense(NumPts + 3);

            for (int i = 0; i < NumPts; i++)
            {
                ResultMatrix[i + 3] = (float)ControlPoints[i].X;
            }

            return ResultMatrix;
        }

        /*
        public static float[] CreateSolutionMatrix_X_WithLinear(Vector2[] ControlPoints)
        {
            if (ControlPoints is null)
                throw new ArgumentNullException();

            int NumPts = ControlPoints.Length;

            float[] ResultMatrix = new float[(NumPts + 3)];

            for (int i = 0; i < NumPts; i++)
            {
                ResultMatrix[i + 3] = (float)ControlPoints[i].X;
            }

            return ResultMatrix;
        }
        */

        public static Vector<float> CreateSolutionMatrix_Y_WithLinear(Vector2[] ControlPoints)
        {
            if (ControlPoints is null)
                throw new ArgumentNullException(nameof(ControlPoints));

            int NumPts = ControlPoints.Length;

            Vector<float> ResultMatrix = Vector<float>.Build.Dense(NumPts + 3);

            for (int i = 0; i < NumPts; i++)
            {
                ResultMatrix[i + 3] = (float)ControlPoints[i].Y;
            }

            return ResultMatrix;
        }

        /*
        public static float[] CreateSolutionMatrix_Y_WithLinear(Vector2[] ControlPoints)
        {
            if (ControlPoints is null)
                throw new ArgumentNullException();

            int NumPts = ControlPoints.Length;

            float[] ResultMatrix = new float[(NumPts + 3)];

            for (int i = 0; i < NumPts; i++)
            {
                ResultMatrix[i + 3] = (float)ControlPoints[i].Y;
            }

            return ResultMatrix;
        }
        */

        /// <summary>
        /// Populates matrix by applying basis function to control points and filling a matrix [B 0; 0 B];
        /// </summary>
        /// <param name="ControlPoints"></param>
        /// <param name="BasisFunction">How to weight pairs of points, if null, use Euclidean distance</param>
        /// <returns></returns>
        public static Matrix<float> CreateBetaMatrixWithLinear(Vector2[] ControlPoints, BasisFunctionDelegate BasisFunction = null)
        {
            if (ControlPoints is null)
                throw new ArgumentNullException(nameof(ControlPoints));

            int NumPts = ControlPoints.Length;

            Matrix<float> BetaMatrix = Matrix<float>.Build.Dense(NumPts + 3, NumPts + 3);

            for (int iRow = 3; iRow < NumPts + 3; iRow++)
            {
                int iPointA = iRow - 3;

                for (int iCol = iPointA + 1; iCol < NumPts; iCol++)
                {
                    int iPointB = iCol;
                    double value;
                    if (BasisFunction != null)
                    {
                        double dist = Vector2.Distance(ControlPoints[iPointA], ControlPoints[iPointB]);
                        value = BasisFunction(dist);
                    }
                    else
                    {
                        double dist_squared = Vector2.DistanceSquared(ControlPoints[iPointA], ControlPoints[iPointB]);
                        value = dist_squared <= 0 ? 0 : dist_squared * (Math.Log(dist_squared) / 2.0); // = distance^2 * log(distance).
                    }
                    BetaMatrix[iRow, iCol] = (float)value;
                    BetaMatrix[iCol + 3, iRow - 3] = (float)value;
                }

                BetaMatrix[iRow, NumPts] = (float)ControlPoints[iPointA].Y;
                BetaMatrix[iRow, NumPts + 1] = (float)ControlPoints[iPointA].X;
                BetaMatrix[iRow, NumPts + 2] = 1;
            }

            for (int iCol = 0; iCol < NumPts; iCol++)
            {
                BetaMatrix[0, iCol] = (float)ControlPoints[iCol].X;
                BetaMatrix[1, iCol] = (float)ControlPoints[iCol].Y;
                BetaMatrix[2, iCol] = 1;
            }

            return BetaMatrix;
        }

        /*
        /// <summary>
        /// Populates matrix by applying basis function to control points and filling a matrix [B 0; 0 B];
        /// </summary>
        /// <param name="ControlPoints"></param>
        /// <param name="BasisFunction"></param>
        /// <returns></returns>
        public static float[,] CreateBetaMatrixWithLinear(Vector2[] ControlPoints, BasisFunctionDelegate BasisFunction)
        {
            if (ControlPoints is null)
                throw new ArgumentNullException(); 

            int NumPts = ControlPoints.Length;

            float[,] BetaMatrix = new float[NumPts+3, NumPts+3];

            for (int iRow = 3; iRow < NumPts + 3; iRow++)
            {
                int iPointA = iRow - 3;

                for (int iCol = iPointA+1; iCol < NumPts; iCol++)
                {
                    int iPointB = iCol;
                    double value;
                    if (BasisFunction != null)
                    {
                        double dist = Vector2.Distance(ControlPoints[iPointA], ControlPoints[iPointB]);
                        value = BasisFunction(dist);
                    }
                    else
                    {
                        double dist_squared = Vector2.DistanceSquared(ControlPoints[iPointA], ControlPoints[iPointB]);
                        value = dist_squared <= 0 ? 0 : dist_squared * (Math.Log(dist_squared) / 2.0); // = distance^2 * log(distance).
                    }
                    BetaMatrix[iRow, iCol] = (float)value;
                    BetaMatrix[iCol+3, iRow-3] = (float)value;
                }

                BetaMatrix[iRow, NumPts] = (float)ControlPoints[iPointA].Y;
                BetaMatrix[iRow, NumPts + 1] = (float)ControlPoints[iPointA].X;
                BetaMatrix[iRow, NumPts + 2] = 1; 
            }

            for (int iCol = 0; iCol < NumPts; iCol++)
            {
                BetaMatrix[0, iCol] = (float)ControlPoints[iCol].X;
                BetaMatrix[1, iCol] = (float)ControlPoints[iCol].Y;
                BetaMatrix[2, iCol] = 1;
            }
            
            return BetaMatrix; 
        }
        */

        public static float[] CalculateRBFWeights(Vector2[] MappedPoints, Vector2[] ControlPoints, BasisFunctionDelegate BasisFunction)
        {
            if (MappedPoints is null)
                throw new ArgumentNullException(nameof(MappedPoints));
            if (ControlPoints is null)
                throw new ArgumentNullException(nameof(ControlPoints));

            Debug.Assert(MappedPoints.Length == ControlPoints.Length);

            Matrix<float> NumericsBetaMatrix = CreateBetaMatrixWithLinear(MappedPoints, BasisFunction);
            float[] WeightsX = [.. NumericsBetaMatrix.Solve(CreateSolutionMatrix_X_WithLinear(ControlPoints))];
            float[] WeightsY = [.. NumericsBetaMatrix.Solve(CreateSolutionMatrix_Y_WithLinear(ControlPoints))];
            NumericsBetaMatrix = null;
            float[] Weights = new float[WeightsX.Length + WeightsY.Length];

            Array.Copy(WeightsX, Weights, WeightsX.Length);
            Array.Copy(WeightsY, 0, Weights, WeightsX.Length, WeightsY.Length);

            return Weights;
        }

        public override void MinimizeMemory()
        {
            _MappedToControlSpaceWeights = null;
            _ControlToMappedSpaceWeights = null;

            base.MinimizeMemory();
        }


        /// <summary>
        /// Write transform components to disk when minimizing memory
        /// </summary>
        /// <returns></returns>
        private bool SerializeTransformComponents()
        {
            if (Info is not ITransformCacheInfo cacheInfo)
                return false;

            using Stream binFile = System.IO.File.OpenWrite(cacheInfo.CacheFullPath);
            BinaryFormatter binaryFormatter = new();
            RBFTransformComponents components = new(this.Info,
                                                                               ControlToMappedSpaceWeights,
                                                                               MappedToControlSpaceWeights);

            binaryFormatter.Serialize(binFile, components);

            return true;
        }

        /// <summary>
        /// Write transform components to disk when minimizing memory
        /// </summary>
        /// <returns></returns>
        private bool TryLoadSerializedTransformComponents()
        {
            if (Info is ITransformCacheInfo cacheInfo)
            {
                if (!System.IO.File.Exists(cacheInfo.CacheFullPath))
                    return false;

                bool CacheInvalid = false;
                try
                {

                    using Stream binFile = System.IO.File.OpenRead(cacheInfo.CacheFullPath);
                    BinaryFormatter binaryFormatter = new();
                    RBFTransformComponents components =
                        (RBFTransformComponents)binaryFormatter.Deserialize(binFile);

                    CacheInvalid = components.Info.LastModified < this.Info.LastModified;
                    if (!CacheInvalid)
                    {
                        this._MappedToControlSpaceWeights = components.MappedToControlSpaceWeights;
                        this._ControlToMappedSpaceWeights = components.ControlToMappedSpaceWeights;
                    }
                }
                catch (System.Runtime.Serialization.SerializationException e)
                {
                    Trace.WriteLine(string.Format("Remove file with Serialization exception {0}\n{1}", e.Message,
                        cacheInfo.CacheFullPath));

                    System.IO.File.Delete(cacheInfo.CacheFullPath);

                    return false;
                }

                if (CacheInvalid)
                {
                    System.IO.File.Delete(cacheInfo.CacheFullPath);
                    return false;
                }

                return true;
            }

            return false;
        }

        void IContinuousTransform.Translate(in Vector2 vector) => throw new NotImplementedException();
    }
}

