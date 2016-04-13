using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization; 

namespace Geometry.Transforms
{
    [Serializable]
    public class IdentityTransform : Geometry.ITransform
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
    abstract public class TransformBase : Geometry.ITransform, ISerializable
    {
        public TransformInfo Info { get; internal set; }

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

        public virtual void SaveMosaic(System.IO.StreamWriter stream)
        {
            throw new NotImplementedException("No implementation for SaveMosaic"); 
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


        #endregion
    }
}
