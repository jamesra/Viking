using MathNet.Numerics.LinearAlgebra;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Geometry.Transforms
{
    /// <summary>
    /// Solved RBF weights for both mapping directions, as written to a component cache.
    /// </summary>
    [Serializable]
    public readonly struct RBFTransformComponents(TransformBasicInfo info, double[] CtoM, double[] MtoC)
    {
        public readonly TransformBasicInfo Info = info;
        public readonly double[] ControlToMappedSpaceWeights = CtoM;
        public readonly double[] MappedToControlSpaceWeights = MtoC;
    }


    /// <summary>
    /// Thin-plate radial basis function transform with an affine term. Used as the continuous fallback for points
    /// outside a discrete (triangulated) transform.
    /// </summary>
    /// <remarks>
    /// Weights are solved and stored in double precision. Coordinates reach several hundred thousand pixels and the
    /// basis values (r squared log r) about 1e11, so a single-precision solve cannot hold single-pixel accuracy.
    /// </remarks>
    [Serializable]
    public class RBFTransform : ReferencePointBasedTransform, IContinuousTransform, IMemoryMinimization
    {
        public delegate double BasisFunctionDelegate(double distance);

        readonly BasisFunctionDelegate BasisFunction = new(StandardBasisFunction);

        /// <summary>
        /// Guards building and dropping every cached value below (point arrays and solved weights). Dropping takes the same lock,
        /// so a weight solve that is running finishes before the cache it was computed from is invalidated.
        /// </summary>
        [NonSerialized]
        private readonly object _cacheLock = new();

        /// <summary>Largest per-batch work, in point-by-control-point basis evaluations, that is mapped on the calling thread.</summary>
        private const long ParallelWorkThreshold = 16384;

        [NonSerialized]
        private volatile Vector2[] _mappedPointsCache = null;

        [NonSerialized]
        private volatile Vector2[] _controlPointsCache = null;

        /// <summary>
        /// The mapped point of every <see cref="ReferencePointBasedTransform.MapPoints"/> element, in the same order. Built on first use
        /// and shared by every caller, so callers must not modify it. Dropped by <see cref="OnMapPointsChanged"/> and
        /// <see cref="MinimizeMemory"/>.
        /// </summary>
        private Vector2[] MappedPointsArray
        {
            get
            {
                Vector2[] cached = _mappedPointsCache;
                if (cached is not null)
                    return cached;

                lock (_cacheLock)
                {
                    return _mappedPointsCache ??= MappingVector2.MappedPoints(this.MapPoints);
                }
            }
        }

        /// <summary>
        /// The control point of every <see cref="ReferencePointBasedTransform.MapPoints"/> element, in the same order. Same sharing and
        /// invalidation contract as <see cref="MappedPointsArray"/>.
        /// </summary>
        private Vector2[] ControlPointsArray
        {
            get
            {
                Vector2[] cached = _controlPointsCache;
                if (cached is not null)
                    return cached;

                lock (_cacheLock)
                {
                    return _controlPointsCache ??= MappingVector2.ControlPoints(this.MapPoints);
                }
            }
        }

        /// <summary>
        /// Drops the cached point arrays and the weights solved from them, because the map points were replaced or moved.
        /// </summary>
        /// <remarks>
        /// The caches are copies of the map point values, so any in-place change to <see cref="ReferencePointBasedTransform.MapPoints"/>
        /// must reach this method. The base class does that for point replacement and for <c>Translate</c>. The public constructor
        /// copies the array it is given so that another transform that shares the caller's array cannot change this transform's points
        /// behind its back.
        /// </remarks>
        protected override void OnMapPointsChanged()
        {
            lock (_cacheLock)
            {
                _mappedPointsCache = null;
                _controlPointsCache = null;
                _ControlToMappedSpaceWeights = null;
                _MappedToControlSpaceWeights = null;
            }

            base.OnMapPointsChanged();
        }

        private volatile double[] _ControlToMappedSpaceWeights = null;
        private double[] ControlToMappedSpaceWeights
        {
            get
            {
                double[] cached = _ControlToMappedSpaceWeights;
                if (cached is not null)
                    return cached;

                lock (_cacheLock)
                {
                    return _ControlToMappedSpaceWeights ??= CalculateRBFWeights(ControlPointsArray, MappedPointsArray, null);
                }
            }
        }

        private volatile double[] _MappedToControlSpaceWeights = null;
        private double[] MappedToControlSpaceWeights
        {
            get
            {
                double[] cached = _MappedToControlSpaceWeights;
                if (cached is not null)
                    return cached;

                lock (_cacheLock)
                {
                    return _MappedToControlSpaceWeights ??= CalculateRBFWeights(MappedPointsArray, ControlPointsArray, null);
                }
            }
        }

        public static double StandardBasisFunction(double distance)
        {
            if (distance == 0)
                return 0;

            return distance * distance * Math.Log(distance);
        }

        /// <summary>
        /// Creates a transform through <paramref name="points"/>. The array is copied, so later changes to the caller's array (for
        /// example a <c>Translate</c> on another transform that owns it) do not reach this transform's cached values.
        /// </summary>
        public RBFTransform(MappingVector2[] points, TransformBasicInfo info)
            : base(points is null ? null : (MappingVector2[])points.Clone(), info)
        {
        }

        protected RBFTransform(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));

            //Caches written before the switch to double hold float[] weights. Those are dropped rather than widened, so the
            //weights are solved again in double precision the first time they are needed.
            _ControlToMappedSpaceWeights = info.GetValue("_ControlToMappedSpaceWeights", typeof(object)) as double[];
            _MappedToControlSpaceWeights = info.GetValue("_MappedToControlSpaceWeights", typeof(object)) as double[];
        }


        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            info.AddValue("_ControlToMappedSpaceWeights", ControlToMappedSpaceWeights);
            info.AddValue("_MappedToControlSpaceWeights", MappedToControlSpaceWeights);

            base.GetObjectData(info, context);
        }

        public override bool CanTransform(in Vector2 Point) => true;

        /// <summary>
        /// Evaluates the RBF with solved <paramref name="Weights"/> at <paramref name="Point"/>. Allocation free and thread safe as long
        /// as the arrays are not modified while it runs. Sums are accumulated in point order, so results do not depend on batching.
        /// </summary>
        public static Vector2 Transform(Vector2 Point, double[] Weights, Vector2[] ControlPoints, BasisFunctionDelegate BasisFunction)
        {
            if (ControlPoints is null)
                throw new ArgumentNullException(nameof(ControlPoints));
            if (Weights is null)
                throw new ArgumentNullException(nameof(Weights));
            if (BasisFunction is null)
                throw new ArgumentNullException(nameof(BasisFunction));

            int nPoints = ControlPoints.Length;

            double WeightSumX = 0;
            double WeightSumY = 0;

            for (int i = 0; i < nPoints; i++)
            {
                double dist = Vector2.Distance(ControlPoints[i], Point);
                double funcVal = BasisFunction(dist);

                WeightSumX += (Weights[i] * funcVal);
                WeightSumY += (Weights[i + 3 + nPoints] * funcVal);
            }

            double X = WeightSumX + (Point.Y * Weights[nPoints]) + (Point.X * Weights[nPoints + 1]) + Weights[nPoints + 2];
            double Y = WeightSumY + (Point.Y * Weights[nPoints + 3 + nPoints]) + (Point.X * Weights[nPoints + nPoints + 3 + 1]) + Weights[nPoints + nPoints + 3 + 2];

            return new Vector2(X, Y).Round(Global.TransformSignificantDigits);
        }

        public override Vector2 Transform(in Vector2 Point) => RBFTransform.Transform(Point, MappedToControlSpaceWeights, MappedPointsArray, this.BasisFunction);

        /// <summary>
        /// Maps every point with the same weights and control points, resolved once for the whole batch. Batches large enough to
        /// repay the thread cost are mapped in parallel; each output slot is written by exactly one thread, so order is preserved.
        /// </summary>
        public override Vector2[] Transform(in Vector2[] Points)
        {
            if (Points is null)
                throw new ArgumentNullException(nameof(Points));

            return MapBatch(Points, MappedToControlSpaceWeights, MappedPointsArray);
        }

        /// <summary>
        /// Evaluates <see cref="Transform(Vector2, double[], Vector2[], BasisFunctionDelegate)"/> for each point, in parallel when
        /// <c>points * controlPoints</c> reaches <see cref="ParallelWorkThreshold"/>.
        /// </summary>
        private Vector2[] MapBatch(Vector2[] Points, double[] Weights, Vector2[] ControlPoints)
        {
            Vector2[] Output = new Vector2[Points.Length];
            BasisFunctionDelegate basis = this.BasisFunction;

            if ((long)Points.Length * ControlPoints.Length < ParallelWorkThreshold)
            {
                for (int i = 0; i < Points.Length; i++)
                    Output[i] = RBFTransform.Transform(Points[i], Weights, ControlPoints, basis);
            }
            else
            {
                System.Threading.Tasks.Parallel.For(0, Points.Length,
                    i => Output[i] = RBFTransform.Transform(Points[i], Weights, ControlPoints, basis));
            }

            return Output;
        }

        private static bool[] AllTrue(int length)
        {
            bool[] result = new bool[length];
            for (int i = 0; i < result.Length; i++)
                result[i] = true;

            return result;
        }

        public override bool TryTransform(in Vector2 Point, out Vector2 v)
        {
            v = Transform(Point);
            return true;
        }
        public override bool[] TryTransform(in Vector2[] Points, out Vector2[] Output)
        {
            Output = this.Transform(Points);
            return AllTrue(Output.Length);
        }

        public override bool CanInverseTransform(in Vector2 Point) => true;

        public override Vector2 InverseTransform(in Vector2 Point) => RBFTransform.Transform(Point, ControlToMappedSpaceWeights, ControlPointsArray, this.BasisFunction);

        /// <summary>Maps control space points back to mapped space; see <see cref="Transform(Vector2[])"/> for the batching rules.</summary>
        public override Vector2[] InverseTransform(in Vector2[] Points)
        {
            if (Points is null)
                throw new ArgumentNullException(nameof(Points));

            return MapBatch(Points, ControlToMappedSpaceWeights, ControlPointsArray);
        }

        public override bool TryInverseTransform(in Vector2 Point, out Vector2 v)
        {
            v = InverseTransform(Point);
            return true;
        }

        public override bool[] TryInverseTransform(in Vector2[] Points, out Vector2[] Output)
        {
            Output = this.InverseTransform(Points);
            return AllTrue(Output.Length);
        }

        public static double[] CreateSolutionMatrixWithLinear(Vector2[] ControlPoints)
        {
            if (ControlPoints is null)
                throw new ArgumentNullException(nameof(ControlPoints));

            int NumPts = ControlPoints.Length;

            double[] ResultMatrix = new double[(NumPts + 3) * 2];

            for (int i = 0; i < NumPts; i++)
            {
                ResultMatrix[i + 3] = ControlPoints[i].X;
                ResultMatrix[(i + 3) + (NumPts + 3)] = ControlPoints[i].Y;
            }

            return ResultMatrix;
        }

        public static Vector<double> CreateSolutionMatrix_X_WithLinear(Vector2[] ControlPoints)
        {
            if (ControlPoints is null)
                throw new ArgumentNullException(nameof(ControlPoints));

            int NumPts = ControlPoints.Length;

            Vector<double> ResultMatrix = Vector<double>.Build.Dense(NumPts + 3);

            for (int i = 0; i < NumPts; i++)
            {
                ResultMatrix[i + 3] = ControlPoints[i].X;
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

        public static Vector<double> CreateSolutionMatrix_Y_WithLinear(Vector2[] ControlPoints)
        {
            if (ControlPoints is null)
                throw new ArgumentNullException(nameof(ControlPoints));

            int NumPts = ControlPoints.Length;

            Vector<double> ResultMatrix = Vector<double>.Build.Dense(NumPts + 3);

            for (int i = 0; i < NumPts; i++)
            {
                ResultMatrix[i + 3] = ControlPoints[i].Y;
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
        public static Matrix<double> CreateBetaMatrixWithLinear(Vector2[] ControlPoints, BasisFunctionDelegate BasisFunction = null)
        {
            if (ControlPoints is null)
                throw new ArgumentNullException(nameof(ControlPoints));

            int NumPts = ControlPoints.Length;

            Matrix<double> BetaMatrix = Matrix<double>.Build.Dense(NumPts + 3, NumPts + 3);

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
                    BetaMatrix[iRow, iCol] = value;
                    BetaMatrix[iCol + 3, iRow - 3] = value;
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

        /// <summary>
        /// Solves the RBF weights that map <paramref name="MappedPoints"/> onto <paramref name="ControlPoints"/>. The result
        /// holds the X weights followed by the Y weights, each <c>N + 3</c> long (N basis weights, then the affine terms).
        /// </summary>
        /// <remarks>
        /// Solved in double precision; see the class remarks for why single precision is not enough. The matrix is factored once with
        /// LU and X and Y are solved from that one factorization. LU is what MathNet's <c>Matrix&lt;double&gt;.Solve</c> picks for a
        /// square matrix, so the weights are identical to solving X and Y separately.
        /// </remarks>
        public static double[] CalculateRBFWeights(Vector2[] MappedPoints, Vector2[] ControlPoints, BasisFunctionDelegate BasisFunction)
        {
            if (MappedPoints is null)
                throw new ArgumentNullException(nameof(MappedPoints));
            if (ControlPoints is null)
                throw new ArgumentNullException(nameof(ControlPoints));

            Debug.Assert(MappedPoints.Length == ControlPoints.Length);

            Matrix<double> NumericsBetaMatrix = CreateBetaMatrixWithLinear(MappedPoints, BasisFunction);
            var lu = NumericsBetaMatrix.LU();
            Vector<double> WeightsX = lu.Solve(CreateSolutionMatrix_X_WithLinear(ControlPoints));
            Vector<double> WeightsY = lu.Solve(CreateSolutionMatrix_Y_WithLinear(ControlPoints));

            double[] Weights = new double[WeightsX.Count + WeightsY.Count];
            for (int i = 0; i < WeightsX.Count; i++)
                Weights[i] = WeightsX[i];
            for (int i = 0; i < WeightsY.Count; i++)
                Weights[WeightsX.Count + i] = WeightsY[i];

            return Weights;
        }

        /// <summary>
        /// Drops the cached point arrays and solved weights. They are rebuilt on the next use.
        /// </summary>
        public override void MinimizeMemory()
        {
            lock (_cacheLock)
            {
                _MappedToControlSpaceWeights = null;
                _ControlToMappedSpaceWeights = null;
                _mappedPointsCache = null;
                _controlPointsCache = null;
            }

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

