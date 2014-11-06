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
    /// <summary>
    /// 
    /// </summary>
    class LocationLinksViewModel : System.Windows.IWeakEventListener
    {
        /// <summary>
        /// This is a symptom of being halfway to the Jotunn architecture.  This is a pointer to the 
        /// parent section viewer control which can perform transforms
        /// </summary>
        public Viking.UI.Controls.SectionViewerControl parent;

        /// <summary>
        /// Allows us to describe all the locationlinks visible on a screen
        /// </summary>
        private ConcurrentDictionary<int, LineSearchGrid<LocationLink>> SectionLocationLinksSearch = new ConcurrentDictionary<int, LineSearchGrid<LocationLink>>();

        /// <summary>
        /// Keeps only one instance of a LocationLink for each LocationLinkKey value
        /// </summary>
        private ConcurrentDictionary<LocationLinkKey, LocationLink> LinkKeyToLinkView = new ConcurrentDictionary<LocationLinkKey, LocationLink>();

        /// <summary>
        /// We don't want to subscribe to LocationObjs multiple times or unsubscribe if they have other links.
        /// This structure records how many subscriptions we have
        /// </summary>
        private ConcurrentDictionary<long, long> LocationSubscriptionRefCounts = new ConcurrentDictionary<long, long>(); 
        


        public LocationLinksViewModel(Viking.UI.Controls.SectionViewerControl Parent)
        {
            this.parent = Parent;
    
            NotifyCollectionChangedEventManager.AddListener(Store.LocationLinks, this);
            NotifyCollectionChangedEventManager.AddListener(Store.Locations, this); 

        }

        public void LoadSection(int sectionNumber)
        {
    //        Store.LocationLinks.GetLinksCrossingSection(sectionNumber);
        }

        /// <summary>
        /// Return all the line segments visible in the passed bounds
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public LocationLink[] VisibleLocationLinks(int sectionNumber, GridRectangle bounds)
        {
            LineSearchGrid<LocationLink> searchGrid = GetSearchGrid(sectionNumber);

            if (null == searchGrid)
                return new LocationLink[0]; 
            
            LocationLink[] LinkList = searchGrid.GetValues(bounds);

            return LinkList.ToArray();
        }


        public LineSearchGrid<LocationLink> GetSearchGrid(int SectionNumber)
        {
            LineSearchGrid<LocationLink> searchGrid; 
            bool success = SectionLocationLinksSearch.TryGetValue(SectionNumber, out searchGrid); 
            if(success)
                return searchGrid;

            return null; 
        }

        public LineSearchGrid<LocationLink> GetOrAddSearchGrid(int SectionNumber, int EstimatedLinks)
        {
            LineSearchGrid<LocationLink> searchGrid; 
            bool success = SectionLocationLinksSearch.TryGetValue(SectionNumber, out searchGrid); 
            if(success)
                return searchGrid;

            searchGrid = new LineSearchGrid<LocationLink>(AnnotationOverlay.SectionBounds(parent, SectionNumber), EstimatedLinks);
            searchGrid = SectionLocationLinksSearch.GetOrAdd(SectionNumber, searchGrid);
            return searchGrid;
        }

        public bool TryRemoveSearchGrid(int SectionNumber)
        {
            LineSearchGrid<LocationLink> searchGrid;
            bool success = SectionLocationLinksSearch.TryRemove(SectionNumber, out searchGrid);
            return success; 
        }

        public IUIObjectBasic GetNearestLink(int SectionNumber, GridVector2 WorldPosition, out double distance)
        {
//            double minDistance = double.MaxValue;
            distance = double.MaxValue; 
            GridVector2 NearestIntersection;
//            IUIObjectBasic FoundLink = null;
            LineSearchGrid<LocationLink> searchGrid = GetSearchGrid(SectionNumber);

            if (searchGrid == null)
                return null; 

            LocationLink locLinkObj = searchGrid.GetNearest(WorldPosition, out NearestIntersection, out distance);
            if (locLinkObj != null)
            {
                if (distance > locLinkObj.Radius)
                {
                    locLinkObj = null;
                }
                else
                {
                    //minDistance = distance;
                    //FoundLink = locLinkObj as IUIObjectBasic;
                }
            }

            return locLinkObj; 

            /*
            StructureLink structLinkObj = StructureLinksSearch.GetNearest(WorldPosition, out NearestIntersection, out distance);
            if (structLinkObj != null && distance < minDistance)
            {
                if (distance <= structLinkObj.Radius && distance < minDistance)
                {
                    FoundLink = structLinkObj as IUIObjectBasic;
                    minDistance = distance;
                }
            }
            distance = minDistance;
            return FoundLink;
             */
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

                foreach (long linkID in loc.Links)
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
            
            if (AObj.VolumePosition.X < 0 && AObj.VolumePosition.Y < 0)
                return false;

            if (BObj.VolumePosition.X < 0 && BObj.VolumePosition.Y < 0)
                return false;

            if (AObj.VolumePosition == BObj.VolumePosition)
                return false;
             
            //LocationLinkKey key = new LocationLinkKey(link); 
            //LocationLink linkView = new LocationLink(AView, BView);
            LocationLink linkView;  
            linkView = LinkKeyToLinkView.GetOrAdd(key, (newLink) => { return new LocationLink(AObj, BObj); });
            bool success = AddLocationLinkToSectionSearchGrids(AObj, BObj, linkView); 

            if (success)
            {
                AddRefLocation(AObj);
                AddRefLocation(BObj);
            }

            return true; 
        }

        private bool AddLocationLinkToSectionSearchGrids(LocationObj AObj, LocationObj BObj, LocationLink linkView)
        {
            int minSection = AObj.Section < BObj.Section ? AObj.Section : BObj.Section;
            int maxSection = AObj.Section < BObj.Section ? BObj.Section : AObj.Section;

            GridLineSegment lineSegment = new GridLineSegment(AObj.VolumePosition, BObj.VolumePosition);

            bool success = false;
            //Add a grid line segment to each section the link intersects
            for (int iSection = minSection; iSection <= maxSection; iSection++)
            {
                //TODO: Check for missing sections!
                if (parent.Section.VolumeViewModel.SectionViewModels.ContainsKey(iSection) == false)
                    continue;

                //int EstimatedLinks = Store.Locations.GetObjectsForSection(iSection).Count;
                //if (EstimatedLinks < 2000)
                int EstimatedLinks = 2500;

                LineSearchGrid<LocationLink> searchGrid = GetOrAddSearchGrid(iSection, EstimatedLinks);
                //           Debug.WriteLine(iSection.ToString() + " add    : " + linkView.ToString() + " " + searchGrid.Count.ToString());

                //Debug.Assert(false == searchGrid.Contains(linkView));
                bool sectionSuccess = searchGrid.TryAdd(lineSegment, linkView);
                success = success || sectionSuccess;  //I had this on one line, but short-circuit logic had me beating my head against the wall for too long
                //Debug.Assert(success); 
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
            LocationLink linkView;
            bool success = LinkKeyToLinkView.TryRemove(key, out linkView);
            if(false == success)
                return; 
            
            success = RemoveLocationLinkFromSectionSearchGrids(linkView);

            if (success)
            {

                LocationObj AObj = linkView.A;
                LocationObj BObj = linkView.B;

                ReleaseRefLocation(AObj);
                ReleaseRefLocation(BObj);
            }
        }

        private bool RemoveLocationLinkFromSectionSearchGrids(LocationLink linkView)
        {
            bool success = false;
            for (int iSection = linkView.minSection; iSection <= linkView.maxSection; iSection++)
            {
                if (parent.Section.VolumeViewModel.SectionViewModels.ContainsKey(iSection) == false)
                    continue;

                //        Debug.WriteLine(iSection.ToString() + " remove : " + linkView.ToString());
                LineSearchGrid<LocationLink> searchGrid = GetSearchGrid(iSection);

                if (searchGrid == null)
                    continue;

                GridLineSegment line;
                bool sectionSuccess = searchGrid.TryRemove(linkView, out line);
                Debug.Assert(sectionSuccess);
                success = success || sectionSuccess;

                //Free all the memory for the search grid if this was the last location link
                if (searchGrid.Count == 0)
                {
                    TryRemoveSearchGrid(iSection);
                }
            }

            return success; 
        }

#endregion

        #region ref counting locations
        private long AddRefLocation(LocationObj loc)
        {
            long refCount = LocationSubscriptionRefCounts.AddOrUpdate(loc.ID, 1, (id, oldValue) => oldValue + 1);
            if (refCount == 1)
            {
                NotifyPropertyChangedEventManager.AddListener(loc, this);
                NotifyCollectionChangedEventManager.AddListener(loc.Links, this); 
            }

            return refCount; 
        }

        private long ReleaseRefLocation(LocationObj loc)
        {
            long refCount = LocationSubscriptionRefCounts.AddOrUpdate(loc.ID, 1, (id, oldValue) => oldValue - 1);
            if (refCount == 0)
            {
                NotifyPropertyChangedEventManager.RemoveListener(loc, this);
                NotifyCollectionChangedEventManager.RemoveListener(loc.Links, this);
            }

            return refCount;
        }
        #endregion

        #region Events

        private void OnLinkedLocationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            LocationObj loc = sender as LocationObj;
            if (loc == null)
                return;

            //Update if a position or everything has changed
            if (e.PropertyName.Contains("VolumePosition") || e.PropertyName.Contains("Position") || String.IsNullOrEmpty(e.PropertyName))
            {
//                Trace.WriteLine("Linked Location property changed: " + loc.ToString() + " property: " + e.PropertyName);

                foreach (long linkID in loc.Links)
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

            parent.Invalidate(); 
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

            parent.Invalidate(); 
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
     
}
