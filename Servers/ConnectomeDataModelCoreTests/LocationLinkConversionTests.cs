using gRPCAnnotationService.Protos;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Xunit;
using ProtoLocation = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location;

namespace ConnectomeDataModelCoreTests
{
    public class LocationLinkConversionTests
    {
        [Fact]
        public void ToLocation_DoesNotMaterializeLinks()
        {
            var proto = new ProtoLocation
            {
                Id = 0,
                MosaicPosition = new AnnotationPoint { X = 1, Y = 2 },
                VolumePosition = new AnnotationPoint { X = 1, Y = 2 },
            };
            proto.Links.Add(42);

            var entity = proto.ToLocation();

            Assert.Empty(entity.LocationLinkANavigations);
            Assert.Empty(entity.LocationLinkBNavigations);
        }
    }
}
