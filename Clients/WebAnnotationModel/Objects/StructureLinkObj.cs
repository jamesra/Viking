using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationService.Types;
using System;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{
    public struct StructureLinkKey : IEquatable<StructureLinkKey>, IComparable<StructureLinkKey>, IStructureLink
    {
        readonly long _SourceID;
        readonly long _TargetID;
        readonly bool _Bidirectional;

        //public long SourceID { get { return link != null ? link.SourceID : _SourceID;  } }
        //public long TargetID { get { return link != null ? link.TargetID : _TargetID; } }

        public long SourceID { get { return _SourceID; } }
        public long TargetID { get { return _TargetID; } }

        public bool Bidirectional { get { return _Bidirectional; } }

        ulong IStructureLink.SourceID => (ulong)SourceID;

        ulong IStructureLink.TargetID => (ulong)TargetID;

        bool IStructureLink.Directional => this.Bidirectional == false;

        public StructureLinkKey(StructureLink obj) : this(obj.SourceID, obj.TargetID, obj.Bidirectional)
        {
        }

        public StructureLinkKey(StructureLinkObj obj) : this(obj.SourceID, obj.TargetID, obj.Bidirectional)
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

            IStructureLink other = obj as IStructureLink;
            if (other == null)
                return false;

            return ((IStructureLink)this).Equals(other);
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
            if ((object)other == null)
                return -1;

            if (this.Bidirectional == other.Bidirectional && this.Bidirectional)
            {
                var A_Low = Math.Min(SourceID, TargetID);
                var A_High = Math.Max(SourceID, TargetID);

                var B_Low = Math.Min(other.SourceID, other.TargetID);
                var B_High = Math.Max(other.SourceID, other.TargetID);

                int LowCompare = A_Low.CompareTo(B_Low);
                if (LowCompare != 0)
                    return LowCompare;

                int HighCompare = B_High.CompareTo(B_High);
                if (HighCompare != 0)
                    return HighCompare;
            }
            else
            {
                int SourceCompare = this.SourceID.CompareTo(other.SourceID);
                if (SourceCompare != 0)
                    return SourceCompare;

                int TargetCompare = this.TargetID.CompareTo(other.TargetID);
                if (TargetCompare != 0)
                    return TargetCompare;
            }

            return this.Bidirectional.CompareTo(other.Bidirectional);
        }

        bool IEquatable<IStructureLink>.Equals(IStructureLink other)
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

    public class StructureLinkObj : WCFObjBaseWithKey<StructureLinkKey, StructureLink>
    {
        public override StructureLinkKey ID
        {
            get { return new StructureLinkKey(this); }
        }

        protected override int GenerateHashCode()
        {
            return (int)(SourceID % int.MaxValue);
        }

        public long SourceID
        {
            get
            {
                return Data.SourceID;
            }
        }

        public long TargetID
        {
            get
            {
                return Data.TargetID;
            }
        }

        public bool Bidirectional
        {
            get { return Data.Bidirectional; }
        }

        public override string ToString()
        {
            if (Bidirectional)
                return string.Format("{0} <-> {1}", SourceID, TargetID);
            else
                return string.Format("{0} -> {1}", SourceID, TargetID);
        }

        public StructureLinkObj(long sourceID, long targetID,
                                bool Bidirectional) : base()
        {
            this.Data = new StructureLink();
            this.Data.SourceID = sourceID;
            this.Data.TargetID = targetID;
            this.Data.Bidirectional = Bidirectional;
            this.DBAction = AnnotationService.Types.DBACTION.INSERT;
        }

        public StructureLinkObj(StructureLink data)
        {
            this.Data = data;
        }

        public StructureLinkObj()
        {
        }
    }
}
