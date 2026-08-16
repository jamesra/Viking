using System;
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

    public static class StructureLinkToStructureUpdaterExtensions
    {
        public static IServiceCollection AddDefaultStructureLinkToStructureUpdater(this IServiceCollection service)
        {
            service.AddSingleton<StructureLinkToStructureUpdater>(); 
            return service;
        }
    }

    /// <summary>
    /// Updates structures with structurelinks when the structure link store is updated
    /// </summary>
    class StructureLinkToStructureUpdater
    {
        private readonly IStructureLinkStore StructureLinkStore;
        private readonly IStructureStore StructureStore;

        StructureLinkToStructureUpdater(IStructureLinkStore structureLinkStore,
            IStructureStore structureStore)
        {
            StructureLinkStore = structureLinkStore;
            StructureStore = structureStore;

            StructureLinkStore.CollectionChanged += OnStructureLinkCollectionChanged;
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
                        .Select(sl => RemoveLinkToStructures(sl.ID, CancellationToken.None))).ConfigureAwait(false);
                }

                if (e.NewItems != null)
                {
                    await Task.WhenAll(e.NewItems.Cast<StructureLinkObj>()
                        .Select(sl => AddLinkToStructures(sl, CancellationToken.None))).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private async Task<bool> AddLinkToStructures(StructureLinkObj link, CancellationToken token)
        {
            Debug.Assert(link.SourceID != link.TargetID, "Trying to link structure to itself");
            if (link.SourceID == link.TargetID)
                return false;

            var SourceTask = StructureStore.GetObjectByID(link.SourceID, token);
            var TargetTask = StructureStore.GetObjectByID(link.TargetID, token);
            if (token.IsCancellationRequested)
                return false;

            StructureObj SourceObj = await SourceTask.ConfigureAwait(false);
            StructureObj TargetObj = await TargetTask.ConfigureAwait(false);

            var tOne = SourceObj?.AddLinkAsync(link) ?? Task.FromResult(false);
            var tTwo = TargetObj?.AddLinkAsync(link) ?? Task.FromResult(false);

            var ResultOne = await tOne.ConfigureAwait(false);
            var ResultTwo = await tTwo.ConfigureAwait(false);

            return ResultOne || ResultTwo;
        }

        private async Task<bool> RemoveLinkToStructures(StructureLinkKey link, CancellationToken token)
        {
            Debug.Assert(link.SourceID != link.TargetID, "Trying to link structure to itself");
            if (link.SourceID == link.TargetID)
                return false;

            var SourceTask = StructureStore.GetObjectByID(link.SourceID, token);
            var TargetTask = StructureStore.GetObjectByID(link.TargetID, token);
            if (token.IsCancellationRequested)
                return false;

            StructureObj SourceObj = await SourceTask.ConfigureAwait(false);
            StructureObj TargetObj = await TargetTask.ConfigureAwait(false);

            var tOne = SourceObj?.RemoveLinkAsync(link) ?? Task.FromResult(false);
            var tTwo = TargetObj?.RemoveLinkAsync(link) ?? Task.FromResult(false);

            var ResultOne = await tOne.ConfigureAwait(false);
            var ResultTwo = await tTwo.ConfigureAwait(false);

            return ResultOne || ResultTwo;
        }
    }
}