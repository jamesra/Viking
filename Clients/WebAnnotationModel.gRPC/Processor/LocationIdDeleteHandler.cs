using System.Threading.Tasks;
using WebAnnotationModel;

namespace WebAnnotationModel.gRPC
{
    class LocationIdDeleteHandler : IServerQueryDeleteHandler<long>
    {
        private readonly LocationStore _locations;

        public LocationIdDeleteHandler(ILocationStore locations)
        {
            _locations = locations as LocationStore
                ?? throw new System.ArgumentException($"{nameof(ILocationStore)} must be {nameof(LocationStore)}", nameof(locations));
        }

        public Task ProcessServerDelete(long deletedID) =>
            _locations.ApplyDeletedLocationIdsAsync(new[] { deletedID });

        public Task ProcessServerDelete(long[] deletedIDs) =>
            _locations.ApplyDeletedLocationIdsAsync(deletedIDs);
    }
}
