using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Geometry;
using NUnit.Framework;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Store-level smoke against the Docker annotation stack via <see cref="IAnnotationStores"/>.
    /// Requires Start-AnnotationTestStack.ps1 -ApplySchema.
    /// </summary>
    [TestFixture]
    public class AnnotationStoresSmokeTests
    {
        private AnnotationStoresTestHost _host;

        [SetUp]
        public async Task SetUp()
        {
            _host = await AnnotationStoresTestHost.CreateAsync();
        }

        [TearDown]
        public void TearDown()
        {
            _host?.Dispose();
            _host = null;
        }

        [Test]
        public void Initialize_LoadsSeedStructureType()
        {
            Assert.That(_host.Stores.StructureTypes.TryGetObjectByID(1, out var type), Is.True);
            Assert.That(type, Is.Not.Null);
            Assert.That(type.ID, Is.EqualTo(1));
            Assert.That(type.Name, Does.Contain("Neuron").IgnoreCase);
        }

        [Test]
        public async Task Locations_GetObjectByID_ReturnsSeedLocation()
        {
            var loc = await _host.Stores.Locations.GetObjectByID(1, CancellationToken.None);
            Assert.That(loc, Is.Not.Null);
            Assert.That(loc.ID, Is.EqualTo(1));
            Assert.That(loc.ParentID, Is.EqualTo(1));
            Assert.That(loc.Section, Is.EqualTo(1));
        }

        [Test]
        public async Task Structures_GetObjectByID_ReturnsSeedStructure()
        {
            var structure = await _host.Stores.Structures.GetObjectByID(1, CancellationToken.None);
            Assert.That(structure, Is.Not.Null);
            Assert.That(structure.ID, Is.EqualTo(1));
            Assert.That(structure.Label, Is.EqualTo("seed-1"));
            Assert.That(structure.TypeID, Is.EqualTo(1));

            var locs = await _host.Stores.Locations.GetStructureLocations(1, QueryTargets.Server);
            Assert.That(locs.Any(l => l.ID == 1), Is.True);
        }

        [Test]
        public async Task CreateMoveLinkDelete_RoundtripViaStores()
        {
            var stores = _host.Stores;
            var type = stores.StructureTypes.TryGetObjectByID(1, out var cachedType)
                       ? cachedType
                       : await stores.StructureTypes.GetObjectByID(1, CancellationToken.None);
            Assert.That(type, Is.Not.Null);

            var section = 42;
            var label = $"store-smoke-{Guid.NewGuid():N}".Substring(0, 24);
            var structureDraft = new StructureObj(type) { Label = label };
            // Use POINT (same as seed / proto round-trips); CIRCLE becomes CURVEPOLYGON WKT.
            var shapeA = new Vector2(120, 130);
            var locationDraft = new LocationObj(structureDraft, shapeA, shapeA, section, LocationType.POINT);

            var (structure, createdLocFromCreate) = await stores.Structures.Create(structureDraft, locationDraft);
            Assert.That(structure, Is.Not.Null);
            Assert.That(structure.ID, Is.GreaterThan(0));
            Assert.That(createdLocFromCreate, Is.Not.Null);
            Assert.That(createdLocFromCreate.ID, Is.GreaterThan(0));

            long structureId = structure.ID;
            long locationAId = createdLocFromCreate.ID;
            long? locationBId = null;

            try
            {
                var locationA = await stores.Locations.Refresh(locationAId, CancellationToken.None);
                Assert.That(locationA, Is.Not.Null);

                // Move
                var moved = new Vector2(200, 210);
                locationA.MosaicShape = moved;
                locationA.VolumeShape = moved;
                Assert.That(await stores.Locations.Save(), Is.True);

                var reloadedA = await stores.Locations.Refresh(locationAId, CancellationToken.None);
                Assert.That(reloadedA.Position.X, Is.EqualTo(200).Within(0.5));
                Assert.That(reloadedA.Position.Y, Is.EqualTo(210).Within(0.5));

                // Second location + location link
                var shapeB = new Vector2(220, 230);
                var locationBDraft = new LocationObj(structure, shapeB, shapeB, section, LocationType.POINT);
                var locationB = await stores.Locations.Create(locationBDraft);
                Assert.That(locationB, Is.Not.Null);
                locationBId = locationB.ID;

                var link = await stores.LocationLinks.CreateLink(locationAId, locationB.ID);
                Assert.That(link, Is.Not.Null);
                Assert.That(stores.LocationLinks.Contains(new LocationLinkKey(locationAId, locationB.ID)), Is.True);

                Assert.That(await stores.LocationLinks.DeleteLink(locationAId, locationB.ID), Is.True);

                // Structure link to seed structure (id 1)
                var structureLink = new StructureLinkObj(structureId, 1, Bidirectional: true);
                var createdStructureLink = await stores.StructureLinks.Create(structureLink);
                Assert.That(createdStructureLink, Is.Not.Null);

                var linksForStructure = await stores.StructureLinks.GetLinks(structureId);
                Assert.That(linksForStructure.Any(l =>
                    (l.SourceID == structureId && l.TargetID == 1) ||
                    (l.SourceID == 1 && l.TargetID == structureId)), Is.True);
            }
            finally
            {
                try
                {
                    if (locationBId.HasValue)
                        await stores.LocationLinks.DeleteLink(locationAId, locationBId.Value);
                }
                catch { /* best-effort cleanup */ }

                try
                {
                    var toDelete = await stores.Structures.GetObjectByID(structureId, CancellationToken.None);
                    if (toDelete != null)
                    {
                        await stores.Structures.Remove(toDelete);
                        await stores.Structures.Save();
                    }
                }
                catch { /* best-effort cleanup */ }
            }
        }
    }
}
