using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationService.Types;
using System;

namespace WebAnnotationModel.Objects
{
    public readonly struct PermittedStructureLinkKey : IEquatable<PermittedStructureLinkKey>, IComparable<PermittedStructureLinkKey>, IPermittedStructureLink
    {
        readonly long _SourceTypeID;
        readonly long _TargetTypeID;
        readonly bool _Bidirectional;

        //public long SourceTypeID { get { return link != null ? link.SourceTypeID : _SourceID;  } }
        //public long TargetTypeID { get { return link != null ? link.TargetTypeID : _TargetID; } }

        public long SourceTypeID { get { return _SourceTypeID; } }
        public long TargetTypeID { get { return _TargetTypeID; } }

        public bool Bidirectional { get { return _Bidirectional; } }

        ulong IPermittedStructureLink.SourceTypeID => (ulong)SourceTypeID;

        ulong IPermittedStructureLink.TargetTypeID => (ulong)TargetTypeID;

        bool IPermittedStructureLink.Directional => !this.Bidirectional;

        public PermittedStructureLinkKey(PermittedStructureLink obj) : this(obj.SourceTypeID, obj.TargetTypeID, obj.Bidirectional)
        {
        }

        public PermittedStructureLinkKey(PermittedStructureLinkObj obj) : this(obj.SourceTypeID, obj.TargetTypeID, obj.Bidirectional)
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
            if (!(obj is IPermittedStructureLink other))
                return false;

            return ((IPermittedStructureLink)this).Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)(SourceTypeID % int.MaxValue);
        }

        public bool Equals(PermittedStructureLinkKey other)
        {
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

        public int CompareTo(PermittedStructureLinkKey other)
        {  
            if (this.Bidirectional == other.Bidirectional && this.Bidirectional)
            {
                var A_Low = Math.Min(SourceTypeID, TargetTypeID);
                var A_High = Math.Max(SourceTypeID, TargetTypeID);

                var B_Low = Math.Min(other.SourceTypeID, other.TargetTypeID);
                var B_High = Math.Max(other.SourceTypeID, other.TargetTypeID);

                int LowCompare = A_Low.CompareTo(B_Low);
                if (LowCompare != 0)
                    return LowCompare;

                int HighCompare = B_High.CompareTo(B_High);
                if (HighCompare != 0)
                    return HighCompare;
            }
            else
            {
                int SourceCompare = this.SourceTypeID.CompareTo(other.SourceTypeID);
                if (SourceCompare != 0)
                    return SourceCompare;

                int TargetCompare = this.TargetTypeID.CompareTo(other.TargetTypeID);
                if (TargetCompare != 0)
                    return TargetCompare;
            }

            return this.Bidirectional.CompareTo(other.Bidirectional);
        }

        bool IEquatable<IPermittedStructureLink>.Equals(IPermittedStructureLink other)
        {
            if (other is null)
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

    public class PermittedStructureLinkObj : WCFObjBaseWithKey<PermittedStructureLinkKey,
        PermittedStructureLink>,
        IPermittedStructureLink
    {
        public override PermittedStructureLinkKey ID
        {
            get { return new PermittedStructureLinkKey(this); }
        }

        protected override int GenerateHashCode()
        {
            return (int)(SourceTypeID % int.MaxValue);
        }

        public long SourceTypeID
        {
            get
            {
                return Data.SourceTypeID;
            }
        }

        public long TargetTypeID
        {
            get
            {
                return Data.TargetTypeID;
            }
        }

        public bool Bidirectional
        {
            get { return Data.Bidirectional; }
        }

        ulong IPermittedStructureLink.SourceTypeID => (ulong)SourceTypeID;

        ulong IPermittedStructureLink.TargetTypeID => (ulong)TargetTypeID;

        bool IPermittedStructureLink.Directional => !Bidirectional;

        public override string ToString()
        {
            if (Bidirectional)
                return string.Format("{0} <-> {1}", SourceTypeID, TargetTypeID);
            else
                return string.Format("{0} -> {1}", SourceTypeID, TargetTypeID);
        }

        public bool Equals(IPermittedStructureLink other)
        {
            return ((IPermittedStructureLink)ID).Equals(other);
        }

        public PermittedStructureLinkObj(long SourceTypeID, long TargetTypeID,
                                bool Bidirectional) : base()
        {
            this.Data = new PermittedStructureLink();
            if (Bidirectional)
            {
                this.Data.SourceTypeID = Math.Min(SourceTypeID, TargetTypeID);
                this.Data.TargetTypeID = Math.Max(SourceTypeID, TargetTypeID);
            }
            else
            {
                this.Data.SourceTypeID = SourceTypeID;
                this.Data.TargetTypeID = TargetTypeID;
            }
            this.Data.Bidirectional = Bidirectional;


            this.DBAction = AnnotationService.Types.DBACTION.INSERT;
        }

        public PermittedStructureLinkObj(PermittedStructureLink data)
        {
            this.Data = data;
        }

        public PermittedStructureLinkObj()
        {
        }
    }
}
