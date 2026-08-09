using Geometry;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.AnnotationServiceTypes;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC
{

    internal class LocationLinkStore : StoreBaseWithKey<LocationLinkKey, LocationLinkObj, ILocationLink, ILocationLink, ILocationLink>, ILocationLinkStore
    { 
        public LocationLinkStore(
            IServerAnnotationsClientFactory<IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>> clientFactory,
            IObjectConverter<LocationLinkObj, ILocationLink> objToServerObjConverter,
            IObjectConverter<ILocationLink, LocationLinkObj> serverObjToObjConverter,
            IQueryLogger log) : base(clientFactory, null, objToServerObjConverter, serverObjToObjConverter, log)
        {
        }

        protected override Task Init() => Task.CompletedTask;

        /// <summary>
        /// Synchronous wrapper so exceptions surface to the caller's try/catch, matching legacy WCF-store semantics.
        /// </summary>
        public LocationLinkObj CreateLink(long A, long B)
        {
            var newLink = new LocationLinkObj(A, B);
            var client = ClientFactory.GetOrCreate();
            var serverResult = client.Create(newLink, CancellationToken.None).Result;
            return Add(ServerObjConverter.Convert(serverResult)).Result;
        }

        /// <summary>
        /// Synchronous wrapper so exceptions surface to the caller's try/catch, matching legacy WCF-store semantics.
        /// </summary>
        public bool DeleteLink(long A, long B)
        {
            var key = new LocationLinkKey(A, B);
            var client = ClientFactory.GetOrCreate();
            client.Delete(key, CancellationToken.None).Wait();
            var deleted = Remove(key).Result;
            return deleted != null;
        }

        /// <summary>
        /// Location-link UpdateAsync is a no-op (links have no mutable fields). Route INSERT/DELETE
        /// through the dedicated create/delete RPCs so a deferred Save() still hits the server.
        /// </summary>
        protected override async Task<bool> Save(List<LocationLinkObj> changedObjects, CancellationToken token)
        {
            if (changedObjects.Count == 0)
                return true;

            var client = ClientFactory.GetOrCreate();
            foreach (var obj in changedObjects)
            {
                switch (obj.DBAction)
                {
                    case DBACTION.DELETE:
                        await client.Delete(obj.ID, token).ConfigureAwait(false);
                        break;
                    case DBACTION.INSERT:
                        await client.Create(obj, token).ConfigureAwait(false);
                        break;
                    case DBACTION.UPDATE:
                    case DBACTION.NONE:
                        break;
                    default:
                        throw new NotSupportedException($"Unexpected location-link DBAction {obj.DBAction}");
                }

                obj.DBAction = DBACTION.NONE;
            }

            return true;
        }

        /// <summary>
        /// Section sync via LocationsClient.GetLocationLinksForSection (results + deleted keys).
        /// </summary>
        public async Task GetLinksForSectionAsync(long section, DateTime? modifiedAfter = null, CancellationToken token = default)
        {
            var client = ClientFactory.GetOrCreate();
            if (!(client is ILocationsClient locationsClient))
                throw new NotSupportedException(
                    $"{client.GetType().Name} does not implement {nameof(ILocationsClient)} section link sync.");

            var update = await locationsClient
                .GetLocationLinksForSectionAsync(section, modifiedAfter, token)
                .ConfigureAwait(false);

            var changes = await ServerQueryResultsHandler
                .ProcessServerUpdate(update.NewOrUpdated, update.DeletedIDs)
                .ConfigureAwait(false);
            await CallOnCollectionChanged(changes).ConfigureAwait(false);
        }

        public async Task MergeServerLinksAsync(IEnumerable<ILocationLink> links, DateTime? queryTime = null, CancellationToken token = default)
        {
            var arr = links?.Where(l => l != null).ToArray() ?? Array.Empty<ILocationLink>();
            if (arr.Length == 0)
                return;

            token.ThrowIfCancellationRequested();
            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(
                new ServerUpdate<LocationLinkKey, ILocationLink[]>(
                    queryTime ?? DateTime.UtcNow, arr, Array.Empty<LocationLinkKey>()))
                .ConfigureAwait(false);
            await CallOnCollectionChanged(changes).ConfigureAwait(false);
        }
    }
}
