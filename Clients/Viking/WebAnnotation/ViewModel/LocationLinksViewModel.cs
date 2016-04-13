using System;
using System.Diagnostics;
using System.ComponentModel; 
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using WebAnnotation;
using Viking;
using Viking.Common;
using Geometry;
using WebAnnotation.UI;
using Viking.ViewModels;
using WebAnnotationModel; 

namespace WebAnnotation.ViewModel
{
    /*
    /// <summary>
    /// 
    /// </summary>
    class LocationLinksViewModel : System.Windows.IWeakEventListener
    {
        /// <summary>
        /// Allows us to describe all the locationlinks visible on a screen
        /// </summary>
        private ConcurrentDictionary<int, RTree.RTree<LocationLinkView>> SectionLocationLinksSearch = new ConcurrentDictionary<int, RTree.RTree<LocationLinkView>>();

        /// <summary>
        /// Allows us to describe all the locationlinks visible on a screen
        /// </summary>
        private ConcurrentDictionary<int, KeyTracker<LocationLinkKey>> SectionOverlappedLinksSearch = new ConcurrentDictionary<int, KeyTracker<LocationLinkKey>>();

        /// <summary>
        /// Keeps only one instance of a LocationLink for each LocationLinkKey value
        /// </summary>
        private ConcurrentDictionary<LocationLinkKey, LocationLinkView> LinkKeyToLinkView = new ConcurrentDictionary<LocationLinkKey, LocationLinkView>();

        /// <summary>
        /// We don't want to subscribe to LocationObjs multiple times or unsubscribe if they have other links.
        /// This structure records how many subscriptions we have
        /// </summary>
        private RefCountingKeyTracker<long> LocationSubscriptions = new RefCountingKeyTracker<long>(); 
          
        public LocationLinksViewModel()
        {
            NotifyCollectionChangedEventManager.AddListener(Store.LocationLinks, this);
            NotifyCollectionChangedEventManager.AddListener(Store.Locations, this); 
        }
        
        /// <summary>
        /// Return all the line segments visible in the passed bounds
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public IEnumerable<LocationLinkView> VisibleLocationLinks(int sectionNumber, GridRectangle bounds)
        {
            RTree.RTree<LocationLinkView> searchGrid = GetSearchGrid(sectionNumber);

            if (null == searchGrid)
                return new LocationLinkView[0];

            return searchGrid.Intersects(bounds.ToRTreeRect(sectionNumber));
        }


        public RTree.RTree<LocationLinkView> GetSearchGrid(int SectionNumber)
        {
            RTree.RTree<LocationLinkView> searchGrid; 
            bool success = SectionLocationLinksSearch.TryGetValue(SectionNumber, out searchGrid); 
            if(success)
                return searchGrid;

            return null; 
        }

        public RTree.RTree<LocationLinkView> GetOrAddSearchGrid(int SectionNumber)
        {
            return SectionLocationLinksSearch.GetOrAdd(SectionNumber, (sn) => { return new RTree.RTree<LocationLinkView>(); });
        }

        public bool TryRemoveSearchGrid(int SectionNumber)
        {
            RTree.RTree<LocationLinkView> searchGrid;
            bool success = SectionLocationLinksSearch.TryRemove(SectionNumber, out searchGrid);
            return success; 
        }

        public IUIObjectBasic GetNearestLink(int SectionNumber, GridVector2 WorldPosition, out double distance)
        {
            distance = double.MaxValue; 
            RTree.RTree<LocationLinkView> searchGrid = GetSearchGrid(SectionNumber);

            if (searchGrid == null)
                return null; 

//            IEnumerable<LocationLink> intersectingObjs = searchGrid.Intersects(WorldPosition.ToRTreeRect(SectionNumber)).Where(l => l.LineSegment.DistanceToPoint(WorldPosition) <= l.Radius).ToList();
            List<LocationLinkView> intersecting_candidates = searchGrid.Intersects(WorldPosition.ToRTreeRect(SectionNumber)).Where(l => l.Intersects(WorldPosition)).ToList();
            LocationLinkView nearest = intersecting_candidates.OrderBy(l => l.DistanceFromCenterNormalized(WorldPosition)).FirstOrDefault();
            if (nearest != null)
            {
                distance = nearest.LineSegment.DistanceToPoint(WorldPosition);
                return nearest;
            }

            return null;
        }

#region Add/Remove Location Links

        /// <summary>
        /// All locations which are linked get a line between them
        /// </summary>
        internal void AddLocationLinks(IEnumerable<LocationLinkObj> links)
        { 
            // Add links to each section they intersect
            foreach (LocationLinkObj link in links)
            { 
                AddLocationLink(new LocationLinkKey(link)); 
            }
        }

        /// <summary>
        /// All locations which are linked get a line between them
        /// </summary>
        internal void AddLocationLinks(IEnumerable<LocationObj> locs)
        {
            // Add links to each section they intersect
            foreach (LocationObj loc in locs)
            {
                if (loc.VolumePosition.X < 0 ||
                   loc.VolumePosition.Y < 0)
                    continue; 

                foreach (long linkID in loc.LinksCopy)
                {
                    AddLocationLink(new LocationLinkKey(loc.ID, linkID));
                }
            }
        }
         

        private bool AddLocationLink(LocationLinkKey key)
        {
            //Check if we know about this key already
            if(LinkKeyToLinkView.ContainsKey(key))
            {
                return false; 
            }

            //Trace.WriteLine("Add Link " + key.A.ToString() + " -> " + key.B.ToString());
            LocationObj AObj = Store.Locations.GetObjectByID(key.A, false);
            LocationObj BObj = Store.Locations.GetObjectByID(key.B, false);

            if (AObj == null)
                return false;

            if (BObj == null)
                return false;            
            
            if (!(AObj.VolumePositionHasBeenCalculated && BObj.VolumePositionHasBeenCalculated))
                return false;

            if (AObj.VolumePosition == BObj.VolumePosition)
                return false;
             
            LocationLinkView linkView = LinkKeyToLinkView.GetOrAdd(key, link => {
                AddRefLocation(AObj);
                AddRefLocation(BObj);
                return new LocationLinkView(AObj, BObj, Viking.UI.State.volume);
            });

            AddLocationLinkToSectionSearchGrids(AObj, BObj, linkView); 
            
            return true; 
        }

        private bool AddLocationLinkToSectionSearchGrids(LocationObj AObj, LocationObj BObj, LocationLinkView linkView)
        {
            int minSection = linkView.MinSection;
            int maxSection = linkView.MaxSection;

            GridLineSegment lineSegment = new GridLineSegment(AObj.VolumePosition, BObj.VolumePosition);

            bool success = false;
            //Add a grid line segment to each section the link intersects
            for (int iSection = minSection; iSection <= maxSection; iSection++)
            {
                //TODO: Check for missing sections!
                if (Viking.UI.State.volume.SectionViewModels.ContainsKey(iSection) == false)
                    continue;

                //Do not bother mapping location links which are covered by overlapping locations
                if (!linkView.LinksOverlap(iSection))
                {
                    RTree.RTree<LocationLinkView> searchGrid = GetOrAddSearchGrid(iSection);
                    //           Debug.WriteLine(iSection.ToString() + " add    : " + linkView.ToString() + " " + searchGrid.Count.ToString());

                    //Debug.Assert(false == searchGrid.Contains(linkView));

                    bool sectionSuccess = searchGrid.TryAdd(linkView.BoundingBox.ToRTreeRect(iSection), linkView);
                    success |= sectionSuccess;  //I had this on one line, but short-circuit logic had me beating my head against the wall for too long
                                                          //Debug.Assert(success); 
                }
                else
                {
                    KeyTracker<LocationLinkKey> OverlappedSet = SectionOverlappedLinksSearch.GetOrAdd(iSection, (sn) => { return new KeyTracker<LocationLinkKey>(); });
                    OverlappedSet.TryAdd(linkView.Key);
                }
            }

            return success; 
        }

        /// <summary>
        /// Remove all lines to the passed locations
        /// </summary>
        internal void RemoveLocationLinks(IEnumerable<LocationLinkObj> links)
        {
            foreach (LocationLinkObj link in links)
            {
                RemoveLocationLinks(new LocationLinkKey(link) );
            }
        }

        /// <summary>
        /// All locations which are linked get a line between them
        /// </summary>
        internal void RemoveLocationLinks(IEnumerable<LocationObj> locs)
        {
            // Add links to each section they intersect
            foreach (LocationObj loc in locs)
            {
                foreach (long linkID in loc.Links)
                {
                    RemoveLocationLinks(new LocationLinkKey(loc.ID, linkID));
                }
            }
        }

        internal void RemoveLocationLinks(LocationLinkKey key)
        {
            //LocationLinkKey key = new LocationLinkKey(link); 
            LocationLinkView linkView;
            bool success = LinkKeyToLinkView.TryRemove(key, out linkView);
            if(false == success)
                return; 
            
            success = RemoveLocationLinkFromSectionSearchGrids(linkView);

            LocationObj AObj = Store.Locations[linkView.Key.A];
            LocationObj BObj = Store.Locations[linkView.Key.B];

            ReleaseRefLocation(AObj);
            ReleaseRefLocation(BObj);
        }

        private bool RemoveLocationLinkFromSectionSearchGrids(LocationLinkView linkView)
        {
            bool success = false;
            for (int iSection = linkView.MinSection; iSection <= linkView.MaxSection; iSection++)
            {
                //        Debug.WriteLine(iSection.ToString() + " remove : " + linkView.ToString());
                bool sectionSuccess = RemoveNonOverlappedLocationLinkFromSection(iSection, linkView) || RemoveOverlappedLocationLinkFromSection(iSection, linkView.Key);
                success = success || sectionSuccess;
            }

            return success; 
        }

        private bool RemoveNonOverlappedLocationLinkFromSection(int SectionNumber, LocationLinkView linkView)
        {
            RTree.RTree<LocationLinkView> searchGrid = GetSearchGrid(SectionNumber);

            if (searchGrid == null)
                return false;

            LocationLinkView line;
            bool nonOverlappedRemoved = searchGrid.Delete(linkView, out line);
            if (nonOverlappedRemoved && searchGrid.Count == 0)
            {
                TryRemoveSearchGrid(SectionNumber);
            }
            
            return nonOverlappedRemoved;            
        }

        private bool RemoveOverlappedLocationLinkFromSection(int SectionNumber, LocationLinkKey key)
        {
            KeyTracker<LocationLinkKey> OverlappedLinks;
            if (this.SectionOverlappedLinksSearch.TryGetValue(SectionNumber, out OverlappedLinks))
            {
                bool Removed = OverlappedLinks.TryRemove(key);
            }

            return false;
        }

#endregion

        #region ref counting locations

        private static void SubscribeToLocationChangeEvents(LocationObj loc, System.Windows.IWeakEventListener listener)
        {
            NotifyPropertyChangedEventManager.AddListener(loc, listener);
            NotifyCollectionChangedEventManager.AddListener(loc.Links, listener);
        }

        private static void UnsubscribeToLocationChangeEvents(LocationObj loc, System.Windows.IWeakEventListener listener)
        {
            NotifyPropertyChangedEventManager.RemoveListener(loc, listener);
            NotifyCollectionChangedEventManager.RemoveListener(loc.Links, listener);
        }

        private void AddRefLocation(LocationObj loc)
        {
            LocationSubscriptions.AddRef(loc.ID, l => SubscribeToLocationChangeEvents(loc, this));
        }

        private void ReleaseRefLocation(LocationObj loc)
        {
            LocationSubscriptions.ReleaseRef(loc.ID, l => UnsubscribeToLocationChangeEvents(loc, this));
        }

        #endregion

        #region Events

        private void OnLinkedLocationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            LocationObj loc = sender as LocationObj;
            if (loc == null)
                return;

            //Update if a position or everything has changed
            if (LocationObj.IsGeometryProperty(e.PropertyName))
            {
//                Trace.WriteLine("Linked Location property changed: " + loc.ToString() + " property: " + e.PropertyName);

                foreach (long linkID in loc.LinksCopy)
                {
                    LocationLinkKey key = new LocationLinkKey(loc.ID, linkID);
                    RemoveLocationLinks(key);
                    AddLocationLink(key); 
                }
            }
        }

        
        //Called when a key is added or removed from the store
        public void OnLocationLinksStoreChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    AddLocationLinks(e.NewItems.Cast<LocationLinkObj>());
                    break;

                case NotifyCollectionChangedAction.Replace:
                   // Debug.Assert(false, "Locations links are created or deleted, but never replaced...");

                   // RemoveLocationLinks(e.OldItems.Cast<LocationLinkObj>());
                   // AddLocationLinks(e.NewItems.Cast<LocationLinkObj>());
                    break;

                case NotifyCollectionChangedAction.Remove:
                    RemoveLocationLinks(e.OldItems.Cast<LocationLinkObj>());
                    break;

                default:
                    Debug.Assert(false, "Unexpected change action in OnStoreAddRemoveKey");
                    break;
            }
        }
        
        
        //Called when a key is added or removed from the store
        public void OnLocationsStoreChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    AddLocationLinks(e.NewItems.Cast<LocationObj>());
                    break;

                case NotifyCollectionChangedAction.Replace:
                    // Debug.Assert(false, "Locations links are created or deleted, but never replaced...");
                    RemoveLocationLinks(e.OldItems.Cast<LocationObj>());
                    AddLocationLinks(e.NewItems.Cast<LocationObj>());
                    break;

                case NotifyCollectionChangedAction.Remove:
                    RemoveLocationLinks(e.OldItems.Cast<LocationObj>());
                    break;

                default:
                    Debug.Assert(false, "Unexpected change action in OnStoreAddRemoveKey");
                    break;
            }
        }
        

        public void OnLocationLinksChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            LocationObj source = sender as LocationObj;
            if (source == null)
                return; 

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:                   

                    AddLocationLinks(e.NewItems.Cast<LocationObj>());
                    break;

                case NotifyCollectionChangedAction.Remove:

                    RemoveLocationLinks(e.OldItems.Cast<LocationObj>());
                    break;
            }
        }

        #endregion


        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            if (sender == null)
                throw new ArgumentNullException("sender"); 

            System.Collections.Specialized.NotifyCollectionChangedEventArgs CollectionChangeArgs = e as System.Collections.Specialized.NotifyCollectionChangedEventArgs;
            if (CollectionChangeArgs != null)
            {
                Type senderType = sender.GetType();
                if (senderType == typeof(LocationStore))
                {
                    this.OnLocationsStoreChanged(sender, CollectionChangeArgs);
                    return true;
                }
                else if (senderType == typeof(LocationLinkStore))
                {
                    this.OnLocationLinksStoreChanged(sender, CollectionChangeArgs);
                    return true;
                }
                else
                {
                    this.OnLocationLinksChanged(sender, CollectionChangeArgs);
                    return true;
                }
            }

            PropertyChangedEventArgs PropertyChangedArgs = e as PropertyChangedEventArgs;
            if (PropertyChangedArgs != null)
            {
                if (sender.GetType() == typeof(LocationObj))
                {
                    OnLinkedLocationPropertyChanged(sender, PropertyChangedArgs);
                    return true;
                }
            }

            Debug.Fail("Weak Event not handled");
            return false;
        }
    }
     */
}
