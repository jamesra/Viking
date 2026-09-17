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

    public static class PermittedStructureLinkToStructureTypeUpdaterExtensions
    {
        public static IServiceCollection AddDefaultPermittedStructureLinkToStructureTypeUpdater(this IServiceCollection service)
        {
            service.AddSingleton<PermittedStructureLinkToStructureUpdater>(); 
            return service;
        }
    }

    /// <summary>
    /// Attaches permitted-link rules to StructureTypeObj when PermittedStructureLinkStore changes.
    /// </summary>
    class PermittedStructureLinkToStructureUpdater
    {
        private readonly IPermittedStructureLinkStore PermittedStructureLinkStore;
        private readonly IStructureTypeStore StructureTypeStore;

        PermittedStructureLinkToStructureUpdater(IPermittedStructureLinkStore permittedStructureLinkStore,
            IStructureTypeStore structureTypeStore)
        {
            PermittedStructureLinkStore = permittedStructureLinkStore;
            StructureTypeStore = structureTypeStore;

            permittedStructureLinkStore.CollectionChanged += OnPermittedStructureLinkCollectionChanged;
        }

        private void OnPermittedStructureLinkCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) =>
            _ = ApplyCollectionChangeAsync(e);

        /// <summary>
        /// Applies permitted-link add/remove off the CollectionChanged thread. Add has null OldItems; Remove has null NewItems.
        /// </summary>
        private async Task ApplyCollectionChangeAsync(NotifyCollectionChangedEventArgs e)
        {
            try
            {
                if (e.OldItems != null)
                {
                    await Task.WhenAll(e.OldItems.Cast<PermittedStructureLinkObj>()
                        .Select(sl => RemovePermittedLinkToStructureTypes(sl.ID, CancellationToken.None))).ConfigureAwait(false);
                }

                if (e.NewItems != null)
                {
                    await Task.WhenAll(e.NewItems.Cast<PermittedStructureLinkObj>()
                        .Select(sl => AddPermittedLinkToStructureTypes(sl, CancellationToken.None))).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private async Task<bool> AddPermittedLinkToStructureTypes(PermittedStructureLinkObj  link, CancellationToken token)
        {
            var structureTypes = await StructureTypeStore.GetObjectsByIDs(new long[] { link.SourceTypeID, link.TargetTypeID }, token).ConfigureAwait(false);

            foreach (var t in structureTypes.Found)
            {
                await t.TryAddPermittedLink(link).ConfigureAwait(false);
            }

            return true;
        }

        private async Task<bool> RemovePermittedLinkToStructureTypes(PermittedStructureLinkKey link, CancellationToken token)
        {
            var structureTypes = await StructureTypeStore.GetObjectsByIDs(new long[] { link.SourceTypeID, link.TargetTypeID }, token).ConfigureAwait(false);

            foreach (var t in structureTypes.Found)
            {
                await t.TryRemovePermittedLink(link).ConfigureAwait(false);
            }

            return true;
        }
    }
}