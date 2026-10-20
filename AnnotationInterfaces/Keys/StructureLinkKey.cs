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
            unchecked
            {
                long lo = SourceID;
                long hi = TargetID;
                if (Bidirectional && lo > hi)
                {
                    long swap = lo;
                    lo = hi;
                    hi = swap;
                }

                int hash = 17;
                hash = hash * 23 + lo.GetHashCode();
                hash = hash * 23 + hi.GetHashCode();
                hash = hash * 23 + Bidirectional.GetHashCode();
                return hash;
            }
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

        /// <summary>
        /// Orders by the canonical endpoint pair (lower ID first for a bidirectional key), then directional before
        /// bidirectional. Returns 0 exactly when the keys are the same link, including reversed bidirectional keys, so a
        /// SortedSet of keys (KeyTracker) keeps two bidirectional links that share their lower ID.
        /// </summary>
        public static int Compare(IStructureLinkKey A, IStructureLinkKey B)
        {
            if (A is null && B is null)
                return 0;

            if (A is null)
                return 1;

            if (B is null)
                return -1;

            int firstCompare = First(A).CompareTo(First(B));
            if (firstCompare != 0)
                return firstCompare;

            int secondCompare = Second(A).CompareTo(Second(B));
            if (secondCompare != 0)
                return secondCompare;

            return A.Directional.CompareTo(B.Directional);
        }

        /// <summary>The first endpoint in canonical order: the lower ID of a bidirectional key, else the source.</summary>
        private static ulong First(IStructureLinkKey key) => key.Directional ? key.SourceID : Math.Min(key.SourceID, key.TargetID);

        /// <summary>The second endpoint in canonical order: the higher ID of a bidirectional key, else the target.</summary>
        private static ulong Second(IStructureLinkKey key) => key.Directional ? key.TargetID : Math.Max(key.SourceID, key.TargetID);

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

        public static bool operator ==(StructureLinkKey A, StructureLinkKey B) => A.Equals(B);

        public static bool operator !=(StructureLinkKey A, StructureLinkKey B) => !A.Equals(B);
    }
}
