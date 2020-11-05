using MathNet.Numerics.LinearAlgebra;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace Geometry.Transforms
{
    [Serializable]
    class RBFTransformComponents
    {
        public readonly TransformInfo Info;
        public readonly float[] ControlToMappedSpaceWeights;
        public readonly float[] MappedToControlSpaceWeights;

        public RBFTransformComponents(TransformInfo info, float[] CtoM, float[] MtoC)
        {
            Info = info;
            ControlToMappedSpaceWeights = CtoM;
            MappedToControlSpaceWeights = MtoC;
        }
    }


    [Serializable]
    public class RBFTransform : ReferencePointBasedTransform, IContinuousTransform, IMemoryMinimization
    {
        public delegate double BasisFunctionDelegate(double distance);
        BasisFunctionDelegate BasisFunction = new BasisFunctionDelegate(StandardBasisFunction);

        private float[] _ControlToMappedSpaceWeights = null;
        private float[] ControlToMappedSpaceWeights
        {
            get
            {
                if (_ControlToMappedSpaceWeights == null)
                {
                    lock (this)
                    {
                        if (_ControlToMappedSpaceWeights != null)
                            return _ControlToMappedSpaceWeights;

                        _ControlToMappedSpaceWeights = CalculateRBFWeights(MappingGridVector2.ControlPoints(this.MapPoints),
                                                                           MappingGridVector2.MappedPoints(this.MapPoints),
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

            _ControlToMappedSpaceWeights = info.GetValue("_ControlToMappedSpaceWeights", typeof(float[])) as float[];
            _MappedToControlSpaceWeights = info.GetValue("_MappedToControlSpaceWeights", typeof(float[])) as float[];
        }


        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            info.AddValue("_ControlToMappedSpaceWeights", ControlToMappedSpaceWeights);
            info.AddValue("_MappedToControlSpaceWeights", MappedToControlSpaceWeights);

            base.GetObjectData(info, context);
        }

        public bool CanTransform(GridVector2 Point)
        {
            return true;
        }

        public static GridVector2 Transform(GridVector2 Point, float[] Weights, GridVector2[] ControlPoints, BasisFunctionDelegate BasisFunction)
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

        public GridVector2 Transform(GridVector2 Point)
        {
            return RBFTransform.Transform(Point, MappedToControlSpaceWeights, MappingGridVector2.MappedPoints(this.MapPoints), this.BasisFunction);
        }

        public GridVector2[] Transform(GridVector2[] Points)
        {
            var Output = from Point in Points.AsParallel().AsOrdered() select RBFTransform.Transform(Point, MappedToControlSpaceWeights, MappingGridVector2.MappedPoints(this.MapPoints), this.BasisFunction);
            return Output.ToArray();
        }

        public bool TryTransform(GridVector2 Point, out GridVector2 v)
        {
            v = Transform(Point);
            return true;
        }
        public bool[] TryTransform(GridVector2[] Points, out GridVector2[] Output)
        {
            Output = this.Transform(Points);
            return Points.Select(p => true).ToArray();
        }

        public bool CanInverseTransform(GridVector2 Point)
        {
            return true;
        }

        public GridVector2 InverseTransform(GridVector2 Point)
        {
            return RBFTransform.Transform(Point, ControlToMappedSpaceWeights, MappingGridVector2.ControlPoints(this.MapPoints), this.BasisFunction);
        }

        public GridVector2[] InverseTransform(GridVector2[] Points)
        {
            var Output = from Point in Points.AsParallel().AsOrdered() select RBFTransform.Transform(Point, ControlToMappedSpaceWeights, MappingGridVector2.ControlPoints(this.MapPoints), this.BasisFunction);
            return Output.ToArray();
        }

        public bool TryInverseTransform(GridVector2 Point, out GridVector2 v)
        {
            v = InverseTransform(Point);
            return true;
        }

        public bool[] TryInverseTransform(GridVector2[] Points, out GridVector2[] Output)
        {
            Output = this.InverseTransform(Points);
            return Points.Select(p => true).ToArray();
        }

        public static float[] CreateSolutionMatrixWithLinear(GridVector2[] ControlPoints)
        {
            if (ControlPoints == null)
                throw new ArgumentNullException();

            int NumPts = ControlPoints.Length;

            float[] ResultMatrix = new float[(NumPts + 3) * 2];

            for (int i = 0; i < NumPts; i++)
            {
                ResultMatrix[i + 3] = (float)ControlPoints[i].X;
                ResultMatrix[(i + 3) + (NumPts + 3)] = (float)ControlPoints[i].Y;
            }

            return ResultMatrix;
        }

        public static Vector<float> CreateSolutionMatrix_X_WithLinear(GridVector2[] ControlPoints)
        {
            if (ControlPoints == null)
                throw new ArgumentNullException();

            int NumPts = ControlPoints.Length;

            Vector<float> ResultMatrix = Vector<float>.Build.Dense(NumPts + 3);

            for (int i = 0; i < NumPts; i++)
            {
                ResultMatrix[i + 3] = (float)ControlPoints[i].X;
            }

            return ResultMatrix;
        }

        /*
        public static float[] CreateSolutionMatrix_X_WithLinear(GridVector2[] ControlPoints)
        {
            if (ControlPoints == null)
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

        public static Vector<float> CreateSolutionMatrix_Y_WithLinear(GridVector2[] ControlPoints)
        {
            if (ControlPoints == null)
                throw new ArgumentNullException();

            int NumPts = ControlPoints.Length;

            Vector<float> ResultMatrix = Vector<float>.Build.Dense(NumPts + 3);

            for (int i = 0; i < NumPts; i++)
            {
                ResultMatrix[i + 3] = (float)ControlPoints[i].Y;
            }

            return ResultMatrix;
        }

        /*
        public static float[] CreateSolutionMatrix_Y_WithLinear(GridVector2[] ControlPoints)
        {
            if (ControlPoints == null)
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
        /// <param name="BasisFunction"></param>
        /// <returns></returns>
        public static Matrix<float> CreateBetaMatrixWithLinear(GridVector2[] ControlPoints, BasisFunctionDelegate BasisFunction)
        {
            if (ControlPoints == null)
                throw new ArgumentNullException();

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
                        double dist = GridVector2.Distance(ControlPoints[iPointA], ControlPoints[iPointB]);
                        value = BasisFunction(dist);
                    }
                    else
                    {
                        double dist_squared = GridVector2.DistanceSquared(ControlPoints[iPointA], ControlPoints[iPointB]);
                        value = dist_squared * (Math.Log(dist_squared) / 2.0); // = distance^2 * log(distance).
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
        public static float[,] CreateBetaMatrixWithLinear(GridVector2[] ControlPoints, BasisFunctionDelegate BasisFunction)
        {
            if (ControlPoints == null)
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
                        double dist = GridVector2.Distance(ControlPoints[iPointA], ControlPoints[iPointB]);
                        value = BasisFunction(dist);
                    }
                    else
                    {
                        double dist_squared = GridVector2.DistanceSquared(ControlPoints[iPointA], ControlPoints[iPointB]);
                        value = dist_squared * (Math.Log(dist_squared) / 2.0); // = distance^2 * log(distance).
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

        public static float[] CalculateRBFWeights(GridVector2[] MappedPoints, GridVector2[] ControlPoints, BasisFunctionDelegate BasisFunction)
        {
            if (MappedPoints == null || ControlPoints == null)
                throw new ArgumentNullException();

            Debug.Assert(MappedPoints.Length == ControlPoints.Length);

            Matrix<float> NumericsBetaMatrix = CreateBetaMatrixWithLinear(MappedPoints, BasisFunction);
            float[] WeightsX = NumericsBetaMatrix.Solve(CreateSolutionMatrix_X_WithLinear(ControlPoints)).ToArray();
            float[] WeightsY = NumericsBetaMatrix.Solve(CreateSolutionMatrix_Y_WithLinear(ControlPoints)).ToArray();
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
            ITransformCacheInfo cacheInfo = Info as ITransformCacheInfo;
            if (cacheInfo == null)
                return false;

            using (Stream binFile = System.IO.File.OpenWrite(cacheInfo.CacheFullPath))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                RBFTransformComponents components = new RBFTransformComponents(this.Info,
                                                                                   ControlToMappedSpaceWeights,
                                                                                   MappedToControlSpaceWeights);

                binaryFormatter.Serialize(binFile, components);
            }

            return true;
        }

        /// <summary>
        /// Write transform components to disk when minimizing memory
        /// </summary>
        /// <returns></returns>
        private bool TryLoadSerializedTransformComponents()
        {
            ITransformCacheInfo cacheInfo = Info as ITransformCacheInfo;
            if (cacheInfo == null)
                return false;

            if (!System.IO.File.Exists(cacheInfo.CacheFullPath))
                return false;

            bool CacheInvalid = false;
            try
            {

                using (Stream binFile = System.IO.File.OpenRead(cacheInfo.CacheFullPath))
                {
                    var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    RBFTransformComponents components = binaryFormatter.Deserialize(binFile) as RBFTransformComponents;

                    CacheInvalid = components.Info.LastModified < this.Info.LastModified;
                    if (!CacheInvalid)
                    {
                        this._MappedToControlSpaceWeights = components.MappedToControlSpaceWeights;
                        this._ControlToMappedSpaceWeights = components.ControlToMappedSpaceWeights;
                    }

                }
            }
            catch (System.Runtime.Serialization.SerializationException e)
            {
                Trace.WriteLine(string.Format("Remove file with Serialization exception {0}\n{1}", e.Message, cacheInfo.CacheFullPath));

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
    }
}

