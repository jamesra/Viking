using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WebAnnotationModel.Objects;
using Microsoft.Extensions.DependencyInjection;
using Viking.AnnotationServiceTypes;

namespace WebAnnotationModel.gRPC
{

    public static class StructureLinkToStructureUpdaterExtensions
    {
        public static IServiceCollection AddDefaultStructureLinkToStructureUpdater(this IServiceCollection service)
        {
            service.AddSingleton<StructureLinkToStructureUpdater>(); 
            return service;
        }
    }

    /// <summary>
    /// Keeps StructureObj.Links in sync with StructureLinkStore. Only cached structures are updated, as the WCF
    /// StructureLinkStore did: fetching the far end of every link would pull in the whole link graph one structure at a
    /// time. A structure that reaches the store after its links takes them from the link cache instead.
    /// </summary>
    public class StructureLinkToStructureUpdater
    {
        private readonly IStructureLinkStore StructureLinkStore;
        private readonly IStructureStore StructureStore;

        public StructureLinkToStructureUpdater(IStructureLinkStore structureLinkStore,
            IStructureStore structureStore)
        {
            StructureLinkStore = structureLinkStore;
            StructureStore = structureStore;

            StructureLinkStore.CollectionChanged += OnStructureLinkCollectionChanged;
            StructureStore.CollectionChanged += OnStructureCollectionChanged;
        }

        private void OnStructureLinkCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) =>
            _ = ApplyCollectionChangeAsync(e);

        /// <summary>
        /// Applies link add/remove off the CollectionChanged thread. Add has null OldItems; Remove has null NewItems.
        /// </summary>
        private async Task ApplyCollectionChangeAsync(NotifyCollectionChangedEventArgs e)
        {
            try
            {
                if (e.OldItems != null)
                {
                    await Task.WhenAll(e.OldItems.Cast<StructureLinkObj>()
                        .Select(sl => RemoveLinkToStructures(sl.ID))).ConfigureAwait(false);
                }

                if (e.NewItems != null)
                {
                    await Task.WhenAll(e.NewItems.Cast<StructureLinkObj>()
                        .Select(AddLinkToStructures)).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private void OnStructureCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                _ = AttachCachedLinksAsync(e.NewItems.Cast<StructureObj>());
        }

        /// <summary>
        /// Gives structures new to the store the cached links that name them. The link store raises no event for a
        /// link it already holds, so the far end of a link loaded with another section's structures gets it here.
        /// </summary>
        private async Task AttachCachedLinksAsync(IEnumerable<StructureObj> added)
        {
            try
            {
                var byID = new Dictionary<long, StructureObj>();
                foreach (StructureObj s in added.Where(s => s != null))
                    byID[s.ID] = s;

                var tasks = new List<Task<bool>>();
                foreach (StructureLinkObj link in StructureLinkStore.CachedObjects)
                {
                    if (link.SourceID == link.TargetID)
                        continue;

                    if (byID.TryGetValue(link.SourceID, out StructureObj source))
                        tasks.Add(source.AddLinkAsync(link));
                    if (byID.TryGetValue(link.TargetID, out StructureObj target))
                        tasks.Add(target.AddLinkAsync(link));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private async Task<bool> AddLinkToStructures(StructureLinkObj link)
        {
            Debug.Assert(link.SourceID != link.TargetID, "Trying to link structure to itself");
            if (link.SourceID == link.TargetID)
                return false;

            StructureStore.TryGetObjectByID(link.SourceID, out StructureObj SourceObj);
            StructureStore.TryGetObjectByID(link.TargetID, out StructureObj TargetObj);

            var tOne = SourceObj?.AddLinkAsync(link) ?? Task.FromResult(false);
            var tTwo = TargetObj?.AddLinkAsync(link) ?? Task.FromResult(false);

            var ResultOne = await tOne.ConfigureAwait(false);
            var ResultTwo = await tTwo.ConfigureAwait(false);

            return ResultOne || ResultTwo;
        }

        private async Task<bool> RemoveLinkToStructures(StructureLinkKey link)
        {
            Debug.Assert(link.SourceID != link.TargetID, "Trying to link structure to itself");
            if (link.SourceID == link.TargetID)
                return false;

            StructureStore.TryGetObjectByID(link.SourceID, out StructureObj SourceObj);
            StructureStore.TryGetObjectByID(link.TargetID, out StructureObj TargetObj);

            var tOne = SourceObj?.RemoveLinkAsync(link) ?? Task.FromResult(false);
            var tTwo = TargetObj?.RemoveLinkAsync(link) ?? Task.FromResult(false);

            var ResultOne = await tOne.ConfigureAwait(false);
            var ResultTwo = await tTwo.ConfigureAwait(false);

            return ResultOne || ResultTwo;
        }
    }
}
