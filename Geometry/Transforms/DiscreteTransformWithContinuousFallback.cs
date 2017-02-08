using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.IO;

namespace Geometry.Transforms
{
    /// <summary>
    /// A transform which uses a discreet transform where possible, but falls back to a continuous transform for points that cannot be mapped discreetly.
    /// </summary>
    [Serializable]
    public class DiscreteTransformWithContinuousFallback : IContinuousTransform, ITransformInfo, IMemoryMinimization, IControlPointTriangulation
    {
        IDiscreteTransform DiscreteTransform;
        IContinuousTransform ContinuousTransform;

        public override string ToString()
        {
            return this.Info.ToString();
        }

        public TransformInfo Info
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
                if(DiscreteTransform as IControlPointTriangulation != null)
                {
                    return ((IControlPointTriangulation)DiscreteTransform).TriangleIndicies;
                }

                return new int[0];
            }
        }

        public List<int>[] Edges
        {
            get
            {
                if (DiscreteTransform as IControlPointTriangulation != null)
                {
                    return ((IControlPointTriangulation)DiscreteTransform).Edges;
                }

                return new List<int>[] { };
            }
        }

        public DiscreteTransformWithContinuousFallback(IDiscreteTransform discreteTransform, IContinuousTransform continuousTransform, TransformInfo info)
        {
            this.DiscreteTransform = discreteTransform;
            this.ContinuousTransform = continuousTransform;
            this.Info = info;
        }

        protected DiscreteTransformWithContinuousFallback(SerializationInfo info, StreamingContext context) 
        {
            if (info == null)
                throw new ArgumentNullException();

            DiscreteTransform = info.GetValue("DiscreetTransform", typeof(IDiscreteTransform)) as IDiscreteTransform;
            ContinuousTransform = info.GetValue("ContinuousTransform", typeof(IContinuousTransform)) as IContinuousTransform;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException();

            info.AddValue("DiscreetTransform", DiscreteTransform);
            info.AddValue("ContinuousTransform", ContinuousTransform); 
        }  

        public bool CanTransform(GridVector2 p)
        {
            return true;
        }

        public bool CanInverseTransform(GridVector2 p)
        {
            return true;
        }

        public GridVector2 Transform(GridVector2 Point)
        {
            GridVector2 output;
            if(!DiscreteTransform.TryTransform(Point, out output))
            {
                output = ContinuousTransform.Transform(Point);
            }

            return output;
        }

        public GridVector2[] Transform(GridVector2[] Points)
        {
            return Points.Select(p => this.Transform(p)).ToArray();
        }

        public bool TryTransform(GridVector2 Point, out GridVector2 v)
        {
            v = Transform(Point);
            return true;
        }

        public bool[] TryTransform(GridVector2[] Points, out GridVector2[] v)
        {
            v = Transform(Points);
            return v.Select(p => true).ToArray();
        }
        
        public GridVector2 InverseTransform(GridVector2 Point)
        {
            GridVector2 output;
            if (!DiscreteTransform.TryInverseTransform(Point, out output))
            {
                output = ContinuousTransform.InverseTransform(Point);
            }

            return output;
        }

        public GridVector2[] InverseTransform(GridVector2[] Points)
        {
            return Points.Select(p => this.InverseTransform(p)).ToArray();
        }

        public bool TryInverseTransform(GridVector2 Point, out GridVector2 v)
        {
            v = InverseTransform(Point);
            return true;
        }

        public bool[] TryInverseTransform(GridVector2[] Points, out GridVector2[] v)
        {
            v = InverseTransform(Points);
            return v.Select(p => true).ToArray();
        }

        public void Translate(GridVector2 vector)
        {
            throw new NotImplementedException();
        }

        public void MinimizeMemory()
        {
            (DiscreteTransform as IMemoryMinimization)?.MinimizeMemory();
            (ContinuousTransform as IMemoryMinimization)?.MinimizeMemory();
        }

        public List<MappingGridVector2> IntersectingControlRectangle(GridRectangle gridRect)
        {
            return ((ITransformControlPoints)DiscreteTransform).IntersectingControlRectangle(gridRect);
        }

        public List<MappingGridVector2> IntersectingMappedRectangle(GridRectangle gridRect)
        {
            return ((ITransformControlPoints)DiscreteTransform).IntersectingMappedRectangle(gridRect);
        }
    }
}
