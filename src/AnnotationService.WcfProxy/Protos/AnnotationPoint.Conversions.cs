using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace AnnotationService.WcfProxy.Protos
{
    public partial class AnnotationPoint
    {
        public static implicit operator AnnotationPoint(global::AnnotationService.WcfProxy.AnnotationPoint src)
        {
            var value = new AnnotationPoint {
                X = src.X,
                Y = src.Y,
                Z = src.Z,
            };
            return value;
        }


        public static implicit operator global::AnnotationService.WcfProxy.AnnotationPoint(AnnotationPoint src)
        {
            var value = new global::AnnotationService.WcfProxy.AnnotationPoint {
                X = src.X,
                Y = src.Y,
                Z = src.Z,
            };
            return value;
        }

    }
}

