using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace gRPCAnnotationServiceCore.Client.Protos
{
    public partial class AnnotationSet
    {
        public static implicit operator AnnotationSet(global::gRPCAnnotationServiceCore.Client.AnnotationSet src)
        {
            var converted = new AnnotationSet();
            
            converted.Structures.AddRange(src.Structures.Select(x => (Protos.Structure)x));
            converted.Locations.AddRange(src.Locations.Select(x => (Protos.Location)x));
            
            return converted;
        }


        public static implicit operator global::gRPCAnnotationServiceCore.Client.AnnotationSet(AnnotationSet src)
        {
            var value = new global::gRPCAnnotationServiceCore.Client.AnnotationSet {
                Structures = src.Structures.Select(x => (global::gRPCAnnotationServiceCore.Client.Structure)x).ToArray(),
                Locations = src.Locations.Select(x => (global::gRPCAnnotationServiceCore.Client.Location)x).ToArray(),
            };
            return value;
        }

    }
}

