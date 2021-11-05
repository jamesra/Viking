using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;

namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
    public partial class StructureLink : IStructureLink, IStructureLinkKey, IEquatable<StructureLinkKey>, IDataObjectWithKey<StructureLinkKey>
    {
        public IStructureLinkKey ID
        { 
            get => new StructureLinkKey(SourceId, TargetId, Bidirectional);
            set => throw new NotImplementedException();
        }

        ulong IStructureLinkKey.SourceID { get => (ulong)SourceId; }
        ulong IStructureLinkKey.TargetID { get => (ulong)TargetId; }

        bool IStructureLinkKey.Directional { get => Bidirectional; }

        ulong IStructureLink.SourceID { get => (ulong)SourceId; set => SourceId = (long)value; }
        ulong IStructureLink.TargetID { get => (ulong)TargetId; set => TargetId = (long)value; }

        bool IStructureLink.Directional { get => Bidirectional; set => Bidirectional = !value; }
        StructureLinkKey IDataObjectWithKey<StructureLinkKey>.ID
        {
            get => new StructureLinkKey(SourceId, TargetId, Bidirectional);
            set => throw new NotImplementedException();
        }

        public int CompareTo(IStructureLinkKey other)
        {
            return StructureLinkKey.Compare(this, other);
        }

        public bool Equals(IStructureLink other)
        {
            if (ReferenceEquals(other, this))
                return true;

            if (other is null)
                return false;

            return (ulong)SourceId == other.SourceID &&
                   (ulong)TargetId == other.TargetID;
        }

        public bool Equals(IStructureLinkKey other)
        {
            if (ReferenceEquals(other, this))
                return true;

            if (other is null)
                return false;

            return (ulong)SourceId == other.SourceID &&
                   (ulong)TargetId == other.TargetID;
        }

        bool IEquatable<IStructureLink>.Equals(IStructureLink other)
        {
            if (ReferenceEquals(other, this))
                return true;

            if (other is null)
                return false;

            return (ulong)SourceId == other.SourceID &&
                   (ulong)TargetId == other.TargetID;
        }

        bool IEquatable<StructureLinkKey>.Equals(StructureLinkKey other)
        {    
            return SourceId == other.SourceID &&
                   TargetId == other.TargetID;
        }
    }
}
