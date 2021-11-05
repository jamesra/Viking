using ProtoBuf;
using System;
using System.Runtime.Serialization;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.Interfaces;

namespace AnnotationService.Types
{
    [ProtoContract]
    [DataContract]
    public class PermittedStructureLink : DataObject, IPermittedStructureLinkKey, IPermittedStructureLink
    {
        public override string ToString()
        {
            string result = _SourceTypeID.ToString();
            result += _Bidirectional ? " <-> " : " -> ";
            result += _TargetTypeID.ToString();
            return result;
        }

        public bool Equals(IPermittedStructureLinkKey other)
        {
            return (ulong)SourceTypeID == other.SourceTypeID &&
                   (ulong)TargetTypeID == other.TargetTypeID &&
                   Bidirectional == !other.Directional;
        }

        public bool Equals(IPermittedStructureLink other)
        {
            if (other is null)
                return false;

            return (ulong)SourceTypeID == other.SourceTypeID &&
                   (ulong)TargetTypeID == other.TargetTypeID &&
                   Bidirectional == !other.Directional;
        }

        public int CompareTo(IPermittedStructureLinkKey other)
        {
            return PermittedStructureLinkKey.Compare(this, other);
        }

        Int64 _SourceTypeID;
        Int64 _TargetTypeID;
        bool _Bidirectional;

        [DataMember]
        [ProtoMember(1)]
        public Int64 SourceTypeID
        {
            get { return _SourceTypeID; }
            set { _SourceTypeID = value; }
        }

        [DataMember]
        [ProtoMember(2)]
        public Int64 TargetTypeID
        {
            get { return _TargetTypeID; }
            set { _TargetTypeID = value; }
        }

        [DataMember]
        [ProtoMember(3)]
        public bool Bidirectional
        {
            get { return _Bidirectional; }
            set { _Bidirectional = value; }
        }

        ulong IPermittedStructureLinkKey.SourceTypeID => (ulong)SourceTypeID;

        ulong IPermittedStructureLinkKey.TargetTypeID => (ulong)TargetTypeID;

        bool IPermittedStructureLinkKey.Directional => !Bidirectional;

        ulong IPermittedStructureLink.SourceTypeID { get => (ulong)SourceTypeID; set => this.SourceTypeID = (Int64)value; }
        ulong IPermittedStructureLink.TargetTypeID { get => (ulong)TargetTypeID; set => this.TargetTypeID = (Int64)value; }
        bool IPermittedStructureLink.Directional { get => !Bidirectional; set => Bidirectional = !value; }
        public IPermittedStructureLinkKey ID { get => new PermittedStructureLinkKey(SourceTypeID, TargetTypeID, Bidirectional); set => throw new NotImplementedException(); }
        PermittedStructureLinkKey IDataObjectWithKey<PermittedStructureLinkKey>.ID { get => new PermittedStructureLinkKey(SourceTypeID, TargetTypeID, Bidirectional); set => throw new NotImplementedException(); }

        public PermittedStructureLink()
        {
        }
    }
}
