using gRPCAnnotationService;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Xunit;
using static ConnectomeDataModelCoreTests.LocationServiceUpdateDeleteTests;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins the links <see cref="StructureService.CreateStructure"/> saves for its first location's Links: one
    /// LocationLink per distinct peer, none for peer 0, the new location itself, or a link the database already
    /// holds, and a peer listed twice saves one link and commits instead of failing on an EF identity conflict.
    /// Offline, on the database fake of <see cref="LocationServiceUpdateDeleteTests"/>.
    /// </summary>
    public class StructureServiceCreateTests
    {
        /// <summary>The structure is saved first and takes the first generated ID; its location takes the next.</summary>
        private const long NewLocationId = FakeDatabase.FirstGeneratedId + 1;

        [Theory]
        [InlineData(new long[0], new long[0])]
        [InlineData(new long[] { 5 }, new long[] { 5 })]
        [InlineData(new long[] { 5, 5 }, new long[] { 5 })]
        [InlineData(new long[] { 7, 5, 7 }, new long[] { 5, 7 })]
        [InlineData(new long[] { 0, NewLocationId, 5 }, new long[] { 5 })]
        [InlineData(new long[] { 2000, 5 }, new long[] { 5, 2000 })]
        public async Task SavesOneLinkPerDistinctPeer(long[] peers, long[] linked)
        {
            var store = new FakeDatabase();
            using var db = NewContext(store);

            var response = await NewService(db).CreateStructure(Request(peers), new TestCallContext());

            Assert.Equal(NewLocationId, response.NewAnnotation.Id);
            Assert.Equal(linked.Select(peer => Ordered(peer, NewLocationId)).ToArray(), store.Saved.AddedLinks);
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task APeerTheDatabaseAlreadyLinksGetsNoSecondLink()
        {
            var store = new FakeDatabase();
            store.Links.Add((5, NewLocationId));
            using var db = NewContext(store);

            await NewService(db).CreateStructure(Request(5, 7, 5), new TestCallContext());

            Assert.Equal(new[] { (7L, NewLocationId) }, store.Saved.AddedLinks);
            Assert.True(store.Committed);
        }

        private static StructureService NewService(Viking.DataModel.Annotation.AnnotationContext db) =>
            new(db, NullLogger<StructureService>.Instance);

        private static (long A, long B) Ordered(long x, long y) => x < y ? (x, y) : (y, x);

        private static CreateStructureRequest Request(params long[] peers)
        {
            var location = NewLocation();
            location.Links.AddRange(peers);
            return new CreateStructureRequest { NewStructure = new Structure { TypeId = 1 }, NewAnnotation = location };
        }
    }
}
