using ProtoBuf;
using System;
using System.Runtime.Serialization;


namespace Viking.gRPC.AnnotationTypes
{
    [DataContract]
    [ProtoContract]
    public enum DBACTION : Int32
    {
        [EnumMember]
        NONE = 0,
        [EnumMember]
        INSERT = 1,
        [EnumMember]
        UPDATE = 2,
        [EnumMember]
        DELETE = 3
    };
}

