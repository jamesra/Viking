using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

using System.Diagnostics;
using System.ServiceModel;

using WebAnnotationModel.Service;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{
    public class LocationLinkStore : StoreBaseWithKey<AnnotateLocationsClient, IAnnotateLocations, LocationLinkKey, LocationLinkObj, LocationLink>
    {
        ConcurrentDictionary<long, ConcurrentDictionary<LocationLinkKey, LocationLinkObj>> SectionToLocationLinks = new ConcurrentDictionary<long, ConcurrentDictionary<LocationLinkKey, LocationLinkObj>>();

//        ConcurrentDictionary<long, ConcurrentDictionary<long,long>> LocIDtoLinks = new ConcurrentDictionary<long, ConcurrentDictionary<long, long>>();

        /// <summary>
        /// When we query the database for locations on a section we store the query time for the section
        /// That way on the next query we only need to store the updates.
        /// </summary>
//        private ConcurrentDictionary<long, DateTime> LastQueryForSection = new ConcurrentDictionary<long, DateTime>();

        /// <summary>
        /// A collection of values indicating which sections have an outstanding query. 
        /// The existence of a key indicates a query is in progress
        /// </summary>
//        private ConcurrentDictionary<long, bool> OutstandingSectionQueries = new ConcurrentDictionary<long, bool>();

        public LocationLinkStore()
        {
            
        }

        public override void Init()
        {
            Store.Locations.OnCollectionChanged += new NotifyCollectionChangedEventHandler(OnLocationsStoreChanged); 
        }

        protected override LocationLink ProxyGetByID(AnnotateLocationsClient proxy, LocationLinkKey ID)
        {
            throw new NotImplementedException();
        }

        protected override LocationLink[] ProxyGetByIDs(AnnotateLocationsClient proxy, LocationLinkKey[] IDs)
        {
            throw new NotImplementedException();
        }

        protected override AnnotateLocationsClient CreateProxy()
        {
            AnnotateLocationsClient proxy = new Service.AnnotateLocationsClient("Annotation.Service.Interfaces.IAnnotateLocations-Binary",
                State.EndpointAddress);
            proxy.ClientCredentials.UserName.UserName = State.UserCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = State.UserCredentials.Password;
            return proxy; 
        }

        protected override LocationLinkKey[] ProxyUpdate(AnnotateLocationsClient proxy, LocationLink[] objects)
        {
            throw new NotImplementedException();
        }
        
        public void CreateLink(long A,long B)
        {
            //lock (LockObject)
            {
                using (AnnotateLocationsClient proxy = CreateProxy())
                {
                    try
                    {
                        proxy.Open();
                        proxy.CreateLocationLink(A, B);
                    }
                    catch (Exception e)
                    {
                        //TODO: Better message
                        Trace.WriteLine("Error creating link between locations, link not created: " + e.Message);
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
                AnnotateLocationsClient proxy = null; 
                LocationLinkObj deletedLink = null;

//                LocationObj AObj = Store.Locations.GetObjectByID(A);
//                LocationObj BObj = Store.Locations.GetObjectByID(B);

                try
                {
                    proxy  = CreateProxy();
                    proxy.Open();
                    proxy.DeleteLocationLink(A, B);
                    
                    deletedLink = InternalDelete(new LocationLinkKey(A, B));
                }
                catch (Exception e)
                {
                    //TODO: Better Error message
                    Trace.WriteLine("Error deleting link between locations, link not created: " + e.Message);
                }
                finally
                {
                    if (proxy != null)
                        proxy.Close();
                }

                if(deletedLink != null)
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
            GetObjectsForSectionAsynch(SectionNumber);

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

        protected override LocationLink[] ProxyGetBySection(AnnotateLocationsClient proxy, long SectionNumber, DateTime LastQuery, out long TicksAtQueryExecute, out LocationLinkKey[] DeletedLinkKeys)
        {
            LocationLink[] deleted_links = null; 
            LocationLink[] links = proxy.LocationLinksForSection(out TicksAtQueryExecute, out deleted_links, SectionNumber, LastQuery.Ticks);

            if( deleted_links == null)
            {
                DeletedLinkKeys = new LocationLinkKey[0];
            }
            else 
            {
                DeletedLinkKeys = new LocationLinkKey[deleted_links.Length];
                for(int i = 0; i < deleted_links.Length; i++)
                {
                    DeletedLinkKeys[i] = new LocationLinkKey(deleted_links[i].SourceID, deleted_links[i].TargetID);
                }
            }

            return links; 
        }

        protected override IAsyncResult ProxyBeginGetBySection(AnnotateLocationsClient proxy, long SectionNumber, DateTime LastQuery, AsyncCallback callback, object asynchState)
        {
            
            return proxy.BeginLocationLinksForSection(SectionNumber,
                                                 LastQuery.Ticks,
                                                 GetObjectsBySectionCallback,
                                                 new GetObjectBySectionCallbackState(proxy, SectionNumber, LastQuery));
        }

        protected override LocationLink[] ProxyGetBySectionCallback(out long TicksAtQueryExecute,
                                                                    out LocationLinkKey[] DeletedKeys,
                                                                    GetObjectBySectionCallbackState state,
                                                                    IAsyncResult result)
        {
            LocationLink[] links;
            LocationLink[] DeletedLinks;
            links = state.Proxy.EndLocationLinksForSection(out TicksAtQueryExecute, out DeletedLinks, result);

            DeletedKeys = new LocationLinkKey[DeletedLinks.Length];
            for(int iLink = 0; iLink < DeletedLinks.Length; iLink++)
            {
                DeletedKeys[iLink] = new LocationLinkKey(DeletedLinks[iLink].SourceID, DeletedLinks[iLink].TargetID); 
            }

            return links;
        }

        /// <summary>
        /// We override this because if the Locations are not loaded the only information we have about which sections locationLinks belong to is with the query state
        /// </summary>
        /// <param name="locations"></param>
        /// <param name="state"></param>
        /// <param name="DeletedLocations"></param>
        protected override ChangeInventory<LocationLinkObj> ParseQuery(LocationLink[] newLinks,                                            
                                           LocationLinkKey[] DeletedLocations,
                                           GetObjectBySectionCallbackState state)
        {
            ConcurrentDictionary<LocationLinkKey, LocationLinkObj> SectionLocationLinks = new ConcurrentDictionary<LocationLinkKey, LocationLinkObj>(); 
            SectionLocationLinks = SectionToLocationLinks.GetOrAdd(state.SectionNumber, SectionLocationLinks);
            if (SectionLocationLinks != null)
            {
                if (DeletedLocations != null)
                {
                    foreach (LocationLinkKey key in DeletedLocations)
                    {
                        LocationLinkObj value; 
                        SectionLocationLinks.TryRemove(key, out value);
                    }

                    InternalDelete(DeletedLocations);
                }

                LocationLinkObj[] listNewObj = new LocationLinkObj[newLinks.Length];
                System.Threading.Tasks.Parallel.For(0, newLinks.Length, (iLink) =>
                {
                    listNewObj[iLink] = new LocationLinkObj(newLinks[iLink]);
                    SectionLocationLinks.TryAdd(new LocationLinkKey(listNewObj[iLink]), listNewObj[iLink]);
                });

                return InternalAdd(listNewObj);
            }

            return new ChangeInventory<LocationLinkObj>();
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
            foreach (LocationLinkKey link in keys)
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


            return base.InternalDelete(keys);
        }

        /// <summary>
        /// Add links from objects, no object update needed
        /// </summary>
        /// <param name="delObjs"></param>
        /// <returns></returns>
        protected List<LocationLinkObj> InternalDelete(IEnumerable<LocationObj> delObjs)
        {
            List<LocationLinkKey> links = new List<LocationLinkKey>();
            foreach (LocationObj obj in delObjs)
            {
                foreach (long linkID in obj.Links)
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
                /*
                case NotifyCollectionChangedAction.Replace:
                    // Debug.Assert(false, "Locations links are created or deleted, but never replaced...");
                    //TODO: We don't care about updates, but since links come with locations this means we will miss new links from the server
                    //unless we query for them directly,
                    InternalDelete(e.OldItems.Cast<LocationObj>());
                    InternalAdd(e.NewItems.Cast<LocationObj>());
                    break;
                 */

                case NotifyCollectionChangedAction.Remove:
                    List<LocationLinkObj> listDeleted = InternalDelete(e.OldItems.Cast<LocationObj>());
                    CallOnCollectionChangedForDelete(listDeleted); 
                    break;

                default:
                    Debug.Assert(false, "Unexpected change action in OnStoreAddRemoveKey");
                    break;
            }
        }
    }
}
