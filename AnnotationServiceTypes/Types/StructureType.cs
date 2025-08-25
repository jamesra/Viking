using ProtoBuf;
using System;
using System.Runtime.Serialization;

namespace AnnotationService.Types
{
    [DataContract]
    [ProtoContract]
    public class StructureType : DataObjectWithParentOfLong
    {
        private string _Name;
        private string _Notes;
        private string _MarkupType;
        private string[] _Tags = Array.Empty<string>();
        private string[] _StructureTags = Array.Empty<string>();
        private bool _Abstract;
        private int _Color;
        private string _Code;
        private char _HotKey;
        private PermittedStructureLink[] _Links;

        [DataMember]
        [ProtoMember(10)]
        public string Name
        {
            get => _Name;
            set => _Name = value;
        }

        [DataMember]
        [ProtoMember(11)]
        public string Notes
        {
            get => _Notes;
            set => _Notes = value;
        }

        [DataMember]
        [ProtoMember(12)]
        public string MarkupType
        {
            get => _MarkupType;
            set => _MarkupType = value;
        }

        [DataMember]
        [ProtoMember(13)]
        public string[] Tags
        {
            get => _Tags;
            set => _Tags = value;
        }

        [DataMember]
        [ProtoMember(14)]
        public string[] StructureTags
        {
            get => _StructureTags;
            set => _StructureTags = value;
        }

        [DataMember]
        [ProtoMember(15)]
        public bool Abstract
        {
            get => _Abstract;
            set => _Abstract = value;
        }

        [DataMember]
        [ProtoMember(16)]
        public int Color
        {
            get => _Color;
            set => _Color = value;
        }

        [DataMember]
        [ProtoMember(17)]
        public string Code
        {
            get => _Code;
            set => _Code = value;
        }

        [DataMember]
        [ProtoMember(18)]
        public char HotKey
        {
            get => _HotKey;
            set => _HotKey = value;
        }

        [DataMember]
        [ProtoMember(19)]
        public PermittedStructureLink[] PermittedLinks
        {
            get => _Links;
            set => _Links = value;
        }

        public StructureType()
        {
            //       DBAction = DBACTION.INSERT; 
        }
    }
}
