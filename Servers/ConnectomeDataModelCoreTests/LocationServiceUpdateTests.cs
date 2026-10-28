using System;
using gRPCAnnotationService;
using gRPCAnnotationService.Protos;
using Viking.AnnotationServiceTypes.gRPC.V1;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Xunit;
using EfLocation = Viking.DataModel.Annotation.Location;
using Viking.AnnotationServiceTypes.Interfaces;
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
            MosaicGeometry = TestShapes.Mosaic(mosaic),
            VolumeGeometry = TestShapes.Volume("POLYGON ((100 100, 110 100, 110 110, 100 110, 100 100))"),
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
        [InlineData("Terminal")]
        [InlineData("OffEdge")]
        public void ApplyUpdate_EachAuthoredFieldChanged_StampsCaller(string field)
        {
            var stored = Stored();
            var incoming = Proto();
            switch (field)
            {
                case "Section": incoming.Section = 13; break;
                case "Terminal": incoming.Terminal = !stored.Terminal; break;
                case "OffEdge": incoming.OffEdge = !stored.OffEdge; break;
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

        /// <summary>A self-intersecting ring: invalid, so NTS's topological equality reports it unequal even to itself.</summary>
        private const string Bowtie = "POLYGON ((0 0, 10 10, 10 0, 0 10, 0 0))";

        [Fact]
        public void ApplyUpdate_IdenticalInvalidMosaicShape_KeepsAuthor()
        {
            var stored = Stored(Bowtie);

            LocationService.ApplyUpdate(Proto(Bowtie), stored, "reviewer");

            Assert.Equal("original", stored.Username);
        }

        [Theory]
        [InlineData("POLYGON ((0 0, 12 12, 12 0, 0 12, 0 0))")]
        [InlineData("POLYGON ((10 10, 10 0, 0 10, 0 0, 10 10))")]
        [InlineData(Square)]
        public void ApplyUpdate_InvalidMosaicShapeEdited_StampsCaller(string editedShape)
        {
            // Only an identical copy of an invalid shape is unchanged: a moved bowtie, the same bowtie
            // listed from another vertex (topological equality cannot judge it), or a repair to a valid square.
            var stored = Stored(Bowtie);

            LocationService.ApplyUpdate(Proto(editedShape), stored, "reviewer");

            Assert.Equal("reviewer", stored.Username);
        }

        [Fact]
        public void ApplyUpdate_StoredShapeNtsCannotCompare_StampsCaller()
        {
            // Two triangles meeting at a vertex in one ring, set on the row directly because the proto shapes
            // refuse the repeated vertex: NTS's relate throws "side location conflict" against the
            // square, whose envelope is the same. A shape that cannot be compared counts as changed.
            var stored = Stored();
            stored.MosaicShape = new NetTopologySuite.IO.WKTReader().Read("POLYGON ((0 0, 10 0, 10 10, 0 0, 0 10, 10 10, 0 0))");

            LocationService.ApplyUpdate(Proto(), stored, "reviewer");

            Assert.Equal("reviewer", stored.Username);
        }

        [Theory]
        [InlineData("Section")]
        [InlineData("Terminal")]
        [InlineData("OffEdge")]
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
                case "Terminal": incoming.Terminal = !stored.Terminal; break;
                case "OffEdge": incoming.OffEdge = !stored.OffEdge; break;
                case "Type":
                    // Same points, now a curve polygon: only the type changes.
                    incoming.MosaicGeometry = TestShapes.Mosaic("POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))", LocationType.CURVEPOLYGON);
                    break;
                case "Tags": incoming.Attributes = "<Attributes/>"; break;
                case "Shape": incoming.MosaicGeometry = TestShapes.Mosaic("POLYGON ((0 0, 12 0, 12 10, 0 10, 0 0))"); break;
            }

            LocationService.ApplyUpdate(incoming, stored, "reviewer");

            Assert.Equal("reviewer", stored.Username);
        }

        [Fact]
        public void ApplyUpdate_LineWidthChanged_StampsCaller()
        {
            const string line = "LINESTRING (0 0, 5 5, 10 0)";
            var incoming = Proto();
            incoming.MosaicGeometry = TestShapes.Mosaic(line, LocationType.POLYLINE, width: 4);
            incoming.VolumeGeometry = TestShapes.Volume(line, LocationType.POLYLINE);
            var stored = incoming.ToLocation();
            stored.Username = "original";

            incoming.MosaicGeometry.Line.Width = 6.5;
            LocationService.ApplyUpdate(incoming, stored, "reviewer");

            Assert.Equal(6.5, stored.Width);
            Assert.Equal("reviewer", stored.Username);
        }

        [Fact]
        public void ApplyUpdate_UnchangedCircle_KeepsAuthor()
        {
            var incoming = Proto();
            incoming.MosaicGeometry = AnnotationShapes.CircleShape(10, 20, 8);
            incoming.VolumeGeometry = AnnotationShapes.CircleShape(11, 22, 10);
            var stored = incoming.ToLocation();
            stored.Username = "original";
            // As SQL derives the stored rim point from the curve's area: equal within rounding.
            stored.MosaicShape = new NetTopologySuite.Geometries.Point(18 + 1e-9, 20);

            LocationService.ApplyUpdate(incoming, stored, "reviewer");

            Assert.Equal("original", stored.Username);
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
        public void ApplyUpdate_IncomingWithoutShapeOrPosition_IsRejected()
        {
            var stored = Stored();
            var incoming = Proto();
            incoming.MosaicGeometry = null;

            Assert.Throws<ArgumentException>(() => LocationService.ApplyUpdate(incoming, stored, "reviewer"));
            Assert.Equal("original", stored.Username);
        }
    }
}
