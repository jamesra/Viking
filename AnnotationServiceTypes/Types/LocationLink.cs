using ProtoBuf;
using System;
using System.Runtime.Serialization;

namespace AnnotationService.Types
{

    [ProtoContract]
    [DataContract]
    public class LocationLink : DataObject
    {
        Int64 _SourceID;
        Int64 _TargetID;

        [ProtoMember(1)]
        [DataMember]
        public Int64 SourceID
        {
            get { return _SourceID; }
            set { _SourceID = value; }
        }

        [ProtoMember(2)]
        [DataMember]
        public Int64 TargetID
        {
            get { return _TargetID; }
            set { _TargetID = value; }
        }

        /*
        [DataMember]
        public string Username
        {
            get { return _Username; }
            set { _Username = value; }
        }
        */

    }

}
