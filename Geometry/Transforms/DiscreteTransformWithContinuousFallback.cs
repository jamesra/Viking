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

        public override string ToString()
        {
            return this.Info.ToString();
        }

        public TransformBasicInfo Info
        {
            get; set;
        }

        public MappingGridVector2[] MapPoints
        {
            get
            {
                return ((ITransformControlPoints)DiscreteTransform).MapPoints;
            }
        }

        public GridRectangle ControlBounds
        {
            get
            {
                return ((ITransformControlPoints)DiscreteTransform).ControlBounds;
            }
        }

        public GridRectangle MappedBounds
        {
            get
            {
                return ((ITransformControlPoints)DiscreteTransform).MappedBounds;
            }
        }

        public int[] TriangleIndicies
        {
            get
            {
                if (DiscreteTransform is IControlPointTriangulation dt)
                {
                    return dt.TriangleIndicies;
                }

                return Array.Empty<int>();
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

                return Array.Empty<List<int>>();
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
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            DiscreteTransform = info.GetValue("DiscreetTransform", typeof(IDiscreteTransform)) as IDiscreteTransform;
            ContinuousTransform = info.GetValue("ContinuousTransform", typeof(IContinuousTransform)) as IContinuousTransform;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            info.AddValue("DiscreetTransform", DiscreteTransform);
            info.AddValue("ContinuousTransform", ContinuousTransform);
        }

        public bool CanTransform(in GridVector2 p)
        {
            return true;
        }

        public bool CanInverseTransform(in GridVector2 p)
        {
            return true;
        }

        public GridVector2 Transform(in GridVector2 Point)
        {
            if (!DiscreteTransform.TryTransform(Point, out GridVector2 output))
            {
                output = ContinuousTransform.Transform(Point);
            }

            return output;
        }

        public GridVector2[] Transform(in GridVector2[] Points)
        {
            return Points.Select(p => this.Transform(p)).ToArray();
        }

        public bool TryTransform(in GridVector2 Point, out GridVector2 v)
        {
            v = Transform(Point);
            return true;
        }

        public bool[] TryTransform(in GridVector2[] Points, out GridVector2[] v)
        {
            v = Transform(Points);
            return v.Select(p => true).ToArray();
        }

        public GridVector2 InverseTransform(in GridVector2 Point)
        {
            if (!DiscreteTransform.TryInverseTransform(Point, out GridVector2 output))
            {
                output = ContinuousTransform.InverseTransform(Point);
            }

            return output;
        }

        public GridVector2[] InverseTransform(in GridVector2[] Points)
        {
            return Points.Select(p => this.InverseTransform(p)).ToArray();
        }

        public bool TryInverseTransform(in GridVector2 Point, out GridVector2 v)
        {
            v = InverseTransform(Point);
            return true;
        }

        public bool[] TryInverseTransform(in GridVector2[] Points, out GridVector2[] v)
        {
            v = InverseTransform(Points);
            return v.Select(p => true).ToArray();
        }

        public void Translate(in GridVector2 vector)
        {
            throw new NotImplementedException();
        }

        public void MinimizeMemory()
        {
            (DiscreteTransform as IMemoryMinimization)?.MinimizeMemory();
            (ContinuousTransform as IMemoryMinimization)?.MinimizeMemory();
        }

        public List<MappingGridVector2> IntersectingControlRectangle(in GridRectangle gridRect)
        {
            return ((ITransformControlPoints)DiscreteTransform).IntersectingControlRectangle(gridRect);
        }

        public List<MappingGridVector2> IntersectingMappedRectangle(in GridRectangle gridRect)
        {
            return ((ITransformControlPoints)DiscreteTransform).IntersectingMappedRectangle(gridRect);
        }
    }
}
