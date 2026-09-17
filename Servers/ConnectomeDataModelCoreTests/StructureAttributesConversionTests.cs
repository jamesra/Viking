using gRPCAnnotationService.Protos;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Xunit;
using EfStructure = Viking.DataModel.Annotation.Structure;
using ProtoStructure = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Structure;

namespace ConnectomeDataModelCoreTests
{
    public class StructureAttributesConversionTests
    {
        [Fact]
        public void ToStructure_CopiesAttributesToTags()
        {
            var proto = new ProtoStructure { Attributes = "<Tag Name=\"Color\"/>" };

            var entity = proto.ToStructure();

            Assert.Equal(proto.Attributes, entity.Tags);
        }

        [Fact]
        public void ToStructure_TreatsBlankAttributesAsNullTags()
        {
            var proto = new ProtoStructure { Attributes = "  " };

            Assert.Null(proto.ToStructure().Tags);
        }

        [Fact]
        public void ToProtobufMessage_CopiesTagsToAttributes()
        {
            var entity = new EfStructure
            {
                Tags = "<Tag Name=\"Color\"/>",
                Username = string.Empty,
            };

            Assert.Equal(entity.Tags, entity.ToProtobufMessage().Attributes);
        }
    }
}
