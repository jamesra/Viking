using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace gRPCAnnotationServiceCore.Client.Protos
{
    public partial class BoundingRectangle
    {
        public static implicit operator BoundingRectangle(global::gRPCAnnotationServiceCore.Client.BoundingRectangle src)
        {
            var value = new BoundingRectangle {
                Xmin = src.XMin,
                Ymin = src.YMin,
                Xmax = src.XMax,
                Ymax = src.YMax,
            };
            return value;
        }


        public static implicit operator global::gRPCAnnotationServiceCore.Client.BoundingRectangle(BoundingRectangle src)
        {
            var value = new global::gRPCAnnotationServiceCore.Client.BoundingRectangle {
                XMin = src.Xmin,
                YMin = src.Ymin,
                XMax = src.Xmax,
                YMax = src.Ymax,
            };
            return value;
        }

    }
}

