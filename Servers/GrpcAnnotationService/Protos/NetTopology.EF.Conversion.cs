using System;
using System.Linq;

namespace gRPCAnnotationService.Protos
{
    public static class NetTopologyGeometryExtensions
    {
        public static NetTopologySuite.Geometries.Geometry ToNetTopologyGeometry(this global::Viking.gRPC.AnnotationTypes.V1.Protos.Geometry src)
        {
            switch (src.EncodingCase)
            { 
                case global::Viking.gRPC.AnnotationTypes.V1.Protos.Geometry.EncodingOneofCase.Text:
                {
                    var reader = new NetTopologySuite.IO.WKBReader();
                    return reader.Read(src.Binary.ToArray());
                }
                case global::Viking.gRPC.AnnotationTypes.V1.Protos.Geometry.EncodingOneofCase.Binary:
                {
                    var reader = new NetTopologySuite.IO.WKTReader();
                    return reader.Read(src.Text);
                }
                default:
                    throw new ArgumentException($"Unexpected geometry message encoding: {src.EncodingCase}");
            }
        }


        public static global::Viking.gRPC.AnnotationTypes.V1.Protos.Geometry ToProtobufMessage(this NetTopologySuite.Geometries.Geometry src)
        {
            var value = new global::Viking.gRPC.AnnotationTypes.V1.Protos.Geometry
            { 
                Binary = Google.Protobuf.ByteString.CopyFrom(src.ToBinary())
            };
            return value;
        }
    }
}
