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
