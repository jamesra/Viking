using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization; 


namespace Annotation
{
    [DataContract]
    public enum DBACTION
    {
        [EnumMember]
        NONE,
        [EnumMember]
        INSERT,
        [EnumMember]
        UPDATE,
        [EnumMember]
        DELETE
    };
}
