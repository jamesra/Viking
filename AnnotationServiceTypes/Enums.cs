using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;
using ProtoBuf;


namespace Annotation
{
    [DataContract]
    [ProtoContract]
    public enum DBACTION
    {
        [EnumMember]
        [ProtoEnum]
        NONE,
        [EnumMember]
        [ProtoEnum]
        INSERT,
        [EnumMember]
        [ProtoEnum]
        UPDATE,
        [EnumMember]
        [ProtoEnum]
        DELETE
    };
}
