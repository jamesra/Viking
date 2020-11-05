using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Geometry.Transforms
{
    [Serializable]
    public class IdentityTransform : Geometry.IContinuousTransform
    {
        public bool CanInverseTransform(GridVector2 Point)
        {
            return true;
        }

        public bool CanTransform(GridVector2 Point)
        {
            return true;
        }

        public GridVector2[] InverseTransform(GridVector2[] Points)
        {
            GridVector2[] transformedP = new GridVector2[Points.Length];
            Points.CopyTo(transformedP, 0);
            return transformedP;
        }

        public GridVector2 InverseTransform(GridVector2 Point)
        {
            return Point;
        }

        public GridVector2[] Transform(GridVector2[] Points)
        {
            GridVector2[] transformedP = new GridVector2[Points.Length];
            Points.CopyTo(transformedP, 0);
            return transformedP;
        }

        public GridVector2 Transform(GridVector2 Point)
        {
            return Point;
        }

        public void Translate(GridVector2 vector)
        {
            throw new NotImplementedException();
        }

        public bool[] TryInverseTransform(GridVector2[] Points, out GridVector2[] transformedP)
        {
            transformedP = new GridVector2[Points.Length];
            Points.CopyTo(transformedP, 0);
            return transformedP.Select(p => true).ToArray();
        }

        public bool TryInverseTransform(GridVector2 Point, out GridVector2 v)
        {
            v = Point;
            return true;
        }

        public bool[] TryTransform(GridVector2[] Points, out GridVector2[] transformedP)
        {
            transformedP = new GridVector2[Points.Length];
            Points.CopyTo(transformedP, 0);
            return transformedP.Select(p => true).ToArray();
        }

        public bool TryTransform(GridVector2 Point, out GridVector2 v)
        {
            v = Point;
            return true;
        }
    }


    [Serializable]
    abstract public class TransformBase : ISerializable, IMemoryMinimization, ITransformInfo
    {
        public TransformInfo Info { get; set; }

        public override string ToString()
        {
            if (Info != null)
                return Info.ToString();
            else
                return "Transform Base, No Info";
        }

        abstract public bool CanTransform(GridVector2 Point);
        abstract public GridVector2 Transform(GridVector2 Point);
        abstract public GridVector2[] Transform(GridVector2[] Points);
        abstract public bool TryTransform(GridVector2 Point, out GridVector2 v);
        abstract public bool[] TryTransform(GridVector2[] Points, out GridVector2[] v);

        abstract public bool CanInverseTransform(GridVector2 Point);
        abstract public GridVector2 InverseTransform(GridVector2 Point);
        abstract public GridVector2[] InverseTransform(GridVector2[] Points);
        abstract public bool TryInverseTransform(GridVector2 Point, out GridVector2 v);
        abstract public bool[] TryInverseTransform(GridVector2[] Points, out GridVector2[] v);

        /// <summary>
        /// Adjust the output of the transform by the following vector
        /// </summary>
        /// <param name="vector"></param>
        abstract public void Translate(GridVector2 vector);

        protected TransformBase(TransformInfo info)
        {
            Info = info;
        }

        #region ISerializable Members

        protected TransformBase(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException();

            this.Info = info.GetValue("Info", typeof(TransformInfo)) as TransformInfo;
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException();

            info.AddValue("Info", this.Info);
        }

        /// <summary>
        /// Function to call to minimize the memory use of transforms
        /// </summary>
        public abstract void MinimizeMemory();


        #endregion
    }

    public static class TransformExtensions
    {
        /// <summary>
        /// Given three spaces: A,B,C, a transform mapping from B to C, and control points in A & B
        /// Returns control points mapping A to C
        /// </summary>
        /// <param name="BtoC"></param>
        /// <param name="AtoB"></param>
        /// <returns></returns>
        public static MappingGridVector2[] TransformControlPoints(this IContinuousTransform BtoC, MappingGridVector2[] AtoB)
        {
            return AtoB.Select(mp => new MappingGridVector2(BtoC.Transform(mp.ControlPoint), mp.MappedPoint)).ToArray();
        }
        public static IContinuousTransform TransformTransform(this IContinuousTransform BtoC, ITransformControlPoints AtoB)
        {
            StosTransformInfo BtoCInfo = ((ITransformInfo)BtoC)?.Info as StosTransformInfo;
            StosTransformInfo AtoBInfo = ((ITransformInfo)AtoB)?.Info as StosTransformInfo;
            MappingGridVector2[] newControlPoints = BtoC.TransformControlPoints(AtoB.MapPoints);
            IContinuousTransform rbfTransform = new RBFTransform(newControlPoints,
                StosTransformInfo.Merge(AtoBInfo, BtoCInfo));
            return rbfTransform;
        }

        public static ITransform TransformTransform(this IContinuousTransform BtoC, ITransformControlPoints AtoB, Type transformType)
        {
            StosTransformInfo BtoCInfo = ((ITransformInfo)BtoC)?.Info as StosTransformInfo;
            StosTransformInfo AtoBInfo = ((ITransformInfo)AtoB)?.Info as StosTransformInfo;

            StosTransformInfo AtoCInfo = StosTransformInfo.Merge(AtoBInfo, BtoCInfo);

            MappingGridVector2[] newControlPoints = BtoC.TransformControlPoints(AtoB.MapPoints);

            if (transformType == typeof(RBFTransform))
            {
                return new RBFTransform(newControlPoints, AtoCInfo);
            }
            else if (transformType == typeof(GridTransform))
            {
                IGridTransformInfo grid_info = (IGridTransformInfo)AtoB;
                return new GridTransform(newControlPoints, newControlPoints.MappedBounds(), grid_info.GridSizeX, grid_info.GridSizeY, AtoCInfo);
            }
            else if (transformType == typeof(MeshTransform))
            {
                return new MeshTransform(newControlPoints, AtoCInfo);
            }
            else
            {
                return new MeshTransform(newControlPoints, AtoCInfo);
            }

        }
    }
}
