using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace gRPCAnnotationServiceCore.Protos
{
    public partial class LocationLink
    {
        public static implicit operator LocationLink(global::AnnotationService.Types.LocationLink src)
        {
            var value = new LocationLink {
                SourceId = src.SourceID,
                TargetId = src.TargetID,
            };
            return value;
        }


        public static implicit operator global::AnnotationService.Types.LocationLink(LocationLink src)
        {
            var value = new global::AnnotationService.Types.LocationLink {
                SourceID = src.SourceId,
                TargetID = src.TargetId,
            };
            return value;
        }

    }
}

