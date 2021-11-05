using System;
using Viking.AnnotationServiceTypes.Interfaces;

namespace WebAnnotationModel.Objects
{
    public abstract class AnnotationModelObjBaseWithKey<KEY, SERVER_INTERFACE> : AnnotationModelObjBase<SERVER_INTERFACE>,
        IEquatable<AnnotationModelObjBaseWithKey<KEY, SERVER_INTERFACE>>, IDataObjectWithKey<KEY>
        where KEY : struct, IComparable<KEY>, IEquatable<KEY>
    {
        public virtual KEY ID { get; protected set; }
        KEY IDataObjectWithKey<KEY>.ID
        {
            get => ID;
            set => ID = value;
        }

        public override string ToString()
        { 
            return ID.ToString();
        }

        public override bool Equals(object obj)
        {
            AnnotationModelObjBaseWithKey<KEY, SERVER_INTERFACE> locObj = obj as AnnotationModelObjBaseWithKey<KEY, SERVER_INTERFACE>;
            if (locObj != null)
            {
                return this.Equals(locObj);
            }
            else
                return base.Equals(obj);
        }

        public static bool operator ==(AnnotationModelObjBaseWithKey<KEY, SERVER_INTERFACE> A, AnnotationModelObjBaseWithKey<KEY, SERVER_INTERFACE> B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if ((object)A != null)
                return A.Equals(B);

            return false;
        }

        public static bool operator !=(AnnotationModelObjBaseWithKey<KEY, SERVER_INTERFACE> A, AnnotationModelObjBaseWithKey<KEY, SERVER_INTERFACE> B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return false;
            }

            if ((object)A != null)
                return !A.Equals(B);

            return true;
        }

        protected abstract int GenerateHashCode();
        int? _HashCode; 

        /// <summary>
        /// The ID for newo bjects can change from a negative number to the number in the database.
        /// In this case make sure we always return the same hash code.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            if (_HashCode.HasValue)
                return _HashCode.Value;
            else
            {
                _HashCode = new int?(GenerateHashCode());
                return _HashCode.Value;
            }
        }

        public bool Equals(AnnotationModelObjBaseWithKey<KEY, SERVER_INTERFACE> other)
        {
            if ((object)other == null)
                return false;

            return this.ID.Equals(other.ID);
        }
    }
}
