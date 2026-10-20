using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;
using System;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes;

namespace WebAnnotationModel.Objects
{

    /// <summary>
    /// Allowed link between two structure types. Same Bidirectional vs server Directional inversion as StructureLinkObj.
    /// </summary>
    public class PermittedStructureLinkObj : AnnotationModelObjBaseWithKey<PermittedStructureLinkKey,
        IPermittedStructureLink>,
        IPermittedStructureLinkKey, IComparable<IPermittedStructureLinkKey>,
        IEquatable<PermittedStructureLinkObj>, IEquatable<PermittedStructureLinkKey>,
        IEquatable<IPermittedStructureLink>, IEquatable<IPermittedStructureLinkKey>
    {
        public override PermittedStructureLinkKey ID => new PermittedStructureLinkKey(SourceTypeID, TargetTypeID, Bidirectional);

        /// <summary>
        /// Hashes through <see cref="ID"/>, so objects whose keys compare equal (including a bidirectional link stored
        /// the other way round) hash alike.
        /// </summary>
        protected override int GenerateHashCode() => ID.GetHashCode();

        public long SourceTypeID { get; private set; }

        public long TargetTypeID { get; private set; }

        public bool Bidirectional { get; private set; }

        ulong IPermittedStructureLinkKey.SourceTypeID => (ulong)SourceTypeID;

        ulong IPermittedStructureLinkKey.TargetTypeID => (ulong)TargetTypeID;

        bool IPermittedStructureLinkKey.Directional => !Bidirectional;

        public override string ToString()
        {
            if (Bidirectional)
                return $"{SourceTypeID} <-> {TargetTypeID}";
            else
                return $"{SourceTypeID}  -> {TargetTypeID}";
        }

        public override bool Equals(object other)
        {
            if (other is PermittedStructureLinkObj l)
                return Equals(l);

            if (other is PermittedStructureLinkKey psk)
                return Equals(psk);

            if (other is IPermittedStructureLink psl)
                return Equals(psl);

            if (other is IPermittedStructureLinkKey o)
                return ID.Equals(o);
              
            return false;
        }

        public bool Equals(PermittedStructureLinkObj other)
        {
            return other.TargetTypeID == this.TargetTypeID &&
                   other.SourceTypeID == this.SourceTypeID &&
                   other.Bidirectional == this.Bidirectional;
        }

        public bool Equals(PermittedStructureLinkKey other)
        { 
            return other.TargetTypeID == this.TargetTypeID &&
                   other.SourceTypeID == this.SourceTypeID &&
                   other.Bidirectional == this.Bidirectional;
        }

        public bool Equals(IPermittedStructureLink other)
        { 
            if (other is null)
                return false;

            return (long)other.TargetTypeID == this.TargetTypeID &&
                   (long)other.SourceTypeID == this.SourceTypeID &&
                   other.Directional == !this.Bidirectional;
        }

        public bool Equals(IPermittedStructureLinkKey other)
        { 
            if (other is null)
                return false;

            return (long)other.TargetTypeID == this.TargetTypeID &&
                   (long)other.SourceTypeID == this.SourceTypeID &&
                   other.Directional == !this.Bidirectional;
        }

        public PermittedStructureLinkObj(long SourceTypeID, long TargetTypeID,
                                bool Bidirectional) : base()
        { 
            if (Bidirectional)
            {
                this.SourceTypeID = Math.Min(SourceTypeID, TargetTypeID);
                this.TargetTypeID = Math.Max(SourceTypeID, TargetTypeID);
            }
            else
            {
                this.SourceTypeID = SourceTypeID;
                this.TargetTypeID = TargetTypeID;
            }
            this.Bidirectional = Bidirectional;
            
            this.DBAction = DBACTION.INSERT;
        }

        public PermittedStructureLinkObj(IPermittedStructureLink data) :
            this((long)data.SourceTypeID, (long)data.TargetTypeID, !data.Directional)
        {
        }

        public PermittedStructureLinkObj()
        {
        }

        internal static Task<PermittedStructureLinkObj> CreateFromServer(IPermittedStructureLink newdata)
        {
            return Task.FromResult(new PermittedStructureLinkObj
            {
                SourceTypeID = (long) newdata.SourceTypeID,
                TargetTypeID = (long)newdata.TargetTypeID,
                Bidirectional = !newdata.Directional
            });
        }

        internal override Task Update(IPermittedStructureLink newdata)
        {
            SourceTypeID = (long)newdata.SourceTypeID;
            TargetTypeID = (long)newdata.TargetTypeID;
            Bidirectional = !newdata.Directional;
            return Task.CompletedTask;
        }

        public int CompareTo(IPermittedStructureLinkKey other)
        {
            return PermittedStructureLinkKey.Compare(this, other);
        }
    }
}
