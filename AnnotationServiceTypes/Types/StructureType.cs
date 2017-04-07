using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;
using ProtoBuf;

namespace AnnotationService.Types
{
    [DataContract]
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
        protected PermittedStructureLink[] _Links;

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
        public int Color
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

        public StructureType()
        {
     //       DBAction = DBACTION.INSERT; 
        } 
    }
}
