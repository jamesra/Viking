using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace gRPCAnnotationServiceCore.Protos
{
    public partial class AnnotationPoint
    {
        public static implicit operator AnnotationPoint(global::AnnotationService.Types.AnnotationPoint src)
        {
            var value = new AnnotationPoint {
                X = src.X,
                Y = src.Y,
                Z = src.Z,
            };
            return value;
        }


        public static implicit operator global::AnnotationService.Types.AnnotationPoint(AnnotationPoint src)
        {
            var value = new global::AnnotationService.Types.AnnotationPoint {
                X = src.X,
                Y = src.Y,
                Z = src.Z,
            };
            return value;
        }

    }
}

