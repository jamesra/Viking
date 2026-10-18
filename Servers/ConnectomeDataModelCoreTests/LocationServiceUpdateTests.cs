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
    /// Pins the per-field change detection in <c>LocationService.ApplyUpdate</c> that decides whether the
    /// caller becomes the author: each authored field, a missing ParentId, and topologically equal shapes.
    /// Complements
    /// <see cref="LocationUpdateUsernameTests"/>, which covers shape, type, tags and null username.
    /// Needs no database or identity stack.
    /// </summary>
    public class LocationServiceUpdateTests
    {
        private const string Square = "POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))";
        private const string SquareFromOtherVertex = "POLYGON ((10 10, 0 10, 0 0, 10 0, 10 10))";
        private const string SquareReversed = "POLYGON ((0 0, 0 10, 10 10, 10 0, 0 0))";

        private static ProtoLocation Proto(string mosaic = Square) => new()
        {
            Id = 7,
            ParentId = 3,
            Section = 12,
            TypeCode = AnnotationType.Polygon,
            MosaicShape = new ProtoGeometry { Text = mosaic },
            VolumeShape = new ProtoGeometry { Text = "POLYGON ((100 100, 110 100, 110 110, 100 110, 100 100))" },
        };

        private static EfLocation Stored(string mosaic = Square)
        {
            var row = Proto(mosaic).ToLocation();
            row.Username = "original";
            return row;
        }

        [Fact]
        public void ApplyUpdate_ParentIdZero_KeepsExistingParentAndAuthor()
        {
            var stored = Stored();
            var incoming = Proto();
            incoming.ParentId = 0;

            LocationService.ApplyUpdate(incoming, stored, "reviewer");

            Assert.Equal(3, stored.ParentId);
            Assert.Equal("original", stored.Username);
        }

        [Fact]
        public void ApplyUpdate_ParentIdChanged_WritesParentAndStampsCaller()
        {
            var stored = Stored();
            var incoming = Proto();
            incoming.ParentId = 4;

            LocationService.ApplyUpdate(incoming, stored, "reviewer");

            Assert.Equal(4, stored.ParentId);
            Assert.Equal("reviewer", stored.Username);
        }

        [Theory]
        [InlineData("Section")]
        [InlineData("Closed")]
        [InlineData("Terminal")]
        [InlineData("OffEdge")]
        [InlineData("Width")]
        public void ApplyUpdate_EachAuthoredFieldChanged_StampsCaller(string field)
        {
            var stored = Stored();
            var incoming = Proto();
            switch (field)
            {
                case "Section": incoming.Section = 13; break;
                case "Closed": incoming.Closed = !stored.Closed; break;
                case "Terminal": incoming.Terminal = !stored.Terminal; break;
                case "OffEdge": incoming.OffEdge = !stored.OffEdge; break;
                case "Width": incoming.Width = stored.Width.GetValueOrDefault() + 2.5; break;
            }

            LocationService.ApplyUpdate(incoming, stored, "reviewer");

            Assert.Equal("reviewer", stored.Username);
        }

        [Theory]
        [InlineData(SquareFromOtherVertex)]
        [InlineData(SquareReversed)]
        public void ApplyUpdate_TopologicallyEqualMosaicShape_KeepsAuthor(string equalShape)
        {
            var stored = Stored();

            LocationService.ApplyUpdate(Proto(equalShape), stored, "reviewer");

            Assert.Equal("original", stored.Username);
        }

        [Theory]
        [InlineData("Section")]
        [InlineData("Closed")]
        [InlineData("Terminal")]
        [InlineData("OffEdge")]
        [InlineData("Width")]
        [InlineData("Type")]
        [InlineData("Tags")]
        [InlineData("Shape")]
        public void ApplyUpdate_ParentAndOneMoreFieldChanged_StampsCaller(string field)
        {
            // Two simultaneous changes must not cancel each other out.
            var stored = Stored();
            var incoming = Proto();
            incoming.ParentId = 4;
            switch (field)
            {
                case "Section": incoming.Section = 13; break;
                case "Closed": incoming.Closed = !stored.Closed; break;
                case "Terminal": incoming.Terminal = !stored.Terminal; break;
                case "OffEdge": incoming.OffEdge = !stored.OffEdge; break;
                case "Width": incoming.Width = stored.Width.GetValueOrDefault() + 2.5; break;
                case "Type": incoming.TypeCode = AnnotationType.Closedcurve; break;
                case "Tags": incoming.Attributes = "<Attributes/>"; break;
                case "Shape": incoming.MosaicShape = new ProtoGeometry { Text = "POLYGON ((0 0, 12 0, 12 10, 0 10, 0 0))" }; break;
            }

            LocationService.ApplyUpdate(incoming, stored, "reviewer");

            Assert.Equal("reviewer", stored.Username);
        }

        [Fact]
        public void ApplyUpdate_NoUsernameAndParentChanged_StampsCaller()
        {
            var stored = Stored();
            stored.Username = null;
            var incoming = Proto();
            incoming.ParentId = 4;

            LocationService.ApplyUpdate(incoming, stored, "reviewer");

            Assert.Equal("reviewer", stored.Username);
        }

        [Theory]
        [InlineData("", null, false)]
        [InlineData(null, "", false)]
        [InlineData("<A/>", "<A/>", false)]
        [InlineData("<A/>", null, true)]
        [InlineData("<A/>", "", true)]
        [InlineData("", "<A/>", true)]
        [InlineData("<A/>", "<a/>", true)]
        public void ApplyUpdate_TagsComparison(string storedTags, string incomingTags, bool stamps)
        {
            var stored = Stored();
            stored.Tags = storedTags;
            var incoming = Proto();
            incoming.Attributes = incomingTags ?? "";

            LocationService.ApplyUpdate(incoming, stored, "reviewer");

            Assert.Equal(stamps ? "reviewer" : "original", stored.Username);
        }

        [Fact]
        public void ApplyUpdate_StoredShapeMissing_StampsCaller()
        {
            var stored = Stored();
            stored.MosaicShape = null;

            LocationService.ApplyUpdate(Proto(), stored, "reviewer");

            Assert.Equal("reviewer", stored.Username);
        }

        [Fact]
        public void ApplyUpdate_BothShapesMissing_KeepsAuthor()
        {
            var stored = Stored();
            stored.MosaicShape = null;
            var incoming = Proto();
            incoming.MosaicShape = null;

            LocationService.ApplyUpdate(incoming, stored, "reviewer");

            Assert.Equal("original", stored.Username);
        }
    }
}
