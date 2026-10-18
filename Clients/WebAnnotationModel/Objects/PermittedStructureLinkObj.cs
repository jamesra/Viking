using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationService.Types;
using System;

namespace WebAnnotationModel.Objects
{
    public readonly struct PermittedStructureLinkKey : IEquatable<PermittedStructureLinkKey>, IComparable<PermittedStructureLinkKey>, IPermittedStructureLinkReadOnly
    {
        readonly long _SourceTypeID;
        readonly long _TargetTypeID;
        readonly bool _Bidirectional;

        //public long SourceTypeID { get { return link != null ? link.SourceTypeID : _SourceID;  } }
        //public long TargetTypeID { get { return link != null ? link.TargetTypeID : _TargetID; } }

        public long SourceTypeID => _SourceTypeID;
        public long TargetTypeID => _TargetTypeID;

        public bool Bidirectional => _Bidirectional;

        ulong IPermittedStructureLinkReadOnly.SourceTypeID => (ulong)SourceTypeID;

        ulong IPermittedStructureLinkReadOnly.TargetTypeID => (ulong)TargetTypeID;

        bool IPermittedStructureLinkReadOnly.Directional => !this.Bidirectional;

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
            if (obj is not IPermittedStructureLinkReadOnly other)
                return false;

            return ((IPermittedStructureLinkReadOnly)this).Equals(other);
        }

        /// <summary>
        /// Hashes both endpoints. The constructor stores a bidirectional key lowest ID first, so (A,B) and (B,A)
        /// are already the same fields and equality, hashing and ordering can compare them directly.
        /// </summary>
        public override int GetHashCode() => unchecked((SourceTypeID.GetHashCode() * 397) ^ TargetTypeID.GetHashCode() ^ (Bidirectional ? 1 : 0));

        /// <summary>True for the same link: same endpoints (bidirectional keys are stored lowest ID first) and direction.</summary>
        public bool Equals(PermittedStructureLinkKey other) =>
            SourceTypeID == other.SourceTypeID && TargetTypeID == other.TargetTypeID && Bidirectional == other.Bidirectional;

        /// <summary>Orders by source, then target, then directional before bidirectional; returns 0 exactly when <see cref="Equals(PermittedStructureLinkKey)"/> is true.</summary>
        public int CompareTo(PermittedStructureLinkKey other)
        {
            int sourceCompare = SourceTypeID.CompareTo(other.SourceTypeID);
            if (sourceCompare != 0)
                return sourceCompare;

            int targetCompare = TargetTypeID.CompareTo(other.TargetTypeID);
            if (targetCompare != 0)
                return targetCompare;

            return Bidirectional.CompareTo(other.Bidirectional);
        }

        bool IEquatable<IPermittedStructureLinkReadOnly>.Equals(IPermittedStructureLinkReadOnly other)
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

        public static bool operator ==(PermittedStructureLinkKey A, PermittedStructureLinkKey B) => A.Equals(B);

        public static bool operator !=(PermittedStructureLinkKey A, PermittedStructureLinkKey B) => !A.Equals(B);
    }

    public class PermittedStructureLinkObj : WCFObjBaseWithKey<PermittedStructureLinkKey,
        PermittedStructureLink>,
        IPermittedStructureLinkReadOnly
    {
        public override PermittedStructureLinkKey ID => new(this);

        protected override int GenerateHashCode() => ID.GetHashCode();

        public long SourceTypeID => Data.SourceTypeID;

        public long TargetTypeID => Data.TargetTypeID;

        public bool Bidirectional => Data.Bidirectional;

        ulong IPermittedStructureLinkReadOnly.SourceTypeID => (ulong)SourceTypeID;

        ulong IPermittedStructureLinkReadOnly.TargetTypeID => (ulong)TargetTypeID;

        bool IPermittedStructureLinkReadOnly.Directional => !Bidirectional;

        public override string ToString()
        {
            if (Bidirectional)
                return string.Format("{0} <-> {1}", SourceTypeID, TargetTypeID);
            else
                return string.Format("{0} -> {1}", SourceTypeID, TargetTypeID);
        }

        public bool Equals(IPermittedStructureLinkReadOnly other) => ((IPermittedStructureLinkReadOnly)ID).Equals(other);

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
