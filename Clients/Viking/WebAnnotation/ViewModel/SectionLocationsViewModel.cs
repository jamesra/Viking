
#define SUBMITVOLUMEPOSITION

using System;
using System.Diagnostics; 
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Viking;
using Viking.Common;
using Geometry;
using WebAnnotation.UI;
using Viking.ViewModels;
using WebAnnotationModel;
using System.ComponentModel; 
using System.Threading.Tasks;


namespace WebAnnotation.ViewModel
{
    public class SectionLocationsViewModel : System.Windows.IWeakEventListener, IDisposable
    {
        /// <summary>
        /// The section we store annotations for
        /// <summary>
        public readonly SectionViewModel Section;

        /// <summary>
        /// Set to true if the annotations have been requested for this section
        /// </summary>
        public bool HaveLoadedSectionAnnotations = false;

        /// <summary>
        /// Locations on the section we are providing an overlay for
        /// implemented as a quad tree which can map a point to the nearest Location
        /// </summary>
        private QuadTree<Location_CanvasViewModel> Locations = null;
       
        /// <summary>
        /// Maps a structureID to all the locations for that structure on the visible section
        /// </summary>
        private ConcurrentDictionary<long, ConcurrentDictionary<long, Location_CanvasViewModel>> LocationsForStructure = new ConcurrentDictionary<long, ConcurrentDictionary<long, Location_CanvasViewModel>>();
        
        //        public static SortedDictionary<long, SortedList<long, RoundLineCode.RoundLine> > LocationLinesDict = null;

        /// <summary>
        /// Allows us to describe all the locationlinks visible on a screen
        /// </summary>
        //private LineSearchGrid<LocationLink> LocationLinksSearch = null;

        /// <summary>
        /// Allows us to describe all the StructureLinks visible on a screen
        /// </summary>
        private LineSearchGrid<StructureLink> StructureLinksSearch = null;

        /// <summary>
        /// This is a symptom of being halfway to the Jotunn architecture.  This is a pointer to the 
        /// parent section viewer control which can perform transforms
        /// </summary>
        public readonly Viking.UI.Controls.SectionViewerControl parent;

        public SectionLocationsViewModel(SectionViewModel section,  Viking.UI.Controls.SectionViewerControl Parent)
        {
            this.parent = Parent;
            Trace.WriteLine("Create SectionLocationsViewModel for " + section.Number.ToString());
            this.Section = section;

            GridRectangle bounds = AnnotationOverlay.SectionBounds(parent, parent.Section.Number);

            if (Locations == null)
                Locations = new QuadTree<Location_CanvasViewModel>(bounds);

            LocationsForStructure = new ConcurrentDictionary<long, ConcurrentDictionary<long, Location_CanvasViewModel>>();
            
            StructureLinksSearch = new LineSearchGrid<StructureLink>(bounds, 10000);
            
            CollectionChangedEventManager.AddListener(Store.Structures, this);
            CollectionChangedEventManager.AddListener(Store.StructureLinks, this);
        }

        #region Location Property Changes

        protected void OnLocationPropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            LocationObj loc = sender as LocationObj;
            if (loc == null)
                return;

//            Trace.WriteLine("Location property changing: " + loc.ToString() + " property: " + e.PropertyName); 

            //Update if a position or everything has changed
            if (e.PropertyName.Contains("Position") || e.PropertyName.Contains("WorldPosition") || string.IsNullOrEmpty(e.PropertyName))
            {
//                Location locView = new Location(loc);
                RemoveStructureLinks(new LocationObj[]{loc});
                RemoveLocations(new LocationObj[] { loc }, false);
            }
        }

        protected void OnLocationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            LocationObj loc = sender as LocationObj;
            if (loc == null)
                return;

//            Trace.WriteLine("Location property changed: " + loc.ToString() + " property: " + e.PropertyName); 

            

            //Update if a position or everything has changed
            if (e.PropertyName.Contains("Position") || e.PropertyName.Contains("WorldPosition") || string.IsNullOrEmpty(e.PropertyName))
            {
               /* Location locView = new Location(loc);
                if (e.PropertyName == "")
                {
                    MapLocation(loc); 
                }
                */
                //RemoveLocations(new LocationObj[] { loc }, true);
                //RemoveStructureLinks(new LocationObj[] { loc }); 
                loc.ResetVolumePositionHasBeenCalculated();
                AddLocations(new LocationObj[] { loc }, false);
                AddStructureLinks(new LocationObj[] { loc });
      //          bool Success = Locations.TryAdd(locView.VolumePosition, locView);
            }
        }

        protected void OnLinkedLocationPropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            LocationObj loc = sender as LocationObj;
            if (loc == null)
                return;

//            Trace.WriteLine("Linked Location property changing: " + loc.ToString() + " property: " + e.PropertyName);

            //Update if a position or everything has changed
            if (e.PropertyName.Contains('X') || e.PropertyName.Contains('Y') || string.IsNullOrEmpty(e.PropertyName))
            {
                /*
                Location locView = new Location(loc);
                foreach (long linkedID in loc.Links)
                {

                }
                bool Success = Locations.TryRemove(locView);
                //      Debug.Assert(Success); 
                */
            }
        }

        /*
        protected void OnLinkedLocationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            LocationObj loc = sender as LocationObj;
            if (loc == null)
                return;

            Trace.WriteLine("Linked Location property changed: " + loc.ToString() + " property: " + e.PropertyName);

            //Update if a position or everything has changed
            if (e.PropertyName.Contains('X') || e.PropertyName.Contains('Y') || e.PropertyName == "")
            { 
                List<LocationObj> listLocs = new List<LocationObj>();
                listLocs.Add(loc);
                RemoveLocationLinks(listLocs);
                AddLocationLinks(listLocs); 
            }
        }
        */

        #endregion 

        #region Cache updates

        //Called when a key is added or removed from the store
        public void OnLocationsStoreChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            IEnumerable<LocationObj> listNewObjs; 
            IEnumerable<LocationObj> listOldObjs;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    listNewObjs = e.NewItems.Cast<LocationObj>();
                    AddLocations(listNewObjs);
                    AddStructureLinks(listNewObjs);

                    //foreach (LocationObj locObj in listNewObjs)
                    //{
                    //    locObj.PropertyChanging += this.OnLocationPropertyChanging;
                    //    locObj.PropertyChanged += this.OnLocationPropertyChanged;
                    //}

                    break;
                case NotifyCollectionChangedAction.Replace:
                    RemoveStructureLinks(e.OldItems.Cast<LocationObj>()); 
                    RemoveLocations(e.OldItems.Cast<LocationObj>());
                    AddLocations(e.NewItems.Cast<LocationObj>());
                    AddStructureLinks(e.NewItems.Cast<LocationObj>());
                    break;

                case NotifyCollectionChangedAction.Remove:
                    listOldObjs = e.OldItems.Cast<LocationObj>();
                    RemoveStructureLinks(listOldObjs);
                    RemoveLocations(listOldObjs);

                    //foreach (LocationObj locObj in listOldObjs)
                    //{
                    //    locObj.PropertyChanging -= this.OnLocationPropertyChanging;
                    //    locObj.PropertyChanged -= this.OnLocationPropertyChanged;
                    //}
                    break;

                default:
                    Debug.Assert(false, "Unexpected change action in OnStoreAddRemoveKey");
                    break;
            }
        }
        
        //Called when a key is added or removed from the store
        public void OnStructuresStoreChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    AddStructureLinks(e.NewItems.Cast<StructureObj>());
                    break;

                case NotifyCollectionChangedAction.Replace:
                    RemoveStructureLinks(e.OldItems.Cast<StructureObj>());
                    AddStructureLinks(e.NewItems.Cast<StructureObj>());
                    break;

                case NotifyCollectionChangedAction.Remove:
                    RemoveStructureLinks(e.OldItems.Cast<StructureObj>());
                    break;

                default:
                    Debug.Assert(false, "Unexpected change action in OnStoreAddRemoveKey");
                    break;
            }
        }

        //Called when a key is added or removed from the store
        public void OnStructureLinksStoreChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    AddStructureLinks(e.NewItems.Cast<StructureLinkObj>());
                    break;

                case NotifyCollectionChangedAction.Replace:
                    RemoveStructureLinks(e.OldItems.Cast<StructureLinkObj>());
                    AddStructureLinks(e.NewItems.Cast<StructureLinkObj>());
                    break;

                case NotifyCollectionChangedAction.Remove:
                    RemoveStructureLinks(e.OldItems.Cast<StructureLinkObj>());
                    break;

                default:
                    Debug.Assert(false, "Unexpected change action in OnStoreAddRemoveKey");
                    break;
            }
        }

        private bool MapLocation(LocationObj loc)
        {
            GridVector2 VolumePosition = new GridVector2(-1, -1);
            //Don't bother mapping if the location was already mapped
            if (loc.VolumeTransformID == parent.CurrentTransformUniqueID)
                return true; 

            bool mappedPosition = parent.TrySectionToVolume(loc.Position, this.Section.section, out VolumePosition);
            if (!mappedPosition) //Remove locations we can't map
            {
                Trace.WriteLine("AddLocation: Location #" + loc.ID.ToString() + " was unmappable.", "WebAnnotation");
                return false;
            }

            loc.VolumePosition = VolumePosition;
            loc.VolumeTransformID = parent.CurrentTransformUniqueID;

            return true;
        }

        protected void AddLocations(IEnumerable<LocationObj> listLocations)
        {
            AddLocations(listLocations, true); 
        }

        /// <summary>   
        ///  Keys have been added to the locations store
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void AddLocations(IEnumerable<LocationObj> listLocations, bool Subscribe)
        {
#if SUBMITVOLUMEPOSITION
            bool UpdateVolumeLocations = false;
            bool SubmitUpdatedVolumeLocations = false;
            long VolumePositionUpdatedCount = 0; 

            
            if (this.parent.CurrentVolumeTransform == this.Section.VolumeViewModel.DefaultVolumeTransform)
            {
                UpdateVolumeLocations = true;
            }
#endif
                        
            foreach(LocationObj loc in listLocations)
            {

                if(AddLocation(loc, Subscribe, UpdateVolumeLocations))
                {
                    VolumePositionUpdatedCount++; 
                    SubmitUpdatedVolumeLocations |= true; 
                }
                //Add the location to our mapping if the location is on our section
                
                   
                   // Debug.Assert(Added);

                //    AddLocationLinks(locView);
                
            }
#if SUBMITVOLUMEPOSITION
            if (SubmitUpdatedVolumeLocations)
            {
                //System.Threading.ThreadPool.QueueUserWorkItem( f => { Store.Locations.Save(); } );

                //System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Func<bool>(Store.Locations.Save), System.Windows.Threading.DispatcherPriority.Background, null);

                Trace.WriteLine("Updated " + VolumePositionUpdatedCount.ToString() + " volume positions");
                Store.Locations.Save(); 
            }
#endif
        }
        
        /// <summary>
        /// Add a location to the view model. 
        /// </summary>
        /// <param name="loc">Location to add</param>
        /// <param name="Subscribe">Subscribe to the location's change events</param>
        /// <param name="UpdateVolumeLocations">Return True if the volume position of the location was updated</param>
        private bool AddLocation(LocationObj loc, bool Subscribe, bool UpdateVolumeLocations)
        {
            if (loc.Section != Section.Number)
                return false; 

             //Trace.WriteLine("AddLocation: " + obj.ToString(), "WebAnnotation");

            bool FirstMapping = !loc.VolumePositionHasBeenCalculated;
            bool UpdatedVolumeLocation = false;
            GridVector2 original = loc.VolumePosition;
            bool mapped = MapLocation(loc);
            if (!mapped)
                return false;

#if SUBMITVOLUMEPOSITION
            if (UpdateVolumeLocations && FirstMapping)
            {
                if (GridVector2.DistanceSquared(original, loc.VolumePosition) > 225.0)
                {
                    loc.SubmitOnNextUpdate();
                    UpdatedVolumeLocation = true;
                }
            }
#endif

            //Add location if it hasn't been seen before
            Location_CanvasViewModel locView = new Location_CanvasViewModel(loc);
            bool Added = Locations.TryAdd(locView.VolumePosition, locView);

            if (Added)
            {            
                if (Subscribe)
                {
                    locView.RegisterForLocationEvents();
                    SubscribeToLocationChangeEvents(loc);
                }

                ConcurrentDictionary<long, Location_CanvasViewModel> KnownLocationsForStructure;
                KnownLocationsForStructure = LocationsForStructure.GetOrAdd(loc.ParentID.Value, (key) => { return new ConcurrentDictionary<long, Location_CanvasViewModel>(); });
                KnownLocationsForStructure.TryAdd(locView.ID, locView);
            }

            return UpdatedVolumeLocation;
        }

        private void SubscribeToLocationChangeEvents(LocationObj loc)
        {
            NotifyPropertyChangingEventManager.AddListener(loc, this);
            NotifyPropertyChangedEventManager.AddListener(loc, this);
        }

        private void UnsubscribeToLocationChangeEvents(LocationObj loc)
        {                
           NotifyPropertyChangingEventManager.RemoveListener(loc, this);
           NotifyPropertyChangedEventManager.RemoveListener(loc, this); 
        }

        protected void RemoveLocations(IEnumerable<LocationObj> listLocations)
        {
            RemoveLocations(listLocations, true); 
        }

        /// <summary>
        /// A key is about to be removed from the location store.  Remove it from our cache as well
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void RemoveLocations(IEnumerable<LocationObj> listLocations, bool Unsubscribe)
        {
            foreach (LocationObj loc in listLocations)
            {
                //Trace.WriteLine("RemoveLocation: " + loc.ID.ToString(), "WebAnnotationViewModel");
                //Debug.Assert(loc.Section == Section.Number); 
                if (loc.Section == Section.Number)
                {
                    Location_CanvasViewModel locView = new Location_CanvasViewModel(loc);
                    Location_CanvasViewModel RemovedValue; 

                    bool RemoveSuccess = Locations.TryRemove(locView, out RemovedValue);
                    if (RemoveSuccess)
                    {
                        RemovedValue.DeregisterForLocationEvents();

                        if (Unsubscribe)
                        {
                            UnsubscribeToLocationChangeEvents(loc);
                        }
//                        Debug.Assert(RemoveSuccess); 
                       // RemoveLocationLinks(obj);
                    }
                }
                
                ConcurrentDictionary<long, Location_CanvasViewModel> KnownLocationsForStructure = null;
                bool Success = LocationsForStructure.TryGetValue(loc.ParentID.Value, out  KnownLocationsForStructure);
                if (Success)
                {
                    Location_CanvasViewModel removedLoc;
                    Success = KnownLocationsForStructure.TryRemove(loc.ID, out removedLoc);

                    if (Success)
                    {
                        if (KnownLocationsForStructure.Count == 0)
                        {
                            LocationsForStructure.TryRemove(loc.ParentID.Value, out KnownLocationsForStructure); 
                        }
                        /*PORT: Not thread safe, This proabably all needs to be removed...

                        //Remove entry if it was the last location for that structure
                        if (KnownLocationsForStructure.Count == 0)
                        {
                            LocationsForStructure.TryUpdate(obj.ParentID.Value, null, KnownLocationsForStructure);
                            LocationsForStructure.Remove(obj.ParentID.Value);
                        }
                            */
                    }
                }

                //GridVector2 removeOutParam;
                //TransformedLocationPositionDict.TryRemove(ID, out removeOutParam);

             //   Trace.WriteLine("End RemoveLocation: " + loc.ID.ToString(), "WebAnnotationViewModel");
            }

        /*
            if (AnnotationChanged != null && loc != null)
                AnnotationChanged(obj, new EventArgs());
        */
            
        }

        #endregion

        #region Queries

        public ICollection<Location_CanvasViewModel> GetLocations()
        {
            return Locations.Values;
        }

        public ICollection<Location_CanvasViewModel> GetLocations(GridRectangle bounds)
        {
            List<GridVector2> foundPoints; 
            List<Location_CanvasViewModel> foundLocations;
            Locations.Intersect(bounds, out foundPoints, out foundLocations);

            return foundLocations; 
        }

        /*
        public LocationObj[] GetReferenceLocations()
        {
            List<LocationObj> listRefLocations = new List<LocationObj>();

            if (Section.ReferenceSectionAbove != null)
            {
                listRefLocations.AddRange(Store.Locations.GetLocalObjectsForSection(Section.ReferenceSectionAbove.Number).Values);
            }

            if (Section.ReferenceSectionBelow != null)
            {
                listRefLocations.AddRange(Store.Locations.GetLocalObjectsForSection(Section.ReferenceSectionBelow.Number).Values);
            }

            return listRefLocations.ToArray();
        }
         */

        /// <summary>
        /// Returns the position of the requested locationID in the current transform
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        public bool TryGetPositionForLocation(Location_CanvasViewModel loc, out GridVector2 position)
        {
            return Locations.TryGetPosition(loc, out position);
            //return Locations.TryGetPosition(loc); 

            /*
            if (!Success)
            {
                //Hmm... why don't we have it?
                Trace.WriteLine("Could not find position for location: " + ID.ToString());

            }

            return position;
             */
        }

        public GridVector2 GetPositionForLocation(Location_CanvasViewModel loc)
        {
            GridVector2 pos;
            bool Success = Locations.TryGetPosition(loc, out pos);
            if(!Success)
                throw new ArgumentException("Could not map location: " + loc.ToString());

            return pos; 
        }

        public IUIObjectBasic GetNearestAnnotation(GridVector2 WorldPosition, out double distance)
        {
            distance = double.MaxValue;
            IUIObjectBasic FoundObject = null;
            double locDistance = double.MaxValue;
            Location_CanvasViewModel NearestLocationObj = GetNearestLocation(WorldPosition, out locDistance);
            if (NearestLocationObj != null)
            {
                FoundObject = NearestLocationObj as IUIObjectBasic;
                distance = locDistance;
            }

            
            double linkDistance;
            GridVector2 linkPosition; 
            StructureLink FoundLink = StructureLinksSearch.GetNearest(WorldPosition, out linkPosition, out linkDistance);
            if (FoundLink != null && linkDistance < locDistance)
            {
                if (linkDistance < FoundLink.Radius)
                {
                    FoundObject = FoundLink;
                    distance = linkDistance;
                }
            }
            
            return FoundObject;
        }

        

        /// <summary>
        /// Gets the nearest location, preferring locations on the same section, then checking other sections
        /// </summary>
        /// <param name="WorldPosition"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="font"></param>
        /// <param name="locPosition"></param>
        /// <returns></returns>
        public Location_CanvasViewModel GetNearestLocation(GridVector2 WorldPosition, out double distance)
        {
            distance = double.MaxValue; 
//            double minDistance = double.MaxValue;

            if (Locations == null)
                return null;

            /*Check to see if we clicked a location*/

            Location_CanvasViewModel bestLoc = Locations.FindNearest(WorldPosition, out distance);
            if (bestLoc != null)
            {       
                if (distance < bestLoc.Radius)
                {
                    //minDistance = distance;
                }
                else
                {
                    bestLoc = null;
                }
            }
            
            return bestLoc; 
        }
        
                /*Check to see if we clicked a location on a reference section*/
        /*
                //If we're still here check locations on other sections
                long locID = TransformedRefLocationQuadTree.FindNearest(WorldPosition, out distance);
                if (locID != default(long))
                {
                    loc = Store.Locations.GetObjectByID(locID);
                    if (loc != null)
                    {
                        if (distance < minDistance)
                        {
                            if (distance < loc.OffSectionRadius)
                            {
                                BestLoc = loc;
                                minDistance = distance;
                            }
                        }
                    }
                }
            }

            return BestLoc;
         
        }
         */
        

        #endregion


        /// <summary>
        /// Load the annotations for the passed section and its reference sections
        /// </summary>
        /// <param name="section"></param>
        internal void LoadSectionAnnotations()
        {
            Trace.WriteLine("LoadSectionAnnotations: " + Section.Number.ToString(), "WebAnnotation");


//            Task.Factory.StartNew(() => { 
                                            //Store.Structures.GetObjectsForSection(Section.Number); 
                                            //Store.Locations.GetObjectsForSectionAsynch(Section.Number); 
                                            //});

            MixedLocalAndRemoteQueryResults<long, StructureObj> structure_results = Store.Structures.GetObjectsForSectionAsynch(Section.Number);
#if DEBUG
            //structure_results.ServerRequestResult.AsyncWaitHandle.WaitOne();
            //Store.Structures.GetObjectsForSection(Section.Number); 
#else
            Store.Structures.GetObjectsForSection(Section.Number);
#endif
            //        
            
            //Have to let the Lock go before we call the location store or we can get a deadlock.  Don't modify 
            //data structures after this point. 

            //if (!HaveLoadedSectionAnnotations)
            //    AddLocations(Store.Locations.GetLocationsForSection(Section.Number).Values);
       
            MixedLocalAndRemoteQueryResults<long, LocationObj> results = Store.Locations.GetObjectsForSectionAsynch(Section.Number);
            //ConcurrentDictionary<long, LocationObj> KnownObjects = Store.Locations.GetLocalObjectsForSection(Section.Number);

            
            //System.Threading.Tasks.Task.Factory.StartNew(() => this.AddLocations(results.KnownObjects.Values));
            //System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => this.AddLocations(results.KnownObjects.Values)));
            this.AddLocations(results.KnownObjects.Values);

            HaveLoadedSectionAnnotations = true;
        }

        
        
        

        /// <summary>
        /// Return all the line segments visible in the passed bounds
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public StructureLink[] VisibleStructureLinks(GridRectangle bounds)
        {
            StructureLink[] LinkList = StructureLinksSearch.GetValues(bounds);
            return LinkList;
        }

        internal void AddStructureLinks(IEnumerable<LocationObj> locations)
        {
            foreach (LocationObj locObj in locations)
            {
                if (!locObj.ParentID.HasValue)
                    continue; 

                StructureObj parent = Store.Structures.GetObjectByID(locObj.ParentID.Value, true);//locObj.Parent;
                if (parent == null)
                    continue;

                if (parent.NumLinks > 0)
                {
                    AddStructureLinks(parent.LinksCopy);
                }
            }
        }

        internal void AddStructureLinks(IEnumerable<StructureObj> structures)
        {
            foreach (StructureObj structObj in structures)
            {
                if (structObj.NumLinks > 0)
                    AddStructureLinks(structObj.LinksCopy);
            }
        }
        
        /// <summary>
        /// All locations which are linked get a line between them
        /// </summary>
        internal void AddStructureLinks(IEnumerable<StructureLinkObj> structureLinks)
        {
            foreach(StructureLinkObj structLinkObj in structureLinks)
            {
                if (structLinkObj == null)
                    continue; 
                
                if (structLinkObj.SourceID == structLinkObj.TargetID)
                {
                    Trace.WriteLine("Something is wrong on the server, struct ID links to itself: " + structLinkObj.SourceID.ToString());
                    Store.StructureLinks.Remove(structLinkObj);
                    Store.StructureLinks.Save();
                    continue; //Something is wrong in the database
                }
                //The link may have been created to a structure on an adjacent section
                ConcurrentDictionary<long, Location_CanvasViewModel> SourceLocations = null;
                bool Success = LocationsForStructure.TryGetValue(structLinkObj.SourceID, out SourceLocations);
                if (Success == false)
                    continue;

                ConcurrentDictionary<long, Location_CanvasViewModel> TargetLocations = null;
                Success = LocationsForStructure.TryGetValue(structLinkObj.TargetID, out TargetLocations);
                if (Success == false)
                    continue;

                if(SourceLocations.Count == 0 || TargetLocations.Count == 0)
                    continue; 
                
                //Brute force a search for the shortest distance between the two structures.
                double MinDistance = double.MaxValue;
                Location_CanvasViewModel BestSourceLoc = null; 
                Location_CanvasViewModel BestTargetLoc = null;

                foreach (Location_CanvasViewModel SourceLoc in SourceLocations.Values)
                {
                    foreach (Location_CanvasViewModel TargetLoc in TargetLocations.Values)
                    {
                        double dist = GridVector2.Distance(SourceLoc.VolumePosition, TargetLoc.VolumePosition);
                        if (dist < MinDistance)
                        {
                            BestSourceLoc = SourceLoc;
                            BestTargetLoc = TargetLoc;
                            MinDistance = dist;
                        }
                    }
                }

                //OK, create a StructureLink between the locations
                StructureLink StructLink = new StructureLink(structLinkObj, BestSourceLoc, BestTargetLoc);

                //An error can occur if two structures are linked to each other twicea, once as a source and once as a destination.
                StructureLinksSearch.TryAdd(StructLink.lineSegment, StructLink);
            }
        }

        internal void RemoveStructureLinks(IEnumerable<LocationObj> locations)
        {
            foreach (LocationObj locObj in locations)
            {
                StructureObj parent = locObj.Parent;
                if (parent == null)
                    continue;

                if (parent.NumLinks > 0)
                    RemoveStructureLinks(parent.LinksCopy);
            }
        }

        internal void RemoveStructureLinks(IEnumerable<StructureObj> structures)
        {
            foreach(StructureObj structObj in structures)
            {
                if (structObj.NumLinks > 0)
                    RemoveStructureLinks(structObj.LinksCopy);
            }
        }

        /// <summary>
        /// All locations which are linked get a line between them
        /// </summary>
        internal void RemoveStructureLinks(IEnumerable<StructureLinkObj> structureLinks)
        {
            if (structureLinks == null)
                return; 

            foreach(StructureLinkObj structLinkObj in structureLinks)
            {
                if (structLinkObj == null)
                    continue;

                StructureLink link = new StructureLink(structLinkObj); 
                GridLineSegment line; 
                if(StructureLinksSearch != null)
                    StructureLinksSearch.TryRemove(link, out line); 

            }
        }

        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            System.Collections.Specialized.NotifyCollectionChangedEventArgs CollectionChangeArgs = e as System.Collections.Specialized.NotifyCollectionChangedEventArgs;
            if (CollectionChangeArgs != null)
            {
                Type senderType = sender.GetType();
                if (senderType == typeof(StructureStore))
                {
                    this.OnStructuresStoreChanged(sender, CollectionChangeArgs);
                    return true; 
                }
                else if (senderType == typeof(StructureLinkStore))
                {
                    this.OnStructureLinksStoreChanged(sender, CollectionChangeArgs);
                    return true; 
                }
            }

            PropertyChangedEventArgs PropertyChangedArgs = e as PropertyChangedEventArgs;
            if (PropertyChangedArgs != null)
            {
                if (sender.GetType() == typeof(LocationObj))
                {
                    OnLocationPropertyChanged(sender, PropertyChangedArgs);
                    return true;
                }
            }

            PropertyChangingEventArgs PropertyChangingArgs = e as PropertyChangingEventArgs;
            if (PropertyChangingArgs != null)
            {
                if (sender.GetType() == typeof(LocationObj))
                {
                    OnLocationPropertyChanging(sender, PropertyChangingArgs);
                    return true; 
                }
            }




            Debug.Fail("Weak Event not handled");
            return false;
        }

        protected void Dispose(bool freeManagedObjectsAlso)
        {
            if (freeManagedObjectsAlso)
            {
                if (StructureLinksSearch != null)
                {
                    this.StructureLinksSearch.Dispose();
                    this.StructureLinksSearch = null;
                }

                if (this.Locations != null)
                {
                    this.Locations.Dispose();
                    this.Locations = null;
                }
            }
            
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); 
        }
    }
}
