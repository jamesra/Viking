using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace AnnotationService.WcfProxy.Protos
{
    public partial class LocationHistory
    {
        public static implicit operator LocationHistory(global::AnnotationService.WcfProxy.LocationHistory src)
        {
            var value = new LocationHistory {
                ChangedColumnMask = src.ChangedColumnMask,
            };
            return value;
        }


        public static implicit operator global::AnnotationService.WcfProxy.LocationHistory(LocationHistory src)
        {
            var value = new global::AnnotationService.WcfProxy.LocationHistory {
                ChangedColumnMask = src.ChangedColumnMask,
            };
            return value;
        }

    }
}

