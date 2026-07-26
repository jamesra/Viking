using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace gRPCAnnotationServiceCore.Protos
{
    public partial class LocationHistory
    {
        public static implicit operator LocationHistory(global::AnnotationService.Types.LocationHistory src)
        {
            var value = new LocationHistory {
                ChangedColumnMask = src.ChangedColumnMask,
            };
            return value;
        }


        public static implicit operator global::AnnotationService.Types.LocationHistory(LocationHistory src)
        {
            var value = new global::AnnotationService.Types.LocationHistory {
                ChangedColumnMask = src.ChangedColumnMask,
            };
            return value;
        }

    }
}

