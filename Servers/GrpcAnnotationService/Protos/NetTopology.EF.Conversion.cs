using System;
using System.Linq;

namespace gRPCAnnotationService.Protos
{
    /// <summary>
    /// Proto Geometry ↔ NTS. Inbound accepts WKT or WKB; outbound always writes WKB.
    /// Circles must not go through this path — use LocationEFExtensions / PersistCircleShapesAsync.
    /// </summary>
    public static class NetTopologyGeometryExtensions
    {
        public static NetTopologySuite.Geometries.Geometry ToNetTopologyGeometry(this global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry src)
        {
            switch (src.EncodingCase)
            { 
                case global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry.EncodingOneofCase.Text:
                {
                    var reader = new NetTopologySuite.IO.WKTReader();
                    return reader.Read(src.Text);
                }
                case global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry.EncodingOneofCase.Binary:
                {
                    var reader = new NetTopologySuite.IO.WKBReader();
                    return reader.Read(src.Binary.ToArray());
                }
                default:
                    throw new ArgumentException($"Unexpected geometry message encoding: {src.EncodingCase}");
            }
        }


        public static global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry ToProtobufMessage(this NetTopologySuite.Geometries.Geometry src)
        {
            var value = new global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry
            { 
                Binary = Google.Protobuf.ByteString.CopyFrom(src.ToBinary())
            };
            return value;
        }
    }
}
