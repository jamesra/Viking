using ProtoBuf;
using System;
using System.Linq;
using System.Runtime.Serialization;
using Viking.AnnotationServiceTypes.Interfaces;

namespace AnnotationService.Types
{
    [DataContract]
    [ProtoContract]
    public class StructureType : DataObjectWithParentOfLong, Viking.AnnotationServiceTypes.Interfaces.IStructureType
    {
        private string _Name;
        private string _Notes;
        private string _MarkupType;
        private string[] _Tags = Array.Empty<string>();
        private string[] _StructureTags = Array.Empty<string>();
        private bool _Abstract;
        private uint _Color;
        private string _Code;
        private char _HotKey;
        private PermittedStructureLink[] _Links;

        [DataMember]
        [ProtoMember(1)]
        public string Name
        {
            get { return _Name; }
            set { _Name = value; }
        }

        [DataMember]
        [ProtoMember(2)]
        public string Notes
        {
            get { return _Notes; }
            set { _Notes = value; }
        }

        [DataMember]
        [ProtoMember(3)]
        public string MarkupType
        {
            get { return _MarkupType; }
            set { _MarkupType = value; }
        }

        [DataMember]
        [ProtoMember(4)]
        public string[] Tags
        {
            get { return _Tags; }
            set { _Tags = value; }
        }

        [DataMember]
        [ProtoMember(5)]
        public string[] StructureTags
        {
            get { return _StructureTags; }
            set { _StructureTags = value; }
        }

        [DataMember]
        [ProtoMember(6)]
        public bool Abstract
        {
            get { return _Abstract; }
            set { _Abstract = value; }
        }

        [DataMember]
        [ProtoMember(7)]
        public uint Color
        {
            get { return _Color; }
            set { _Color = value; }
        }

        [DataMember]
        [ProtoMember(8)]
        public string Code
        {
            get { return _Code; }
            set { _Code = value; }
        }

        [DataMember]
        [ProtoMember(9)]
        public char HotKey
        {
            get { return _HotKey; }
            set { _HotKey = value; }
        }

        [DataMember]
        [ProtoMember(10)]
        public PermittedStructureLink[] PermittedLinks
        {
            get { return _Links; }
            set { _Links = value; }
        }

        IPermittedStructureLink[] IStructureType.PermittedLinks
        {
            get { return PermittedLinks; }
            set
            {
                _Links = value.Select(pl => new PermittedStructureLink()
                {
                    SourceTypeID = (long)pl.SourceTypeID,
                    TargetTypeID = (long)pl.TargetTypeID,
                    Bidirectional = !pl.Directional
                }).ToArray();
            }
        }

        long? IDataObjectWithParent<long>.ParentID
        {
            get { return ParentID.HasValue ? (long)this.ParentID.Value : new long?(); }
            set { ParentID = value.HasValue ? (long)value.Value : new long?(); }
        }

        long IDataObjectWithKey<long>.ID { get => this.ID; set => ID = value; }
        string IStructureType.Attributes { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public StructureType()
        {
            //       DBAction = DBACTION.INSERT; 
        }

        bool IEquatable<IStructureType>.Equals(IStructureType other)
        {
            if (ReferenceEquals(this, other))
                return true;

            return (long)this.ID == (long)other.ID;
        }
    }
}
