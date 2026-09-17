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
    public class DiscreteTransformWithContinuousFallback : IContinuousTransform, ITransformInfo, IMemoryMinimization, IControlPointTriangulation
    {
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

        public Vector2[] Transform(in Vector2[] Points) => [.. Points.Select(p => this.Transform(p))];

        public bool TryTransform(in Vector2 Point, out Vector2 v)
        {
            v = Transform(Point);
            return true;
        }

        public bool[] TryTransform(in Vector2[] Points, out Vector2[] v)
        {
            v = Transform(Points);
            return [.. v.Select(p => true)];
        }

        public Vector2 InverseTransform(in Vector2 Point)
        {
            if (!DiscreteTransform.TryInverseTransform(Point, out Vector2 output))
            {
                output = ContinuousTransform.InverseTransform(Point);
            }

            return output;
        }

        public Vector2[] InverseTransform(in Vector2[] Points) => [.. Points.Select(p => this.InverseTransform(p))];

        public bool TryInverseTransform(in Vector2 Point, out Vector2 v)
        {
            v = InverseTransform(Point);
            return true;
        }

        public bool[] TryInverseTransform(in Vector2[] Points, out Vector2[] v)
        {
            v = InverseTransform(Points);
            return [.. v.Select(p => true)];
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
