using AnnotationService.Types;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using Viking.AnnotationServiceTypes;
using WebAnnotationModel.Service;

namespace WebAnnotationModel
{
    public class LocationLinkStore : StoreBaseWithKey<AnnotateLocationsClient, IAnnotateLocations, LocationLinkKey, LocationLinkObj, LocationLink>
    {
        ConcurrentDictionary<long, ConcurrentDictionary<LocationLinkKey, LocationLinkObj>> SectionToLocationLinks = new ConcurrentDictionary<long, ConcurrentDictionary<LocationLinkKey, LocationLinkObj>>();
          
        public LocationLinkStore()
        {
            channelFactory =
                new ChannelFactory<IAnnotateLocations>("Annotation.Service.Interfaces.IAnnotateLocations-Binary");

            channelFactory.Credentials.UserName.UserName = State.UserCredentials.UserName;
            channelFactory.Credentials.UserName.Password = State.UserCredentials.Password;
        }

        public override void Init()
        {
            Store.Locations.OnCollectionChanged += new NotifyCollectionChangedEventHandler(OnLocationsStoreChanged);
        }

        protected override LocationLink ProxyGetByID(IAnnotateLocations proxy, LocationLinkKey ID)
        {
            throw new NotImplementedException();
        }

        protected override LocationLink[] ProxyGetByIDs(IAnnotateLocations proxy, LocationLinkKey[] IDs)
        {
            throw new NotImplementedException();
        } 

        protected override LocationLinkKey[] ProxyUpdate(IAnnotateLocations proxy, LocationLink[] objects)
        {
            throw new NotImplementedException();
        }

        public void CreateLink(long A, long B)
        {
            //lock (LockObject)
            {
                using (IClientChannel proxy = (IClientChannel)CreateProxy())
                {
                    try
                    {
                        proxy.Open();
                        var client = (IAnnotateLocations)proxy;
                        client.CreateLocationLink(A, B);
                    }
                    catch (Exception e)
                    {
                        //TODO: Better message
                        Trace.WriteLine("Error creating link between locations, link not created: " + e.Message);
                        throw;
                    }
                    finally
                    {
                        if (proxy != null)
                            proxy.Close();
                    }
                }

                ChangeInventory<LocationLinkObj> inventory = InternalAdd(new LocationLinkObj(A, B));
                CallOnCollectionChanged(inventory);
            }
        }

        public void DeleteLink(long A, long B)
        {
            //lock (LockObject)
            { 
                LocationLinkObj deletedLink = null;

                //                LocationObj AObj = Store.Locations.GetObjectByID(A);
                //                LocationObj BObj = Store.Locations.GetObjectByID(B);

                using (IClientChannel proxy = CreateProxy())
                {
                    try
                    { 
                        var client = (IAnnotateLocations)proxy;
                        client.DeleteLocationLink(A, B);

                        deletedLink = InternalDelete(new LocationLinkKey(A, B));
                    }
                    catch (Exception e)
                    {
                        //TODO: Better Error message
                        Trace.WriteLine("Error deleting link between locations, link not created: " + e.Message);
                    }
                }

                if (deletedLink != null)
                {
                    CallOnCollectionChangedForDelete(new LocationLinkObj[] { deletedLink });
                }
            }
        }

        public ConcurrentDictionary<LocationLinkKey, LocationLinkObj> GetLinksCrossingSection(int SectionNumber)
        {
            ConcurrentDictionary<LocationLinkKey, LocationLinkObj> SectionLocationLinks = new ConcurrentDictionary<LocationLinkKey, LocationLinkObj>();
            SectionLocationLinks = SectionToLocationLinks.GetOrAdd(SectionNumber, SectionLocationLinks);

            //Request updates after fetching the list so we don't update the list mid-query
            GetObjectsForSectionAsynch(SectionNumber, null);

            return SectionLocationLinks;
        }


        #region GetLocationLinksForSection

        public override ConcurrentDictionary<LocationLinkKey, LocationLinkObj> GetLocalObjectsForSection(long SectionNumber)
        {
            ConcurrentDictionary<LocationLinkKey, LocationLinkObj> SectionLocationLinks;
            bool Success = SectionToLocationLinks.TryGetValue(SectionNumber, out SectionLocationLinks);
            if (Success)
            {
                return SectionLocationLinks;
            }

            return new ConcurrentDictionary<LocationLinkKey, LocationLinkObj>();
        }

        protected override LocationLink[] ProxyGetBySection(IAnnotateLocations proxy, long SectionNumber, DateTime LastQuery, out long TicksAtQueryExecute, out LocationLinkKey[] DeletedLinkKeys)
        {
            LocationLink[] deleted_links = null;
            LocationLink[] links = proxy.GetLocationLinksForSection(out TicksAtQueryExecute, out deleted_links, SectionNumber, LastQuery.Ticks);

            if (deleted_links == null)
            {
                DeletedLinkKeys = new LocationLinkKey[0];
            }
            else
            {
                DeletedLinkKeys = deleted_links.Select(link => new LocationLinkKey(link.SourceID, link.TargetID)).ToArray();
            }

            return links;
        }

        protected override LocationLink[] ProxyGetBySectionRegion(IAnnotateLocations proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery, out long TicksAtQueryExecute, out LocationLinkKey[] DeletedLinkKeys)
        {
            LocationLink[] deleted_links = null;
            LocationLink[] links = proxy.GetLocationLinksForSectionInMosaicRegion(out TicksAtQueryExecute, out deleted_links, SectionNumber, BBox, MinRadius, LastQuery.Ticks);

            if (deleted_links == null)
            {
                DeletedLinkKeys = new LocationLinkKey[0];
            }
            else
            {
                DeletedLinkKeys = deleted_links.Select(link => new LocationLinkKey(link.SourceID, link.TargetID)).ToArray();
            }

            return links;
        }

        protected override IAsyncResult ProxyBeginGetBySectionRegion(IAnnotateLocations proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery, AsyncCallback callback, object asynchState)
        {
            return proxy.BeginGetLocationLinksForSectionInMosaicRegion(SectionNumber, BBox, MinRadius, LastQuery.Ticks, callback, asynchState);
        }

        protected override IAsyncResult ProxyBeginGetBySection(IAnnotateLocations proxy, long SectionNumber, DateTime LastQuery, AsyncCallback callback, object asynchState)
        {
            return proxy.BeginGetLocationChanges(SectionNumber,
                                                 LastQuery.Ticks,
                                                 GetObjectsBySectionCallback,
                                                 asynchState);
        }

        protected override LocationLink[] ProxyGetBySectionCallback(out long TicksAtQueryExecute,
                                                                    out LocationLinkKey[] DeletedLinkKeys,
                                                                    GetObjectBySectionCallbackState<IAnnotateLocations, LocationLinkObj> state,
                                                                    IAsyncResult result)
        {
            LocationLink[] deleted_links;
            LocationLink[] links = state.Proxy.EndGetLocationLinksForSection(out TicksAtQueryExecute, out deleted_links, result);

            DeletedLinkKeys = deleted_links.Select(link => new LocationLinkKey(link.SourceID, link.TargetID)).ToArray();

            return links;
        }

        /// <summary>
        /// We override this because if the Locations are not loaded the only information we have about which sections locationLinks belong to is with the query state
        /// </summary>
        /// <param name="locations"></param>
        /// <param name="state"></param>
        /// <param name="DeletedLocations"></param>
        public override ChangeInventory<LocationLinkObj> ParseQuery(LocationLink[] newLinks,
                                           LocationLinkKey[] DeletedLocations,
                                           GetObjectBySectionCallbackState<IAnnotateLocations, LocationLinkObj> state)
        {
            ConcurrentDictionary<LocationLinkKey, LocationLinkObj> SectionLocationLinks = new ConcurrentDictionary<LocationLinkKey, LocationLinkObj>();
            SectionLocationLinks = SectionToLocationLinks.GetOrAdd(state.SectionNumber, SectionLocationLinks);
            ChangeInventory<LocationLinkObj> change_inventory = new ChangeInventory<LocationLinkObj>();
            List<LocationLinkObj> deleted_objects = null;
            if (SectionLocationLinks != null)
            {
                if (DeletedLocations != null)
                {
                    foreach (LocationLinkKey key in DeletedLocations)
                    {
                        LocationLinkObj value;
                        SectionLocationLinks.TryRemove(key, out value);
                    }

                    deleted_objects = InternalDelete(DeletedLocations);
                }

                LocationLinkObj[] listNewObj = new LocationLinkObj[newLinks.Length];
                System.Threading.Tasks.Parallel.For(0, newLinks.Length, (iLink) =>
                {
                    listNewObj[iLink] = new LocationLinkObj(newLinks[iLink]);
                    SectionLocationLinks.TryAdd(new LocationLinkKey(listNewObj[iLink].A, listNewObj[iLink].B), listNewObj[iLink]);
                });

                change_inventory = InternalAdd(listNewObj);
            }

            if (deleted_objects != null)
                change_inventory.DeletedObjects = deleted_objects;

            return change_inventory;
        }

        #endregion

        #region Internal Add/Update/Delete

        /// <summary>
        /// Add a link, updating the corresponding objects
        /// </summary>
        /// <param name="newObj"></param>
        /// <returns></returns>
        protected override ChangeInventory<LocationLinkObj> InternalAdd(LocationLinkObj[] newObj)
        {
            //Make sure the LocationObjects know about the new links we've pulled from the database
            foreach (LocationLinkObj link in newObj)
            {
                LocationObj AObj = Store.Locations.GetObjectByID(link.A, false);
                if (AObj != null)
                {
                    AObj.AddLink(link.B);
                }

                LocationObj BObj = Store.Locations.GetObjectByID(link.B, false);
                if (BObj != null)
                {
                    BObj.AddLink(link.A);
                }
            }

            return base.InternalAdd(newObj);
        }

        /// <summary>
        /// Add links from objects, no object update needed
        /// </summary>
        /// <param name="newObjs"></param>
        /// <returns></returns>
        protected ChangeInventory<LocationLinkObj> InternalAdd(IEnumerable<LocationObj> newObjs)
        {
            List<LocationLinkObj> links = new List<LocationLinkObj>();
            foreach (LocationObj obj in newObjs)
            {
                if (obj.NumLinks == 0)
                    continue;

                foreach (long linkID in obj.Links)
                {
                    //obj.AddLink(linkID);
                    links.Add(new LocationLinkObj(obj.ID, linkID));
                }
            }

            return InternalAdd(links.ToArray());
        }

        protected override List<LocationLinkObj> InternalDelete(LocationLinkKey[] keys)
        {

            List<LocationLinkObj> deletedLinks = base.InternalDelete(keys);

            foreach (LocationLinkObj link in deletedLinks)
            {
                LocationObj AObj = Store.Locations.GetObjectByID(link.A, false);
                if (AObj != null)
                {
                    AObj.RemoveLink(link.B);
                }

                LocationObj BObj = Store.Locations.GetObjectByID(link.B, false);
                if (BObj != null)
                {
                    BObj.RemoveLink(link.A);
                }
            }

            return deletedLinks;
        }

        /// <summary>
        /// Add links from objects, no object update needed.  The object is being deleted.
        /// </summary>
        /// <param name="delObjs"></param>
        /// <returns></returns>
        protected List<LocationLinkObj> InternalDelete(IEnumerable<LocationObj> delObjs)
        {
            List<LocationLinkKey> links = new List<LocationLinkKey>();
            foreach (LocationObj obj in delObjs)
            {
                if (obj == null)
                    continue;

                foreach (long linkID in obj.LinksCopy)
                {
                    links.Add(new LocationLinkKey(obj.ID, linkID));
                }
            }

            return InternalDelete(links.ToArray());
        }

        #endregion


        /// <summary>
        /// Events from the location store are listened to because locations can arrive with Link information. 
        /// When that occurs we add those links to our own store.
        /// This is a symptom of passing location links with both location objects and location link objects.
        /// To make the code cleaner I could not pass the links with the locations, and query them directly instead.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnLocationsStoreChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    ChangeInventory<LocationLinkObj> inventory = InternalAdd(e.NewItems.Cast<LocationObj>());
                    CallOnCollectionChanged(inventory);
                    break;
                
                case NotifyCollectionChangedAction.Replace: 
                    // Debug.Assert(false, "Locations links are created or deleted, but never replaced...");
                    //TODO: We don't care about updates, but since links come with locations this means we will miss new links from the server
                    //unless we query for them directly,
                    InternalDelete(e.OldItems.Cast<LocationObj>());
                    InternalAdd(e.NewItems.Cast<LocationObj>());
                    break;
                 

                case NotifyCollectionChangedAction.Remove:
                    List<LocationLinkObj> listDeleted = InternalDelete(e.OldItems.Cast<LocationObj>());
                    CallOnCollectionChangedForDelete(listDeleted);
                    break;

                default:
                    Debug.Assert(false, "Unexpected change action in OnStoreAddRemoveKey");
                    break;
            }
        }

        protected override LocationLink[] ProxyGetBySectionRegionCallback(out long TicksAtQueryExecute, out LocationLinkKey[] DeletedLocations, GetObjectBySectionCallbackState<IAnnotateLocations, LocationLinkObj> state, IAsyncResult result)
        {
            throw new NotImplementedException();
        }
    }
}
