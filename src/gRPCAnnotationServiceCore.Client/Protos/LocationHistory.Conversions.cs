using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace gRPCAnnotationServiceCore.Client.Protos
{
    public partial class LocationHistory
    {
        public static implicit operator LocationHistory(global::gRPCAnnotationServiceCore.Client.LocationHistory src)
        {
            var value = new LocationHistory {
                ChangedColumnMask = src.ChangedColumnMask,
            };
            return value;
        }


        public static implicit operator global::gRPCAnnotationServiceCore.Client.LocationHistory(LocationHistory src)
        {
            var value = new global::gRPCAnnotationServiceCore.Client.LocationHistory {
                ChangedColumnMask = src.ChangedColumnMask,
            };
            return value;
        }

    }
}

