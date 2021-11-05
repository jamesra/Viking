using Viking.AnnotationServiceTypes.Interfaces;
using System;

namespace Viking.AnnotationServiceTypes
{
    public readonly struct PermittedStructureLinkKey : IEquatable<PermittedStructureLinkKey>, IComparable<IPermittedStructureLinkKey>, IPermittedStructureLinkKey, IComparable<PermittedStructureLinkKey>
    {
        readonly long _SourceTypeID;
        readonly long _TargetTypeID;
        readonly bool _Bidirectional;

        //public long SourceTypeID { get { return link != null ? link.SourceTypeID : _SourceID;  } }
        //public long TargetTypeID { get { return link != null ? link.TargetTypeID : _TargetID; } }

        public long SourceTypeID { get { return _SourceTypeID; } }
        public long TargetTypeID { get { return _TargetTypeID; } }

        public bool Bidirectional { get { return _Bidirectional; } }

        ulong IPermittedStructureLinkKey.SourceTypeID => (ulong)SourceTypeID;

        ulong IPermittedStructureLinkKey.TargetTypeID => (ulong)TargetTypeID;

        bool IPermittedStructureLinkKey.Directional => !this.Bidirectional;

        public PermittedStructureLinkKey(IPermittedStructureLink obj) : this((long)obj.SourceTypeID, (long)obj.TargetTypeID, !obj.Directional)
        {
        }
         

        public PermittedStructureLinkKey(long SourceTypeID, long TargetTypeID, bool Bidirectional)
        {
            //We don't have a UI to toggle bidirectional, so for the sake of having unique keys
            //I set the lower ID value as the source so comparisons are simple in the Database
            if (Bidirectional)
            {
                _SourceTypeID = Math.Min(SourceTypeID, TargetTypeID);
                _TargetTypeID = Math.Max(SourceTypeID, TargetTypeID);
            }
            else
            {
                _SourceTypeID = SourceTypeID;
                _TargetTypeID = TargetTypeID;
            }

            _Bidirectional = Bidirectional;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            IPermittedStructureLinkKey other = obj as IPermittedStructureLinkKey;
            if (other == null)
                return false;

            return ((IPermittedStructureLinkKey)this).Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)(SourceTypeID % int.MaxValue);
        }

        public bool Equals(PermittedStructureLinkKey other)
        {
            if ((object)other == null)
                return false;

            if (this.Bidirectional == other.Bidirectional && this.Bidirectional)
            {
                if (SourceTypeID == other.SourceTypeID &&
                    TargetTypeID == other.TargetTypeID)
                    return true;
                else if (SourceTypeID == other.TargetTypeID &&
                         TargetTypeID == other.SourceTypeID)
                    return true;

                return false;
            }
            else
                return this.SourceTypeID == other.SourceTypeID && this.TargetTypeID == other.TargetTypeID && this.Bidirectional == other.Bidirectional;
        }

        

        bool IEquatable<IPermittedStructureLinkKey>.Equals(IPermittedStructureLinkKey other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (!this.Bidirectional == other.Directional && this.Bidirectional)
            {
                if ((ulong)SourceTypeID == other.SourceTypeID &&
                    (ulong)TargetTypeID == other.TargetTypeID)
                    return true;
                else if ((ulong)SourceTypeID == other.TargetTypeID &&
                         (ulong)TargetTypeID == other.SourceTypeID)
                    return true;

                return false;
            }
            else
                return other.SourceTypeID == (ulong)this.SourceTypeID &&
                   other.TargetTypeID == (ulong)this.TargetTypeID &&
                   other.Directional == !this.Bidirectional;
        }

        public int CompareTo(PermittedStructureLinkKey other)
        {
            return PermittedStructureLinkKey.Compare(this, other);
        }

        public int CompareTo(IPermittedStructureLinkKey other)
        {
            return PermittedStructureLinkKey.Compare(this, other);
        }

        public static int Compare(IPermittedStructureLinkKey A, IPermittedStructureLinkKey B)
        {
            if (A is null && B is null)
                return 0;

            if (A is null)
                return 1;

            if (B is null)
                return -1;

            if (A.Directional.Equals(B.Directional) && !A.Directional)
            {
                var A_Low = Math.Min(A.SourceTypeID, A.TargetTypeID);
                var A_High = Math.Max(A.SourceTypeID, A.TargetTypeID);

                var B_Low = Math.Min(B.SourceTypeID, B.TargetTypeID);
                var B_High = Math.Max(B.SourceTypeID, B.TargetTypeID);

                int lowCompare = A_Low.CompareTo(B_Low);
                if (lowCompare != 0)
                    return lowCompare;

                int highCompare = B_High.CompareTo(B_High);
                if (highCompare != 0)
                    return highCompare;
            }
            else
            {
                int sourceCompare = A.SourceTypeID.CompareTo(B.SourceTypeID);
                if (sourceCompare != 0)
                    return sourceCompare;

                int targetCompare = A.TargetTypeID.CompareTo(B.TargetTypeID);
                if (targetCompare != 0)
                    return targetCompare;
            }

            return A.Directional.CompareTo(B.Directional);
        }
         

        public static bool operator ==(PermittedStructureLinkKey A, PermittedStructureLinkKey B)
        {
            if (A.Bidirectional == B.Bidirectional && A.Bidirectional)
            {
                if (A.SourceTypeID == B.SourceTypeID &&
                    A.TargetTypeID == B.TargetTypeID)
                    return true;
                else if (A.SourceTypeID == B.TargetTypeID &&
                         A.TargetTypeID == B.SourceTypeID)
                    return true;

                return false;
            }
            else
                return (A.SourceTypeID == B.SourceTypeID) && (A.TargetTypeID == B.TargetTypeID) && (A.Bidirectional == B.Bidirectional);

        }

        public static bool operator !=(PermittedStructureLinkKey A, PermittedStructureLinkKey B)
        {
            if (A.Bidirectional == B.Bidirectional && A.Bidirectional)
            {
                if (A.SourceTypeID == B.SourceTypeID &&
                    A.TargetTypeID == B.TargetTypeID)
                    return false;
                else if (A.SourceTypeID == B.TargetTypeID &&
                         A.TargetTypeID == B.SourceTypeID)
                    return false;

                return true;
            }
            else
                return !((A.SourceTypeID == B.SourceTypeID) && (A.TargetTypeID == B.TargetTypeID) && (A.Bidirectional == B.Bidirectional));
        }
    }
}
