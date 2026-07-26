using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace gRPCAnnotationServiceCore.Client.Protos
{
    public partial class LocationLink
    {
        public static implicit operator LocationLink(global::gRPCAnnotationServiceCore.Client.LocationLink src)
        {
            var value = new LocationLink {
                SourceId = src.SourceID,
                TargetId = src.TargetID,
            };
            return value;
        }


        public static implicit operator global::gRPCAnnotationServiceCore.Client.LocationLink(LocationLink src)
        {
            var value = new global::gRPCAnnotationServiceCore.Client.LocationLink {
                SourceID = src.SourceId,
                TargetID = src.TargetId,
            };
            return value;
        }

    }
}

