using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebAnnotationModel.Objects;
using Microsoft.Extensions.DependencyInjection;
using Viking.AnnotationServiceTypes;

namespace WebAnnotationModel.gRPC
{

    public static class LocationLinkToLocationUpdaterExtensions
    {
        public static IServiceCollection AddDefaultLocationLinkToLocationUpdater(this IServiceCollection service)
        {
            service.AddSingleton<LocationLinkToLocationUpdater>(); 
            return service;
        }
    }

    /// <summary>
    /// Keeps LocationObj.Links in sync when LocationLinkStore changes.
    /// Constructed by DI at store initialization; does not fetch missing endpoints.
    /// </summary>
    public class LocationLinkToLocationUpdater
    {
        private readonly ILocationLinkStore LocationLinkStore;
        private readonly ILocationStore LocationStore;

        public LocationLinkToLocationUpdater(ILocationLinkStore locationLinkStore,
            ILocationStore locationStore)
        {
            LocationLinkStore = locationLinkStore;
            LocationStore = locationStore;

            LocationLinkStore.CollectionChanged += OnLocationLinkCollectionChanged;
        }

        private void OnLocationLinkCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var Tasks = new List<Task>();
            if (e.OldItems != null)
            {
                foreach (var sl in e.OldItems.Cast<LocationLinkObj>())
                {
                    Tasks.Add(RemoveLinkToLocations(sl.ID, CancellationToken.None));
                }

                Task.WaitAll(Tasks.ToArray());
                Tasks.Clear();
            }

            if (e.NewItems == null)
                return;

            foreach (var sl in e.NewItems.Cast<LocationLinkObj>())
            {
                Tasks.Add(AddLinkToLocations(sl, CancellationToken.None));
            }

            Task.WaitAll(Tasks.ToArray());
        }

        private async Task<bool> AddLinkToLocations(LocationLinkObj link, CancellationToken token)
        {
            Debug.Assert(link.A != link.B, "Trying to link location to itself");
            if (link.A == link.B)
                return false;

            LocationStore.TryGetObjectByID(link.A, out LocationObj SourceObj);
            LocationStore.TryGetObjectByID(link.B, out LocationObj TargetObj);

            var tOne = SourceObj?.AddLinkAsync(link.OtherKey(SourceObj.ID)) ?? Task.FromResult(false);
            var tTwo = TargetObj?.AddLinkAsync(link.OtherKey(TargetObj.ID)) ?? Task.FromResult(false);

            var ResultOne = await tOne.ConfigureAwait(false);
            var ResultTwo = await tTwo.ConfigureAwait(false);

            return ResultOne || ResultTwo;
        }

        private async Task<bool> RemoveLinkToLocations(LocationLinkKey link, CancellationToken token)
        {
            Debug.Assert(link.A != link.B, "Trying to link structure to itself");
            if (link.B == link.A)
                return false;

            LocationStore.TryGetObjectByID(link.A, out LocationObj SourceObj);
            LocationStore.TryGetObjectByID(link.B, out LocationObj TargetObj);

            var tOne = SourceObj?.RemoveLinkAsync(link.OtherKey(SourceObj.ID)) ?? Task.FromResult(false);
            var tTwo = TargetObj?.RemoveLinkAsync(link.OtherKey(TargetObj.ID)) ?? Task.FromResult(false);

            var ResultOne = await tOne.ConfigureAwait(false);
            var ResultTwo = await tTwo.ConfigureAwait(false);

            return ResultOne || ResultTwo;
        }
    }
}