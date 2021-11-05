using Viking.AnnotationServiceTypes.Interfaces;
using System;

namespace Viking.AnnotationServiceTypes
{
    public readonly struct StructureLinkKey : IEquatable<StructureLinkKey>, IComparable<StructureLinkKey>, IStructureLinkKey, IComparable<IStructureLinkKey>, IEquatable<IStructureLinkKey>
    {
        readonly long _SourceID;
        readonly long _TargetID;
        readonly bool _Bidirectional;

        //public long SourceID { get { return link != null ? link.SourceID : _SourceID;  } }
        //public long TargetID { get { return link != null ? link.TargetID : _TargetID; } }

        public long SourceID => _SourceID;
        public long TargetID => _TargetID;

        public bool Bidirectional => _Bidirectional;

        ulong IStructureLinkKey.SourceID => (ulong)SourceID;

        ulong IStructureLinkKey.TargetID => (ulong)TargetID;

        bool IStructureLinkKey.Directional => this.Bidirectional == false;
         

        public StructureLinkKey(IStructureLink obj) : this((long)obj.SourceID, (long)obj.TargetID, !obj.Directional)
        {
        }
         
        public StructureLinkKey(long SourceID, long TargetID, bool Bidirectional)
        {
            //We have UI to toggle bidirectional on and off.  I consider a bidirectional link
            //equal if both IDs are the same even if the order is reversed on the other link.
            //However in the database it is not handled that way.
            _SourceID = SourceID;
            _TargetID = TargetID;
            _Bidirectional = Bidirectional;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            IStructureLinkKey other = obj as IStructureLinkKey;
            if (other == null)
                return false;

            return ((IStructureLinkKey)this).Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)(SourceID % int.MaxValue);
        }

        public bool Equals(StructureLinkKey other)
        {
            if ((object)other == null)
                return false;

            if (this.Bidirectional == other.Bidirectional && this.Bidirectional)
            {
                if (SourceID == other.SourceID &&
                    TargetID == other.TargetID)
                    return true;
                else if (SourceID == other.TargetID &&
                         TargetID == other.SourceID)
                    return true;

                return false;
            }
            else
                return this.SourceID == other.SourceID && this.TargetID == other.TargetID && this.Bidirectional == other.Bidirectional;
        }

        public int CompareTo(StructureLinkKey other)
        {
            return CompareTo((IStructureLinkKey)other);
        }

        public static int Compare(IStructureLinkKey A, IStructureLinkKey B)
        {
            if (A is null && B is null)
                return 0;

            if (A is null)
                return 1;

            if (B is null)
                return -1;

            if (A.Directional.Equals(B.Directional) && !A.Directional)
            {
                var A_Low = Math.Min(A.SourceID, A.TargetID);
                var A_High = Math.Max(A.SourceID, A.TargetID);

                var B_Low = Math.Min(B.SourceID, B.TargetID);
                var B_High = Math.Max(B.SourceID, B.TargetID);

                int lowCompare = A_Low.CompareTo(B_Low);
                if (lowCompare != 0)
                    return lowCompare;

                int highCompare = B_High.CompareTo(B_High);
                if (highCompare != 0)
                    return highCompare;
            }
            else
            {
                int sourceCompare = A.SourceID.CompareTo(B.SourceID);
                if (sourceCompare != 0)
                    return sourceCompare;

                int targetCompare = A.TargetID.CompareTo(B.TargetID);
                if (targetCompare != 0)
                    return targetCompare;
            }

            return A.Directional.CompareTo(B.Directional);
        }

        public int CompareTo(IStructureLinkKey other)
        {
            return Compare(this, other);
        }
          
        bool IEquatable<IStructureLinkKey>.Equals(IStructureLinkKey other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (!this.Bidirectional == other.Directional && this.Bidirectional)
            {
                if ((ulong)SourceID == other.SourceID &&
                    (ulong)TargetID == other.TargetID)
                    return true;
                else if ((ulong)SourceID == other.TargetID &&
                         (ulong)TargetID == other.SourceID)
                    return true;

                return false;
            }
            else
                return other.SourceID == (ulong)this.SourceID &&
                   other.TargetID == (ulong)this.TargetID &&
                   other.Directional == !this.Bidirectional;
        }

        public static bool operator ==(StructureLinkKey A, StructureLinkKey B)
        {
            if (A.Bidirectional == B.Bidirectional && A.Bidirectional)
            {
                if (A.SourceID == B.SourceID &&
                    A.TargetID == B.TargetID)
                    return true;
                else if (A.SourceID == B.TargetID &&
                         A.TargetID == B.SourceID)
                    return true;

                return false;
            }
            else
                return (A.SourceID == B.SourceID) && (A.TargetID == B.TargetID) && (A.Bidirectional == B.Bidirectional);
        }

        public static bool operator !=(StructureLinkKey A, StructureLinkKey B)
        {
            if (A.Bidirectional == B.Bidirectional && A.Bidirectional)
            {
                if (A.SourceID == B.SourceID &&
                    A.TargetID == B.TargetID)
                    return false;
                else if (A.SourceID == B.TargetID &&
                         A.TargetID == B.SourceID)
                    return false;

                return true;
            }
            else
                return !((A.SourceID == B.SourceID) && (A.TargetID == B.TargetID) && (A.Bidirectional == B.Bidirectional));
        }
    }
}
