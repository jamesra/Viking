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

    public static class PermittedStructureLinkToStructureTypeUpdaterExtensions
    {
        public static IServiceCollection AddDefaultPermittedStructureLinkToStructureTypeUpdater(this IServiceCollection service)
        {
            service.AddSingleton<PermittedStructureLinkToStructureUpdater>(); 
            return service;
        }
    }

    /// <summary>
    /// Updates structures with structurelinks when the structure link store is updated
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

        private void OnPermittedStructureLinkCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var Tasks = new List<Task>();
            foreach (var sl in e.OldItems.Cast<PermittedStructureLinkObj>())
            {
                Tasks.Add(RemovePermittedLinkToStructureTypes(sl.ID, CancellationToken.None));
            }

            Task.WaitAll(Tasks.ToArray());
            Tasks.Clear();

            foreach (var sl in e.NewItems.Cast<PermittedStructureLinkObj>())
            {
                Tasks.Add(AddPermittedLinkToStructureTypes(sl, CancellationToken.None));
            }

            Task.WaitAll(Tasks.ToArray());
            Tasks.Clear();
        }

        private async Task<bool> AddPermittedLinkToStructureTypes(PermittedStructureLinkObj  link, CancellationToken token)
        {
            var structureTypes = await StructureTypeStore.GetObjectsByIDs(new long[] { link.SourceTypeID, link.TargetTypeID }, AskServer: true, token).ConfigureAwait(false);

            foreach (var t in structureTypes)
            {
                await t.TryAddPermittedLink(link).ConfigureAwait(false);
            }

            return true;
        }

        private async Task<bool> RemovePermittedLinkToStructureTypes(PermittedStructureLinkKey link, CancellationToken token)
        {
            var structureTypes = await StructureTypeStore.GetObjectsByIDs(new long[] { link.SourceTypeID, link.TargetTypeID }, AskServer: true, token).ConfigureAwait(false);

            foreach (var t in structureTypes)
            {
                await t.TryRemovePermittedLink(link).ConfigureAwait(false);
            }

            return true;
        }
    }
}