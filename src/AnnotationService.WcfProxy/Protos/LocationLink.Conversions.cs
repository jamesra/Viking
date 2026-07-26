using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace AnnotationService.WcfProxy.Protos
{
    public partial class LocationLink
    {
        public static implicit operator LocationLink(global::AnnotationService.WcfProxy.LocationLink src)
        {
            var value = new LocationLink {
                SourceId = src.SourceID,
                TargetId = src.TargetID,
            };
            return value;
        }


        public static implicit operator global::AnnotationService.WcfProxy.LocationLink(LocationLink src)
        {
            var value = new global::AnnotationService.WcfProxy.LocationLink {
                SourceID = src.SourceId,
                TargetID = src.TargetId,
            };
            return value;
        }

    }
}

