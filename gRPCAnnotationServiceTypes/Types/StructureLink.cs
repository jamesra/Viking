using ProtoBuf;
using System;

namespace Viking.AnnotationServiceTypes.gRPC
{

    [ProtoContract]
    /* Recoded [DataContract] */
    public class StructureLink : DataObject
    {
        public override string ToString()
        {
            string result = _SourceID.ToString();
            result += _Bidirectional ? " <-> " : " -> ";
            result += _TargetID.ToString();
            return result;
        }

        Int64 _SourceID;
        Int64 _TargetID;
        bool _Bidirectional;
        string _Tags;
        string _Username;

        [ProtoMember(1)]
        /* Recoded [DataMember] */
        public Int64 SourceID
        {
            get { return _SourceID; }
            set { _SourceID = value; }
        }

        [ProtoMember(2)]
        /* Recoded [DataMember] */
        public Int64 TargetID
        {
            get { return _TargetID; }
            set { _TargetID = value; }
        }

        [ProtoMember(3)]
        /* Recoded [DataMember] */
        public bool Bidirectional
        {
            get { return _Bidirectional; }
            set { _Bidirectional = value; }
        }

        [ProtoMember(4)]
        /* Recoded [DataMember] */
        public string Tags
        {
            get { return _Tags; }
            set { _Tags = value; }
        }

        [ProtoMember(5)]
        /* Recoded [DataMember] */
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

