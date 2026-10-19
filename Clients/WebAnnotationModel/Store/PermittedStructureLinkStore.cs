using AnnotationService.Types;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ServiceModel;
using WebAnnotationModel.Objects;
using WebAnnotationModel.Service;

namespace WebAnnotationModel
{

    public class PermittedStructureLinkStore : StoreBaseWithKey<AnnotatePermittedStructureLinksClient, IAnnotatePermittedStructureLinks, PermittedStructureLinkKey, PermittedStructureLinkObj, PermittedStructureLink>
    {
        public PermittedStructureLinkStore()
        {
            channelFactory =
                new ChannelFactory<IAnnotatePermittedStructureLinks>("Annotation.Service.Interfaces.IAnnotatePermittedStructureLinks-Binary");

            channelFactory.Credentials.UserName.UserName = State.UserCredentials.UserName;
            channelFactory.Credentials.UserName.Password = State.UserCredentials.Password;
        }

        public override void Init()
        {
            return;
        }

        protected override PermittedStructureLinkKey[] ProxyUpdate(IAnnotatePermittedStructureLinks proxy, PermittedStructureLink[] linkObjs)
        {
            proxy.UpdatePermittedStructureLinks(linkObjs);
            return [];
        }

        protected override PermittedStructureLink ProxyGetByID(IAnnotatePermittedStructureLinks proxy, PermittedStructureLinkKey ID) => throw new NotImplementedException();

        protected override PermittedStructureLink[] ProxyGetByIDs(IAnnotatePermittedStructureLinks proxy, PermittedStructureLinkKey[] IDs) => throw new NotImplementedException();

        public override System.Collections.Concurrent.ConcurrentDictionary<PermittedStructureLinkKey, PermittedStructureLinkObj> GetLocalObjectsForSection(long SectionNumber) => throw new NotImplementedException();

        protected override PermittedStructureLink[] ProxyGetBySection(IAnnotatePermittedStructureLinks proxy, long SectionNumber, DateTime LastQuery, out long TicksAtQueryExecute, out PermittedStructureLinkKey[] DeletedLocations) => throw new NotImplementedException();

        protected override IAsyncResult ProxyBeginGetBySectionRegion(IAnnotatePermittedStructureLinks proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery, AsyncCallback callback, object asynchState) => throw new NotImplementedException();

        protected override PermittedStructureLink[] ProxyGetBySectionRegionCallback(out long TicksAtQueryExecute, out PermittedStructureLinkKey[] DeletedLocations, GetObjectBySectionCallbackState<IAnnotatePermittedStructureLinks, PermittedStructureLinkObj> state, IAsyncResult result) => throw new NotImplementedException();

        protected override PermittedStructureLink[] ProxyGetBySectionRegion(IAnnotatePermittedStructureLinks proxy,
                                                             long SectionNumber,
                                                             BoundingRectangle BBox,
                                                             double MinRadius,
                                                             DateTime LastQuery,
                                                             out long TicksAtQueryExecute,
                                                             out PermittedStructureLinkKey[] DeletedLocations) => throw new NotImplementedException();


        protected override IAsyncResult ProxyBeginGetBySection(IAnnotatePermittedStructureLinks proxy, long SectionNumber, DateTime LastQuery, AsyncCallback callback, object asynchState) => throw new NotImplementedException();

        protected override PermittedStructureLink[] ProxyGetBySectionCallback(out long TicksAtQueryExecute, out PermittedStructureLinkKey[] DeletedLocations, GetObjectBySectionCallbackState<IAnnotatePermittedStructureLinks, PermittedStructureLinkObj> state, IAsyncResult result) => throw new NotImplementedException();

        public PermittedStructureLinkObj Create(PermittedStructureLinkObj link)
        {
            var proxy = CreateProxy();
            {
                IAnnotatePermittedStructureLinks client = (IAnnotatePermittedStructureLinks)proxy;
                PermittedStructureLink dblink = client.CreatePermittedStructureLink(link.GetData());
                PermittedStructureLinkObj created_link = new(dblink);
                Add(created_link);
                return created_link;
            }
        }

        protected override ChangeInventory<PermittedStructureLinkObj> InternalAdd(PermittedStructureLinkObj[] newObjs)
        {
            List<PermittedStructureLinkObj> ValidObjs = new(newObjs.Length);

            foreach (PermittedStructureLinkObj link in newObjs)
            {

                StructureTypeObj SourceObj = Store.StructureTypes.GetObjectByID(link.SourceTypeID, false);
                StructureTypeObj TargetObj = Store.StructureTypes.GetObjectByID(link.TargetTypeID, false);

                SourceObj?.TryAddPermittedLink(link);

                if (TargetObj != null && TargetObj != SourceObj)
                    TargetObj.TryAddPermittedLink(link);

                ValidObjs.Add(link);
            }

            ChangeInventory<PermittedStructureLinkObj> inventory = base.InternalAdd([.. ValidObjs]);
            QueueUnsavedLinks(inventory.AddedObjects, ChangedObjects);
            return inventory;
        }

        /// <summary>
        /// Puts links that are not on the server yet (DBAction other than NONE) on the dirty set so the next
        /// <c>Save()</c> sends them.
        /// A link built with the editor's constructor is already INSERT before the store subscribes to its
        /// DBAction changes, so without this the structure type editor's Apply never created it on the server.
        /// Links loaded from the server arrive with DBAction NONE and are skipped.
        /// Re-adding a link whose removal is still queued cancels that DELETE and leaves the new link NONE, because
        /// the server row still exists (a DELETE is only queued for saved links; see <see cref="Remove(PermittedStructureLinkKey)"/>).
        /// </summary>
        /// <param name="addedLinks">Links that were newly added to the store.</param>
        /// <param name="changedObjects">The store's dirty set, keyed by link.</param>
        internal static void QueueUnsavedLinks(IEnumerable<PermittedStructureLinkObj> addedLinks,
                                               ConcurrentDictionary<PermittedStructureLinkKey, PermittedStructureLinkObj> changedObjects)
        {
            foreach (PermittedStructureLinkObj link in addedLinks)
            {
                if (link.DBAction == DBACTION.NONE)
                    continue;

                if (link.DBAction == DBACTION.INSERT &&
                    changedObjects.TryGetValue(link.ID, out PermittedStructureLinkObj queued) &&
                    queued.DBAction == DBACTION.DELETE &&
                    changedObjects.TryRemove(link.ID, out _))
                {
                    link.DBAction = DBACTION.NONE;
                    continue;
                }

                changedObjects.TryAdd(link.ID, link);
            }
        }

        /// <summary>
        /// Removes a link from the store. A link added since the last Save (DBAction INSERT) is not on the server,
        /// so its queued INSERT is cancelled and nothing is sent; a saved link is queued for DELETE as usual.
        /// </summary>
        /// <param name="ID">Key of the link to remove.</param>
        /// <returns>True if the link was in the store.</returns>
        public override bool Remove(PermittedStructureLinkKey ID)
        {
            if (IDToObject.TryGetValue(ID, out PermittedStructureLinkObj link) && CancelUnsavedInsert(link, ChangedObjects))
            {
                PermittedStructureLinkObj deleted = InternalDelete(ID);
                CallOnCollectionChangedForDelete(new[] { deleted });
                return true;
            }

            return base.Remove(ID);
        }

        /// <summary>
        /// Drops a never-saved link's pending INSERT from the dirty set. Sending a DELETE instead would hit whatever
        /// server row shares its source and target (the server key ignores Bidirectional), or nothing at all.
        /// </summary>
        /// <param name="link">The link being removed.</param>
        /// <param name="changedObjects">The store's dirty set, keyed by link.</param>
        /// <returns>True if the link was unsaved (INSERT) and is no longer queued; false for a saved link.</returns>
        internal static bool CancelUnsavedInsert(PermittedStructureLinkObj link,
                                                 ConcurrentDictionary<PermittedStructureLinkKey, PermittedStructureLinkObj> changedObjects)
        {
            if (link.DBAction != DBACTION.INSERT)
                return false;

            changedObjects.TryRemove(link.ID, out _);
            return true;
        }

        protected override List<PermittedStructureLinkObj> InternalDelete(PermittedStructureLinkKey[] linkKeys)
        {
            foreach (PermittedStructureLinkKey key in linkKeys)
            {
                /*
                PermittedStructureLinkObj link = Store.StructureLinks.GetObjectByID(key, false);
                if (link is null)
                    continue; 
                */

                StructureTypeObj SourceObj = Store.StructureTypes.GetObjectByID(key.SourceTypeID, false);
                StructureTypeObj TargetObj = Store.StructureTypes.GetObjectByID(key.TargetTypeID, false);

                SourceObj?.TryRemovePermittedLink(key);

                TargetObj?.TryRemovePermittedLink(key);
            }

            return base.InternalDelete(linkKeys);
        }

    }
}
