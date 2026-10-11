using System;
using gRPCAnnotationService.Protos;
using Google.Protobuf.WellKnownTypes;
using Xunit;
using EfStructure = Viking.DataModel.Annotation.Structure;
using ProtoStructure = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Structure;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins the Structure proto-to-EF conversions used on every structure save and fetch: optional parent IDs,
    /// blank attribute handling, null-string normalization, and UTC timestamp stamping.
    /// </summary>
    public class StructureConversionTests
    {
        [Fact]
        public void ToStructure_WithoutParentId_YieldsNullParent()
        {
            Assert.Null(new ProtoStructure { Id = 1 }.ToStructure().ParentId);
        }

        [Fact]
        public void ToStructure_ExplicitZeroParentIsKept()
        {
            Assert.Equal(0L, new ProtoStructure { Id = 1, ParentId = 0 }.ToStructure().ParentId);
        }

        [Theory]
        [InlineData("", null)]
        [InlineData("   ", null)]
        [InlineData("<a/>", "<a/>")]
        public void ToStructure_BlankAttributesBecomeNullTags(string attributes, string expected)
        {
            Assert.Equal(expected, new ProtoStructure { Id = 1, Attributes = attributes }.ToStructure().Tags);
        }

        [Fact]
        public void ToStructure_MissingTimestampsBecomeDefault()
        {
            var entity = new ProtoStructure { Id = 1 }.ToStructure();

            Assert.Equal(default, entity.Created);
            Assert.Equal(default, entity.LastModified);
        }

        [Fact]
        public void ToProtobufMessage_RootStructureHasNoParent()
        {
            Assert.False(new EfStructure { Id = 1, ParentId = null }.ToProtobufMessage().HasParentId);
        }

        [Fact]
        public void ToProtobufMessage_NullStringsBecomeEmpty()
        {
            var proto = new EfStructure { Id = 1, Label = null, Notes = null, Username = null, Tags = null }.ToProtobufMessage();

            Assert.Equal(string.Empty, proto.Label);
            Assert.Equal(string.Empty, proto.Notes);
            Assert.Equal(string.Empty, proto.Username);
            Assert.Equal(string.Empty, proto.Attributes);
        }

        [Fact]
        public void ToProtobufMessage_UnspecifiedKindDatesAreSentAsUtc()
        {
            var when = new DateTime(2026, 10, 11, 1, 2, 3, DateTimeKind.Unspecified);

            var proto = new EfStructure { Id = 1, Created = when, LastModified = when }.ToProtobufMessage();

            Assert.Equal(DateTime.SpecifyKind(when, DateTimeKind.Utc), proto.Created.ToDateTime());
            Assert.Equal(DateTime.SpecifyKind(when, DateTimeKind.Utc), proto.LastModified.ToDateTime());
        }

        [Fact]
        public void RoundTrip_PreservesScalarFields()
        {
            var created = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var proto = new ProtoStructure
            {
                Id = 42, ParentId = 7, Confidence = 0.25, Label = "L", Notes = "N", TypeId = 3,
                Verified = true, Username = "u", Attributes = "<t/>",
                Created = Timestamp.FromDateTime(created), LastModified = Timestamp.FromDateTime(created),
            };

            var back = proto.ToStructure().ToProtobufMessage();

            Assert.Equal(proto, back);
        }
    }
}
