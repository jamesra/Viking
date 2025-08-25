using Annotation;
using ProtoBuf;
using System;
using System.Runtime.Serialization;

namespace AnnotationService.Types
{

    [ProtoContract]
    [DataContract]
    public class CreateStructureRetval
    {
        private Structure _structure;
        private Location _location;

        [ProtoMember(1)]
        [DataMember]
        public Structure structure { get => _structure;
            set => _structure = value;
        }

        [ProtoMember(2)]
        [DataMember]
        public Location location { get => _location;
            set => _location = value;
        }

        public CreateStructureRetval(Structure s, Location l)
        {
            _structure = s;
            _location = l;
        }

        public CreateStructureRetval()
        {
        }
    }

    [ProtoContract]
    [DataContract]
    public class Structure : DataObjectWithParentOfLong
    {
        private Int64 _Type;
        private string _Notes;
        private bool _Verified;
        private double _Confidence;
        private StructureLink[] _Links;
        private Int64[] _ChildIDs;
        private string _Label;
        private string _Username;
        private string _Xml;

        [DataMember]
        [ProtoMember(10)]
        public Int64 TypeID
        {
            get => _Type;
            set => _Type = value;
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
        public bool Verified
        {
            get => _Verified;
            set => _Verified = value;
        }

        /*
        [DataMember]
        public string[] Tags
        {
            get { return _Tags; }
            set { _Tags = value; }
        }
        */

        [DataMember]
        [ProtoMember(13)]
        public string AttributesXml
        {
            get => _Xml;
            set => _Xml = value;
        }

        [DataMember]
        [ProtoMember(14)]
        public double Confidence
        {
            get => _Confidence;
            set => _Confidence = value;
        }

        [DataMember]
        [ProtoMember(15)]
        public StructureLink[] Links
        {
            get => _Links;
            set => _Links = value;
        }

        [DataMember]
        [ProtoMember(16)]
        public Int64[] ChildIDs
        {
            get => _ChildIDs;
            set => _ChildIDs = value;
        }

        [DataMember]
        [ProtoMember(17)]
        public string Label
        {
            get => _Label;
            set => _Label = value;
        }

        [DataMember]
        [ProtoMember(18)]
        [Column("Username")]
        public string Username
        {
            get => _Username;
            set => _Username = value;
        }


        public Structure()
        {
        }

    }
}



