using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace gRPCAnnotationServiceCore.Client.Protos
{
    public partial class AnnotationPoint
    {
        public static implicit operator AnnotationPoint(global::gRPCAnnotationServiceCore.Client.AnnotationPoint src)
        {
            var value = new AnnotationPoint {
                X = src.X,
                Y = src.Y,
                Z = src.Z,
            };
            return value;
        }


        public static implicit operator global::gRPCAnnotationServiceCore.Client.AnnotationPoint(AnnotationPoint src)
        {
            var value = new global::gRPCAnnotationServiceCore.Client.AnnotationPoint {
                X = src.X,
                Y = src.Y,
                Z = src.Z,
            };
            return value;
        }

    }
}