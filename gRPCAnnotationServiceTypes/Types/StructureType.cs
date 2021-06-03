using ProtoBuf;
using System;

namespace Viking.gRPC.AnnotationTypes
{
    /* Recoded [DataContract] */
    [ProtoContract]
    public class StructureType : DataObjectWithParentOfLong
    {
        private string _Name;
        private string _Notes;
        private string _MarkupType;
        private string[] _Tags = new String[0];
        private string[] _StructureTags = new String[0];
        private bool _Abstract;
        private int _Color;
        private string _Code;
        private char _HotKey;
        private PermittedStructureLink[] _Links;

        /* Recoded [DataMember] */
        [ProtoMember(1)]
        public string Name
        {
            get { return _Name; }
            set { _Name = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(2)]
        public string Notes
        {
            get { return _Notes; }
            set { _Notes = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(3)]
        public string MarkupType
        {
            get { return _MarkupType; }
            set { _MarkupType = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(4)]
        public string[] Tags
        {
            get { return _Tags; }
            set { _Tags = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(5)]
        public string[] StructureTags
        {
            get { return _StructureTags; }
            set { _StructureTags = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(6)]
        public bool Abstract
        {
            get { return _Abstract; }
            set { _Abstract = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(7)]
        public int Color
        {
            get { return _Color; }
            set { _Color = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(8)]
        public string Code
        {
            get { return _Code; }
            set { _Code = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(9)]
        public char HotKey
        {
            get { return _HotKey; }
            set { _HotKey = value; }
        }

        /* Recoded [DataMember] */
        [ProtoMember(10)]
        public PermittedStructureLink[] PermittedLinks
        {
            get { return _Links; }
            set { _Links = value; }
        }

        public StructureType()
        {
            //       DBAction = DBACTION.INSERT; 
        }
    }
}

