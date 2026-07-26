using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace AnnotationService.WcfProxy.Protos
{
    public partial class AnnotationSet
    {
        public static implicit operator AnnotationSet(global::AnnotationService.WcfProxy.AnnotationSet src)
        {
            var converted = new AnnotationSet();
            
            converted.Structures.AddRange(src.Structures.Select(x => (Protos.Structure)x));
            converted.Locations.AddRange(src.Locations.Select(x => (Protos.Location)x));
            
            return converted;
        }


        public static implicit operator global::AnnotationService.WcfProxy.AnnotationSet(AnnotationSet src)
        {
            var value = new global::AnnotationService.WcfProxy.AnnotationSet {
                Structures = src.Structures.Select(x => (global::AnnotationService.WcfProxy.Structure)x).ToArray(),
                Locations = src.Locations.Select(x => (global::AnnotationService.WcfProxy.Location)x).ToArray(),
            };
            return value;
        }

    }
}

