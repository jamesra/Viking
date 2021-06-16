using ProtoBuf;
using System;

namespace Viking.AnnotationServiceTypes.gRPC
{

    [ProtoContract]
    /* Recoded [DataContract] */
    public class LocationHistory : Location
    {
        private Int64 _ChangedColumnMask = 0;

        [ProtoMember(1)]
        /* Recoded [DataMember] */
        //[Column("ChangedColumnMask")]
        public Int64 ChangedColumnMask
        {
            get
            {
                return _ChangedColumnMask;
            }
            set
            {
                _ChangedColumnMask = value;
            }
        }
    }
}

