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
        [ProtoEnum]
        NONE = 0,
        [EnumMember]
        [ProtoEnum]
        INSERT = 1,
        [EnumMember]
        [ProtoEnum]
        UPDATE = 2,
        [EnumMember]
        [ProtoEnum]
        DELETE = 3
    };
}

