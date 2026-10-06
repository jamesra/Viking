using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Geometry.Transforms
{
    /// <summary>
    /// A transform which uses a discreet transform where possible, but falls back to a continuous transform for points that cannot be mapped discreetly.
    /// </summary>
    [Serializable]
    public class DiscreteTransformWithContinuousFallback : IContinuousTransform, ITransformInfo, IMemoryMinimization, IControlPointTriangulation, ISpatialIndexPrewarm
    {
        public System.Threading.Tasks.Task PrewarmSpatialIndexAsync() =>
            (DiscreteTransform as ISpatialIndexPrewarm)?.PrewarmSpatialIndexAsync() ?? System.Threading.Tasks.Task.CompletedTask;

        readonly IDiscreteTransform DiscreteTransform;
        readonly IContinuousTransform ContinuousTransform;

        public override string ToString() => this.Info.ToString();

        public TransformBasicInfo Info
        {
            get; set;
        }

        public MappingVector2[] MapPoints => ((ITransformControlPoints)DiscreteTransform).MapPoints;

        public Rectangle ControlBounds => ((ITransformControlPoints)DiscreteTransform).ControlBounds;

        public Rectangle MappedBounds => ((ITransformControlPoints)DiscreteTransform).MappedBounds;

        public int[] TriangleIndicies
        {
            get
            {
                if (DiscreteTransform is IControlPointTriangulation dt)
                {
                    return dt.TriangleIndicies;
                }

                return [];
            }
        }

        public List<int>[] Edges
        {
            get
            {
                if (DiscreteTransform is IControlPointTriangulation dt)
                {
                    return dt.Edges;
                }

                return [];
            }
        }

        public DateTime LastModified => Info.LastModified;

        public DiscreteTransformWithContinuousFallback(IDiscreteTransform discreteTransform, IContinuousTransform continuousTransform, TransformBasicInfo info)
        {
            this.DiscreteTransform = discreteTransform;
            this.ContinuousTransform = continuousTransform;
            this.Info = info;
        }

        protected DiscreteTransformWithContinuousFallback(SerializationInfo info, StreamingContext context)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));

            DiscreteTransform = info.GetValue("DiscreetTransform", typeof(IDiscreteTransform)) as IDiscreteTransform;
            ContinuousTransform = info.GetValue("ContinuousTransform", typeof(IContinuousTransform)) as IContinuousTransform;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));

            info.AddValue("DiscreetTransform", DiscreteTransform);
            info.AddValue("ContinuousTransform", ContinuousTransform);
        }

        public bool CanTransform(in Vector2 p) => true;

        public bool CanInverseTransform(in Vector2 p) => true;

        public Vector2 Transform(in Vector2 Point)
        {
            if (!DiscreteTransform.TryTransform(Point, out Vector2 output))
            {
                output = ContinuousTransform.Transform(Point);
            }

            return output;
        }

        /// <summary>
        /// Maps every point: the discrete transform maps what it can in one batch call, and only the points it rejected go to the
        /// continuous transform, again as one batch. Same result as mapping each point separately.
        /// </summary>
        public Vector2[] Transform(in Vector2[] Points)
        {
            if (Points is null)
                throw new ArgumentNullException(nameof(Points));

            bool[] discreteMapped = DiscreteTransform.TryTransform(Points, out Vector2[] output);
            ApplyFallback(Points, discreteMapped, output, failed => ContinuousTransform.Transform(failed));
            return output;
        }

        public bool TryTransform(in Vector2 Point, out Vector2 v)
        {
            v = Transform(Point);
            return true;
        }

        public bool[] TryTransform(in Vector2[] Points, out Vector2[] v)
        {
            v = Transform(Points);
            return AllTrue(v.Length);
        }

        public Vector2 InverseTransform(in Vector2 Point)
        {
            if (!DiscreteTransform.TryInverseTransform(Point, out Vector2 output))
            {
                output = ContinuousTransform.InverseTransform(Point);
            }

            return output;
        }

        /// <summary>Inverse of <see cref="Transform(in Vector2[])"/>, with the same batching and fallback rules.</summary>
        public Vector2[] InverseTransform(in Vector2[] Points)
        {
            if (Points is null)
                throw new ArgumentNullException(nameof(Points));

            bool[] discreteMapped = DiscreteTransform.TryInverseTransform(Points, out Vector2[] output);
            ApplyFallback(Points, discreteMapped, output, failed => ContinuousTransform.InverseTransform(failed));
            return output;
        }

        public bool TryInverseTransform(in Vector2 Point, out Vector2 v)
        {
            v = InverseTransform(Point);
            return true;
        }

        public bool[] TryInverseTransform(in Vector2[] Points, out Vector2[] v)
        {
            v = InverseTransform(Points);
            return AllTrue(v.Length);
        }

        /// <summary>
        /// Overwrites <paramref name="output"/> at every index the discrete transform rejected with the continuous result for that
        /// point. <paramref name="output"/> must be the array the discrete batch call just returned, which this class owns.
        /// </summary>
        private static void ApplyFallback(Vector2[] points, bool[] discreteMapped, Vector2[] output, Func<Vector2[], Vector2[]> continuousBatch)
        {
            int failedCount = 0;
            for (int i = 0; i < discreteMapped.Length; i++)
            {
                if (!discreteMapped[i])
                    failedCount++;
            }

            if (failedCount == 0)
                return;

            Vector2[] failedPoints = new Vector2[failedCount];
            int next = 0;
            for (int i = 0; i < discreteMapped.Length; i++)
            {
                if (!discreteMapped[i])
                    failedPoints[next++] = points[i];
            }

            Vector2[] fallbackResults = continuousBatch(failedPoints);

            next = 0;
            for (int i = 0; i < discreteMapped.Length; i++)
            {
                if (!discreteMapped[i])
                    output[i] = fallbackResults[next++];
            }
        }

        private static bool[] AllTrue(int length)
        {
            bool[] result = new bool[length];
            for (int i = 0; i < result.Length; i++)
                result[i] = true;

            return result;
        }

        public void Translate(in Vector2 vector) => throw new NotImplementedException();

        public void MinimizeMemory()
        {
            (DiscreteTransform as IMemoryMinimization)?.MinimizeMemory();
            (ContinuousTransform as IMemoryMinimization)?.MinimizeMemory();
        }

        public List<MappingVector2> IntersectingControlRectangle(in Rectangle gridRect) => ((ITransformControlPoints)DiscreteTransform).IntersectingControlRectangle(gridRect);

        public List<MappingVector2> IntersectingMappedRectangle(in Rectangle gridRect) => ((ITransformControlPoints)DiscreteTransform).IntersectingMappedRectangle(gridRect);
    }
}
