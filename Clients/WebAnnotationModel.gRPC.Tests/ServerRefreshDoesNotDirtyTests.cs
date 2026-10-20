using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Geometry;
using NUnit.Framework;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.Objects;
using ProtoGeometry = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins that a server refresh of a cached location or structure type is not a local edit: the object keeps
    /// DBAction NONE, so the next Save does not send the server's values back under the current user's name (the WCF
    /// store's InternalUpdate also left refreshed objects NONE). An object with an unsaved local edit keeps the user's
    /// values until that edit is saved. Runs without a server: the updaters are called directly.
    /// </summary>
    [TestFixture]
    public class ServerRefreshDoesNotDirtyTests
    {
        private const string CachedSquare = "POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))";
        private const string ServerSquare = "POLYGON ((0 0, 20 0, 20 20, 0 20, 0 0))";

        private static LocationObj CachedLocation()
        {
            var obj = new LocationObj(5, 2)
            {
                TypeCode = LocationType.POLYGON,
                MosaicShape = CachedSquare.ParseWKT(),
                VolumeShape = CachedSquare.ParseWKT(),
                Terminal = false,
            };
            obj.DBAction = DBACTION.NONE;
            return obj;
        }

        private static Location ServerLocation() =>
            new Location
            {
                Id = 5,
                ParentId = 2,
                Section = 1,
                TypeCode = AnnotationType.Polygon,
                MosaicShape = new ProtoGeometry { Text = ServerSquare },
                VolumeShape = new ProtoGeometry { Text = ServerSquare },
                Terminal = true,
                Username = "other user",
                LastModified = Timestamp.FromDateTime(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
            };

        [Test]
        public async Task LocationRefresh_AppliesServerValues_AndStaysNone()
        {
            LocationObj obj = CachedLocation();

            bool changed = await new LocationServerToClientUpdater().Update(obj, ServerLocation());

            Assert.That(changed, Is.True);
            Assert.That(obj.Terminal, Is.True);
            Assert.That(obj.Username, Is.EqualTo("other user"));
            Assert.That(obj.MosaicShape.BoundingBox.Width, Is.EqualTo(20).Within(1e-9));
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.NONE), "a server refresh is not queued for Save");
        }

        [Test]
        public async Task LocationRefresh_WithUnsavedEdit_KeepsTheEdit()
        {
            LocationObj obj = CachedLocation();
            obj.OffEdge = true;
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.UPDATE));

            bool changed = await new LocationServerToClientUpdater().Update(obj, ServerLocation());

            Assert.That(changed, Is.False);
            Assert.That(obj.OffEdge, Is.True);
            Assert.That(obj.Terminal, Is.False, "the server value waits until the user's edit is saved");
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.UPDATE));
        }

        [Test]
        public async Task LocationEditAfterRefresh_IsStillQueued()
        {
            LocationObj obj = CachedLocation();
            await new LocationServerToClientUpdater().Update(obj, ServerLocation());

            obj.Terminal = false;

            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.UPDATE));
        }

        [Test]
        public async Task StructureTypeRefresh_AppliesServerValues_AndStaysNone()
        {
            var obj = new StructureTypeObj(3) { Name = "cached", Code = "C" };
            obj.DBAction = DBACTION.NONE;

            bool changed = await new StructureTypeServerToClientUpdater().Update(obj,
                new StructureType { Id = 3, Name = "server", Code = "S", Notes = string.Empty, Color = 0xFF00FF00u });

            Assert.That(changed, Is.True);
            Assert.That(obj.Name, Is.EqualTo("server"));
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.NONE));
        }
    }
}
