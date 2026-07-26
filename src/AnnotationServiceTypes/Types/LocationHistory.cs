using Annotation;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace AnnotationService.Types
{

    [ProtoContract]
    /* Recoded [DataContract] */
    public class LocationHistory : Location
    {
        private Int64 _ChangedColumnMask = 0;

        [ProtoMember(1)]
        /* Recoded [DataMember] */
        [Column("ChangedColumnMask")]
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

