using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationService.Types;
using System;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{
    public readonly struct StructureLinkKey(long SourceID, long TargetID, bool Bidirectional) : IEquatable<StructureLinkKey>, IComparable<StructureLinkKey>, IStructureLink
    {
        readonly long _SourceID = SourceID;
        readonly long _TargetID = TargetID;
        readonly bool _Bidirectional = Bidirectional;

        //public long SourceID { get { return link != null ? link.SourceID : _SourceID;  } }
        //public long TargetID { get { return link != null ? link.TargetID : _TargetID; } }

        public long SourceID => _SourceID;
        public long TargetID => _TargetID;

        public bool Bidirectional => _Bidirectional;

        ulong IStructureLink.SourceID => (ulong)SourceID;

        ulong IStructureLink.TargetID => (ulong)TargetID;

        bool IStructureLink.Directional => this.Bidirectional == false;

        public StructureLinkKey(StructureLink obj) : this(obj.SourceID, obj.TargetID, obj.Bidirectional)
        {
        }

        public StructureLinkKey(StructureLinkObj obj) : this(obj.SourceID, obj.TargetID, obj.Bidirectional)
        {
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            if (obj is not IStructureLink other)
                return false;

            return ((IStructureLink)this).Equals(other);
        }

        /// <summary>
        /// The first endpoint in the key's canonical order: the lower ID of a bidirectional link, else <see cref="SourceID"/>.
        /// A bidirectional link (A,B) is the same link as (B,A), so equality, hashing and ordering all use this canonical pair.
        /// </summary>
        private long First => Bidirectional ? Math.Min(SourceID, TargetID) : SourceID;

        /// <summary>The second endpoint in canonical order: the higher ID of a bidirectional link, else <see cref="TargetID"/>.</summary>
        private long Second => Bidirectional ? Math.Max(SourceID, TargetID) : TargetID;

        /// <summary>Hashes the canonical pair, so keys that compare equal (including reversed bidirectional keys) hash alike.</summary>
        public override int GetHashCode() => unchecked((First.GetHashCode() * 397) ^ Second.GetHashCode() ^ (Bidirectional ? 1 : 0));

        /// <summary>True for the same link: same endpoints and direction, or swapped endpoints when both keys are bidirectional.</summary>
        public bool Equals(StructureLinkKey other) =>
            First == other.First && Second == other.Second && Bidirectional == other.Bidirectional;

        /// <summary>Orders by the canonical pair, then directional before bidirectional; returns 0 exactly when <see cref="Equals(StructureLinkKey)"/> is true.</summary>
        public int CompareTo(StructureLinkKey other)
        {
            int firstCompare = First.CompareTo(other.First);
            if (firstCompare != 0)
                return firstCompare;

            int secondCompare = Second.CompareTo(other.Second);
            if (secondCompare != 0)
                return secondCompare;

            return Bidirectional.CompareTo(other.Bidirectional);
        }

        bool IEquatable<IStructureLink>.Equals(IStructureLink other)
        {
            if (other is null)
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

    public class StructureLinkObj : WCFObjBaseWithKey<StructureLinkKey, StructureLink>
    {
        public override StructureLinkKey ID => new(this);

        protected override int GenerateHashCode() => ID.GetHashCode();

        public long SourceID => Data.SourceID;

        public long TargetID => Data.TargetID;

        public bool Bidirectional => Data.Bidirectional;

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
            this.Data = new StructureLink
            {
                SourceID = sourceID,
                TargetID = targetID,
                Bidirectional = Bidirectional
            };
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
