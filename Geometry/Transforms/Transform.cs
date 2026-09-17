using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Geometry.Transforms
{
    [Serializable]
    public class IdentityTransform : Geometry.IContinuousTransform
    {
        public bool CanInverseTransform(in Vector2 Point) => true;

        public bool CanTransform(in Vector2 Point) => true;

        public Vector2[] InverseTransform(in Vector2[] Points)
        {
            Vector2[] transformedP = new Vector2[Points.Length];
            Points.CopyTo(transformedP, 0);
            return transformedP;
        }

        public Vector2 InverseTransform(in Vector2 Point) => Point;

        public Vector2[] Transform(in Vector2[] Points)
        {
            Vector2[] transformedP = new Vector2[Points.Length];
            Points.CopyTo(transformedP, 0);
            return transformedP;
        }

        public Vector2 Transform(in Vector2 Point) => Point;

        public void Translate(in Vector2 vector) => throw new NotImplementedException();

        public bool[] TryInverseTransform(in Vector2[] Points, out Vector2[] transformedP)
        {
            transformedP = new Vector2[Points.Length];
            Points.CopyTo(transformedP, 0);
            return [.. transformedP.Select(p => true)];
        }

        public bool TryInverseTransform(in Vector2 Point, out Vector2 v)
        {
            v = Point;
            return true;
        }

        public bool[] TryTransform(in Vector2[] Points, out Vector2[] transformedP)
        {
            transformedP = new Vector2[Points.Length];
            Points.CopyTo(transformedP, 0);
            return [.. transformedP.Select(p => true)];
        }

        public bool TryTransform(in Vector2 Point, out Vector2 v)
        {
            v = Point;
            return true;
        }
    }


    [Serializable]
    public abstract class TransformBase : ISerializable, IMemoryMinimization, ITransformInfo
    {
        public TransformBasicInfo Info { get; set; }

        public override string ToString()
        {
            if (Info != null)
                return Info.ToString();
            else
                return "Transform Base, No Info";
        }

        public abstract bool CanTransform(in Vector2 Point);
        public abstract Vector2 Transform(in Vector2 Point);
        public abstract Vector2[] Transform(in Vector2[] Points);
        public abstract bool TryTransform(in Vector2 Point, out Vector2 v);
        public abstract bool[] TryTransform(in Vector2[] Points, out Vector2[] v);

        public abstract bool CanInverseTransform(in Vector2 Point);
        public abstract Vector2 InverseTransform(in Vector2 Point);
        public abstract Vector2[] InverseTransform(in Vector2[] Points);
        public abstract bool TryInverseTransform(in Vector2 Point, out Vector2 v);
        public abstract bool[] TryInverseTransform(in Vector2[] Points, out Vector2[] v);

        /// <summary>
        /// Adjust the output of the transform by the following vector
        /// </summary>
        /// <param name="vector"></param>
        public abstract void Translate(Vector2 vector);

        protected TransformBase(TransformBasicInfo info)
        {
            Info = info;
        }

        #region ISerializable Members

        protected TransformBase(SerializationInfo info, StreamingContext context)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));

            this.Info = info.GetValue("Info", typeof(TransformBasicInfo)) as TransformBasicInfo;
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));

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
        public static MappingVector2[] TransformControlPoints(this IContinuousTransform BtoC, MappingVector2[] AtoB) => [.. AtoB.Select(mp => new MappingVector2(BtoC.Transform(mp.ControlPoint), mp.MappedPoint))];
        public static IContinuousTransform TransformTransform(this IContinuousTransform BtoC, ITransformControlPoints AtoB)
        {
            StosTransformInfo BtoCInfo = ((ITransformInfo)BtoC)?.Info as StosTransformInfo;
            StosTransformInfo AtoBInfo = ((ITransformInfo)AtoB)?.Info as StosTransformInfo;
            MappingVector2[] newControlPoints = BtoC.TransformControlPoints(AtoB.MapPoints);
            IContinuousTransform rbfTransform = new RBFTransform(newControlPoints,
                StosTransformInfo.Merge(AtoBInfo, BtoCInfo));
            return rbfTransform;
        }

        public static ITransform TransformTransform(this IContinuousTransform BtoC, ITransformControlPoints AtoB, Type transformType)
        {
            StosTransformInfo BtoCInfo = ((ITransformInfo)BtoC)?.Info as StosTransformInfo;
            StosTransformInfo AtoBInfo = ((ITransformInfo)AtoB)?.Info as StosTransformInfo;

            StosTransformInfo AtoCInfo = StosTransformInfo.Merge(AtoBInfo, BtoCInfo);

            MappingVector2[] newControlPoints = BtoC.TransformControlPoints(AtoB.MapPoints);

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
