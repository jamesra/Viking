using System;
using gRPCAnnotationService;
using gRPCAnnotationService.Protos;
using Viking.AnnotationServiceTypes.gRPC.V1;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Xunit;
using EfLocation = Viking.DataModel.Annotation.Location;
using ProtoGeometry = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry;
using ProtoLocation = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins who is recorded as a location's author on update: a pass that only rewrites VolumeShape
    /// ("Update Volume Positions" in Viking) keeps the stored username, any other edit takes the caller.
    /// Matches the WCF service. LastModified always advances so incremental sync still delivers the row.
    /// </summary>
    public class LocationUpdateUsernameTests
    {
        private static readonly DateTime OldStamp = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static ProtoLocation Proto() => new()
        {
            Id = 7,
            ParentId = 3,
            Section = 12,
            TypeCode = AnnotationType.Polygon,
            MosaicShape = new ProtoGeometry { Text = "POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))" },
            VolumeShape = new ProtoGeometry { Text = "POLYGON ((100 100, 110 100, 110 110, 100 110, 100 100))" },
        };

        /// <summary>A stored row identical to <see cref="Proto"/>, authored by "original".</summary>
        private static EfLocation Stored()
        {
            var row = Proto().ToLocation();
            row.Username = "original";
            row.LastModified = OldStamp;
            return row;
        }

        [Fact]
        public void ApplyUpdate_OnlyVolumeShapeChanged_KeepsUsername()
        {
            var stored = Stored();
            var incoming = Proto();
            incoming.VolumeShape = new ProtoGeometry { Text = "POLYGON ((101 100, 111 100, 111 110, 101 110, 101 100))" };

            LocationService.ApplyUpdate(incoming, stored, "reviewer");

            Assert.Equal("original", stored.Username);
            Assert.True(stored.LastModified > OldStamp);
            Assert.Equal(101, stored.VolumeShape.EnvelopeInternal.MinX);
        }

        [Fact]
        public void ApplyUpdate_NothingChanged_KeepsUsername()
        {
            var stored = Stored();

            LocationService.ApplyUpdate(Proto(), stored, "reviewer");

            Assert.Equal("original", stored.Username);
        }

        [Fact]
        public void ApplyUpdate_MosaicShapeChanged_StampsCaller()
        {
            var stored = Stored();
            var incoming = Proto();
            incoming.MosaicShape = new ProtoGeometry { Text = "POLYGON ((0 0, 12 0, 12 10, 0 10, 0 0))" };

            LocationService.ApplyUpdate(incoming, stored, "reviewer");

            Assert.Equal("reviewer", stored.Username);
        }

        [Fact]
        public void ApplyUpdate_TypeChanged_StampsCaller()
        {
            var stored = Stored();
            var incoming = Proto();
            incoming.TypeCode = AnnotationType.Closedcurve;

            LocationService.ApplyUpdate(incoming, stored, "reviewer");

            Assert.Equal("reviewer", stored.Username);
        }

        [Fact]
        public void ApplyUpdate_TagsChanged_StampsCaller_ButNullVersusEmptyTagsIsNoChange()
        {
            var unchanged = Stored();
            unchanged.Tags = null;
            var emptyTags = Proto();
            emptyTags.Attributes = "";
            LocationService.ApplyUpdate(emptyTags, unchanged, "reviewer");
            Assert.Equal("original", unchanged.Username);

            var changed = Stored();
            var tagged = Proto();
            tagged.Attributes = "<Attributes><Attribute Name=\"x\"/></Attributes>";
            LocationService.ApplyUpdate(tagged, changed, "reviewer");
            Assert.Equal("reviewer", changed.Username);
        }

        [Fact]
        public void ApplyUpdate_StoredRowHasNoUsername_StampsCaller()
        {
            var stored = Stored();
            stored.Username = null;

            LocationService.ApplyUpdate(Proto(), stored, "reviewer");

            Assert.Equal("reviewer", stored.Username);
        }
    }
}
