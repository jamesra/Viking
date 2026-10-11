using System;
using gRPCAnnotationService.Protos;
using Google.Protobuf.WellKnownTypes;
using Xunit;
using EfPermittedLink = Viking.DataModel.Annotation.PermittedStructureLink;
using EfStructureType = Viking.DataModel.Annotation.StructureType;
using ProtoPermittedLink = Viking.AnnotationServiceTypes.gRPC.V1.Protos.PermittedStructureLink;
using ProtoStructureType = Viking.AnnotationServiceTypes.gRPC.V1.Protos.StructureType;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins the StructureType and PermittedStructureLink proto-to-EF conversions that the gRPC annotation
    /// services use on every save and fetch: optional parent IDs, signed/unsigned colors, and blank attributes.
    /// </summary>
    public class StructureTypeConversionTests
    {
        [Fact]
        public void ToStructureType_WithoutParentId_YieldsNullParent()
        {
            var proto = new ProtoStructureType { Id = 5 };

            Assert.Null(proto.ToStructureType().ParentId);
        }

        [Fact]
        public void ToStructureType_ParentIdZeroIsKeptWhenExplicitlySet()
        {
            var proto = new ProtoStructureType { Id = 5, ParentId = 0 };

            Assert.Equal(0L, proto.ToStructureType().ParentId);
        }

        [Fact]
        public void ToProtobufMessage_NullParentClearsHasParentId()
        {
            var entity = new EfStructureType { Id = 5, ParentId = null, Username = "u" };

            var proto = entity.ToProtobufMessage();

            Assert.False(proto.HasParentId);
        }

        [Fact]
        public void ToProtobufMessage_ParentIdIsCarried()
        {
            var entity = new EfStructureType { Id = 5, ParentId = 9, Username = "u" };

            var proto = entity.ToProtobufMessage();

            Assert.True(proto.HasParentId);
            Assert.Equal(9L, proto.ParentId);
        }

        [Theory]
        [InlineData(0xFF336699u)]
        [InlineData(0u)]
        [InlineData(uint.MaxValue)]
        [InlineData(0x80000000u)]
        public void Color_RoundTripsThroughSignedStorage(uint wireColor)
        {
            var proto = new ProtoStructureType { Id = 1, Color = wireColor };

            var entity = proto.ToStructureType();

            Assert.Equal(unchecked((int)wireColor), entity.Color);
            Assert.Equal(wireColor, entity.ToProtobufMessage().Color);
        }

        [Fact]
        public void ToStructureType_BlankAttributesBecomeNullTags()
        {
            var proto = new ProtoStructureType { Attributes = " ", StructureAttributes = "" };

            var entity = proto.ToStructureType();

            Assert.Null(entity.Tags);
            Assert.Null(entity.StructureTags);
        }

        [Fact]
        public void ToProtobufMessage_NullStringsBecomeEmpty()
        {
            var entity = new EfStructureType { Id = 1, Username = null, Notes = null, Code = null, Name = null };

            var proto = entity.ToProtobufMessage();

            Assert.Equal(string.Empty, proto.Notes);
            Assert.Equal(string.Empty, proto.Username);
            Assert.Equal(string.Empty, proto.Code);
            Assert.Equal(string.Empty, proto.Name);
            Assert.Equal(string.Empty, proto.Attributes);
            Assert.Equal(string.Empty, proto.StructureAttributes);
        }

        [Fact]
        public void ToStructureType_MissingTimestampsDefault()
        {
            var entity = new ProtoStructureType { Id = 1 }.ToStructureType();

            Assert.Equal(default, entity.Created);
            Assert.Equal(default, entity.LastModified);
        }

        [Fact]
        public void ToStructureType_CopiesTimestamps()
        {
            var created = new DateTime(2024, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            var proto = new ProtoStructureType { Id = 1, Created = Timestamp.FromDateTime(created) };

            Assert.Equal(created, proto.ToStructureType().Created);
        }

        [Fact]
        public void Sync_WithoutLastModifiedStampsNowAndKeepsCreated()
        {
            var created = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var target = new EfStructureType { Id = 1, Created = created };
            var before = DateTime.UtcNow;

            new ProtoStructureType { Id = 1, Name = "n" }.Sync(ref target);

            Assert.Equal(created, target.Created);
            Assert.True(target.LastModified >= before);
            Assert.Equal("n", target.Name);
        }

        [Fact]
        public void Sync_ClearsParentWhenProtoHasNone()
        {
            var target = new EfStructureType { Id = 1, ParentId = 7 };

            new ProtoStructureType { Id = 1 }.Sync(ref target);

            Assert.Null(target.ParentId);
        }

        [Fact]
        public void PermittedStructureLink_RoundTrips()
        {
            var proto = new ProtoPermittedLink { SourceTypeId = 3, TargetTypeId = 4, Bidirectional = true };

            var entity = proto.ToPermittedStructureLink();
            var back = entity.ToProtobufMessage();

            Assert.Equal(3, entity.SourceTypeId);
            Assert.Equal(4, entity.TargetTypeId);
            Assert.True(entity.Bidirectional);
            Assert.Equal(proto, back);
        }
    }
}
