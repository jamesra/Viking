using Viking.AnnotationServiceTypes.Interfaces; 
using System;
using System.Diagnostics;
using Viking.AnnotationServiceTypes;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{
    public struct readonly LocationLinkKey : IComparable<LocationLinkKey>, IEquatable<LocationLinkKey>, ILocationLinkKey
    {
        public readonly long A;
        public readonly long B;

        ulong ILocationLinkKey.A => (ulong)A;

        ulong ILocationLinkKey.B => (ulong)B;

        public LocationLinkKey(long a, long b)
        {
            Debug.Assert(a != b);
            A = a < b ? a : b;
            B = b < a ? a : b;
        }

        public LocationLinkKey(LocationLinkObj obj)
        {
            this.A = obj.A;
            this.B = obj.B;
        }

        public override bool Equals(object obj)
        {
            if (System.Object.ReferenceEquals(this, obj))
                return true;
            if ((object)obj == null)
                return false;
            if (!typeof(LocationLinkKey).IsInstanceOfType(obj))
                return false;

            LocationLinkKey other = (LocationLinkKey)obj;

            return (A == other.A) && (B == other.B);
        }

        public override string ToString()
        {
            return A.ToString() + " - " + B.ToString();
        }

        public override int GetHashCode()
        {
            return (int)(A % int.MaxValue);
        }

        public static bool operator ==(LocationLinkKey A, LocationLinkKey B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if ((object)A != null)
                return A.Equals(B);

            return false;
        }

        public static bool operator !=(LocationLinkKey A, LocationLinkKey B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return false;
            }

            if ((object)A != null)
                return !A.Equals(B);

            return true;
        }

        public int CompareTo(LocationLinkKey other)
        {
            if (A != other.A)
                return (int)(other.A - A);
            else
                return (int)(other.B - B);
        }

        public bool Equals(LocationLinkKey other)
        {
            if ((object)other == null)
                return false;

            return (this.A == other.A && this.B == other.B);
        }

        bool IEquatable<ILocationLinkKey>.Equals(ILocationLinkKey other)
        {
            if ((object)other == null)
                return false;

            return ((ulong)this.A == other.A && (ulong)this.B == other.B) || ((ulong)this.B == other.A && (ulong)this.A == other.B);
        }
    }

    public class LocationLinkObj : WCFObjBaseWithKey<LocationLinkKey, LocationLink>
    {
        public override LocationLinkKey ID
        {
            get { return new LocationLinkKey(this); }
        }

        public override string ToString()
        {
            return ID.ToString();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (!this.GetType().IsInstanceOfType(obj))
                return false;

            LocationLinkObj other = (LocationLinkObj)obj;

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

        public long A
        {
            get { return Data.SourceID < Data.TargetID ? Data.SourceID : Data.TargetID; }
        }

        public long B
        {
            get { return Data.SourceID > Data.TargetID ? Data.SourceID : Data.TargetID; }
        }

        public LocationLinkObj()
        {
            LocationLink link = new LocationLink();
            this.Data = link;
        }

        public LocationLinkObj(long IDA,
                               long IDB)
        {
            Debug.Assert(IDA != IDB);
            LocationLink link = new LocationLink();
            link.SourceID = IDA < IDB ? IDA : IDB;
            link.TargetID = IDA < IDB ? IDB : IDA;
            this.Data = link;
        }

        public LocationLinkObj(LocationLink link)
        {
            this.Data = link;
        }
    }
}
