using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;
using ProtoBuf;

namespace Annotation
{
    
    [ProtoContract]
    [DataContract]
    public class CreateStructureRetval
    {
        private Structure _structure;
        private Location _location;

        [ProtoMember(1)]
        [DataMember]
        public Structure structure { get { return _structure; } set { _structure = value; } }

        [ProtoMember(2)]
        [DataMember]
        public Location location { get { return _location; } set { _location = value; }  }

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
    public class Structure : DataObjectWithParent<long>
    {
        protected long _Type;
        protected string _Notes;
        protected bool _Verified; 
        protected double _Confidence;
        protected StructureLink[] _Links;
        protected long[] _ChildIDs;
        protected string _Label;
        protected string _Username;
        protected string _Xml;
        
        [DataMember]
        [ProtoMember(1)]
        public long TypeID
        {
            get { return _Type; }
            set { _Type = value; }
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
        public bool Verified
        {
            get { return _Verified; }
            set { _Verified = value; }
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
        [ProtoMember(4)]
        public string AttributesXml
        {
            get { return _Xml; }
            set { _Xml = value; }
        }

        [DataMember]
        [ProtoMember(5)]
        public double Confidence
        {
            get { return _Confidence; }
            set { _Confidence = value; }
        }

        [DataMember]
        [ProtoMember(6)]
        public StructureLink[] Links
        {
            get { return _Links; }
            set { _Links = value; }
        }

        [DataMember]
        [ProtoMember(7)]
        public long[] ChildIDs
        {
            get { return _ChildIDs; }
            set { _ChildIDs = value; }
        }

        [DataMember]
        [ProtoMember(8)]
        public string Label
        {
            get { return _Label; }
            set { _Label = value; }
        }

        [DataMember]
        [ProtoMember(9)]
        [Column("Username")]
        public string Username
        {
            get { return _Username; }
            set { _Username = value; }
        }


        public Structure()
        {
        }

    }
}



