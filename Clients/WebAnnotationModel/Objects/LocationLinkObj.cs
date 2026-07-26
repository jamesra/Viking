using System;
using Viking.AnnotationServiceTypes.Interfaces; 
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WebAnnotationModel.Objects;
using Viking.AnnotationServiceTypes;

namespace WebAnnotationModel.Objects
{

    public class LocationLinkObj : AnnotationModelObjBaseWithKey<LocationLinkKey, ILocationLink>, IEquatable<LocationLinkObj>, ILocationLinkKey, ILocationLink
    {
        public override LocationLinkKey ID => new LocationLinkKey(this.A, this.B);

        public override string ToString()
        {
            return ID.ToString();
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            if (obj is LocationLinkObj other)
                return Equals(other);

            if (obj is ILocationLinkKey key)
                return Equals(key);

            return base.Equals(obj);
        }

        public bool Equals(LocationLinkObj other)
        {
            if (other is null)
                return false;
            
            return (A == other.A) && (B == other.B);
        }

        protected override int GenerateHashCode()
        {
            return ID.GetHashCode();
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }

        public static bool operator ==(LocationLinkObj A, LocationLinkObj B)
        {
            return object.Equals(A, B);

            /*if (object.Equals(A, B))
                return true;
            if (object.Equals(A, null) || object.Equals(B, null))
                return false;  

            return (A.ID == B.ID);
            */
        }

        public static bool operator !=(LocationLinkObj A, LocationLinkObj B)
        {
            return !object.Equals(A, B);
            /*
            if (object.Equals(A, B))
                return false;
            if(object.Equals(A,null) || object.Equals(B,null))                
                return true; 

            return !((A.ID == B.ID));
            */
        }

        public int CompareTo(LocationLinkKey other)
        {
            if (A != other.A)
                return (int)(other.A - A);
            else
                return (int)(other.B - B);
        }

        internal Task<LocationLinkObj> CreateFromServer(ILocationLink newdata)
        {
            LocationLinkObj obj = new LocationLinkObj((long)newdata.A, (long)newdata.B);
            return Task.FromResult(obj);
        }

        internal override Task Update(ILocationLink newdata)
        {
            throw new System.NotImplementedException();
        }

        private long _A { get; }
        private long _B { get; }

        public long A => _A;

        public long B => _B;

        ulong ILocationLinkKey.A => (ulong)_A;

        ulong ILocationLinkKey.B => (ulong)_B;

        ulong ILocationLink.A => (ulong)_A;

        ulong ILocationLink.B => (ulong)_B;

        ILocationLinkKey IDataObjectWithKey<ILocationLinkKey>.ID { get => new LocationLinkKey(_A, _B); set => throw new NotImplementedException(); }

        public LocationLinkObj()
        {  
        }

        public LocationLinkObj(long IDA,
                               long IDB)
        {
            Debug.Assert(IDA != IDB); 
            _A = IDA < IDB ? IDA : IDB;
            _B = IDA < IDB ? IDB : IDA; 
        }

        /// <summary>
        /// Returns the side of the link that doesn't match the passed key.
        /// Throws an exception if the passed key does not match either A or B
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public long OtherKey(long key)
        {
            if (A == key)
                return B;
            if (B == key)
                return A;

            throw new ArgumentException($"{key} is not part of location link {A}-{B}");
        }

        ulong ILocationLinkKey.OtherKey(ulong key)
        {
            return (ulong)OtherKey((long)key);
        }

        bool IEquatable<ILocationLinkKey>.Equals(ILocationLinkKey other)
        {
            if (other is null)
                return false;

            return ((ulong)_A == other.A) && ((ulong)_B == other.B);
        }

        int IComparable<ILocationLinkKey>.CompareTo(ILocationLinkKey other)
        {
            if ((ulong)_A != other.A)
                return (int)(other.A - (ulong)_A);
            else
                return (int)(other.B - (ulong)_B);
        }

        ulong ILocationLink.OtherKey(ulong key)
        {
            return (ulong)OtherKey((long)key);
        }

        bool IEquatable<ILocationLink>.Equals(ILocationLink other)
        {
            if (other is null)
                return false;

            return ((ulong)_A == other.A) && ((ulong)_B == other.B);
        }
    }
}
