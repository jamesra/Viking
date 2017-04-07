using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;
using ProtoBuf;

namespace AnnotationService.Types
{
    
    [ProtoContract]
    [DataContract]
    public class StructureLink : DataObject
    {
        public override string ToString()
        {
            string result = _SourceID.ToString();
            result += _Bidirectional ? " <-> " : " -> ";
            result += _TargetID.ToString();
            return result;
        }

        long _SourceID;
        long _TargetID;
        bool _Bidirectional;
        string _Tags;
        string _Username;

        [ProtoMember(1)]
        [DataMember]
        public long SourceID
        {
         get{return _SourceID;}
         set{_SourceID = value;}
        }

        [ProtoMember(2)]
        [DataMember]
        public long TargetID
        {
            get { return _TargetID; }
            set { _TargetID = value; }
        }

        [ProtoMember(3)]
        [DataMember]
        public bool Bidirectional
        {
            get { return _Bidirectional; }
            set { _Bidirectional = value; }
        }

        [ProtoMember(4)]
        [DataMember]
        public string Tags
        {
            get { return _Tags; }
            set { _Tags = value; }
        }

        [ProtoMember(5)]
        [DataMember]
        public string Username
        {
            get { return _Username; }
            set { _Username = value; }
        }

        public StructureLink()
        {
        } 
    }
}
