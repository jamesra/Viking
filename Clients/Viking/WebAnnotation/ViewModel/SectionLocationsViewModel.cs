
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
using SqlGeometryUtils;
using WebAnnotation.View;


namespace WebAnnotation.ViewModel
{
    /// <summary>
    /// Stores information about location queries for this region in the volume
    /// </summary>
    public class RegionRequestData
    {
        public DateTime LastQuery = DateTime.MinValue;

        /// <summary>
        /// True if a query has been sent to the server but has not returned
        /// </summary>
        public bool OutstandingQuery
        {
            get
            {
                if (this.AsyncResult == null)
                    return false;

                return AsyncResult.IsCompleted;
            }
        }

        public IAsyncResult AsyncResult;

        public RegionRequestData(DateTime query, IAsyncResult result)
        {
            AsyncResult = result;
            query = LastQuery;
        }
    }

    public class AnnotationRegions : RegionPyramid<RegionRequestData>
    {
        /// <summary>
        /// If set to true any threads using this objects should cancel loading operations
        /// </summary>
        public bool CancelRunningOperations = false; 

        public AnnotationRegions(GridRectangle Boundaries, GridCellDimensions cellDimensions)
            : base (Boundaries, cellDimensions)
        { }
    }

    /// <summary>
    /// This class manages LocationViewModels used on a canvas.  
    /// It handles hit detection, search, and positioning using canvas transforms
    /// </summary>
    public class SectionLocationsViewModel : System.Windows.IWeakEventListener
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
        /// </summary>
        private RTree.RTree<LocationCanvasView> LocationViews = null;

        /// <summary>
        /// Locations on our section as they should be viewed from adjacent sections
        /// </summary>
        private RTree.RTree<LocationCanvasView> AdjacentLocationViews = null;

        /// <summary>
        /// Maps a structureID to all the locations for that structure on the visible section
        /// </summary>
        private ConcurrentDictionary<long, ConcurrentDictionary<long, LocationCanvasView>> LocationsForStructure = new ConcurrentDictionary<long, ConcurrentDictionary<long, LocationCanvasView>>();
        
        /// <summary>
        /// Allows us to describe all the StructureLinks visible on a screen
        /// </summary>
        private RTree.RTree<StructureLink> StructureLinksSearch = null;

        /// <summary>
        /// This is a symptom of being halfway to the Jotunn architecture.  This is a pointer to the 
        /// parent section viewer control which can perform transforms
        /// </summary>
        public readonly Viking.UI.Controls.SectionViewerControl parent;

        private int SectionNumber { get {return this.Section.Number; }}

        private AnnotationRegions RegionQueries;
        private Microsoft.Xna.Framework.Rectangle LastSceneViewport;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="section"></param>
        /// <param name="Parent"></param>
        private bool SubmitUpdatedVolumePositions = false;

        public SectionLocationsViewModel(SectionViewModel section,  Viking.UI.Controls.SectionViewerControl Parent)
        {
            this.parent = Parent;
            Trace.WriteLine("Create SectionLocationsViewModel for " + section.Number.ToString());
            this.Section = section;

            GridRectangle bounds = AnnotationOverlay.SectionBounds(parent, parent.Section.Number);

            RegionQueries = new AnnotationRegions(bounds, new GridCellDimensions(bounds.Width / 2.0, bounds.Height / 2.0));

            if (LocationViews == null)
                LocationViews = new RTree.RTree<LocationCanvasView>(); //new QuadTree<Location_CanvasViewModel>(bounds)

            if (AdjacentLocationViews == null)
                AdjacentLocationViews = new RTree.RTree<LocationCanvasView>();

            this.SubmitUpdatedVolumePositions = section.VolumeViewModel.UpdateServerVolumePositions;

            LocationsForStructure = new ConcurrentDictionary<long, ConcurrentDictionary<long, LocationCanvasView>>();

            StructureLinksSearch = new RTree.RTree<StructureLink>();
            
            CollectionChangedEventManager.AddListener(Store.Structures, this);
            CollectionChangedEventManager.AddListener(Store.StructureLinks, this);
        }

        #region Location Property Changes

        protected void OnLocationPropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            LocationObj loc = sender as LocationObj;
            if (loc == null)
                return;
             
            //Update if a position or everything has changed
            if (LocationObj.IsGeometryProperty(e.PropertyName))
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
            if (LocationObj.IsGeometryProperty(e.PropertyName))
            {
               /* Location locView = new Location(loc);
                if (e.PropertyName == "")
                {
                    MapLocation(loc); 
                }
                */
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
            //Don't bother mapping if the location was already mapped
            if (loc.VolumeTransformID == parent.CurrentTransformUniqueID)
                return true;
            
            switch(loc.TypeCode)
            {
                case LocationType.POINT:
                    return MapLocationByCentroid(loc);
                case LocationType.CIRCLE:
                    return MapLocationByCentroid(loc);
                default:
                    return MapLocationByControlPoints(loc); 
            }
        }
        
        /// <summary>
        /// A faster mapping technique for geometries that do not use control points such as circles and points.
        /// </summary>
        /// <param name="loc"></param>
        /// <returns></returns>
        private bool MapLocationByCentroid(LocationObj loc)
        {
             //Don't bother mapping if the location was already mapped
            if (loc.VolumeTransformID == parent.CurrentTransformUniqueID)
                return true;

            GridVector2 VolumePosition = new GridVector2(-1, -1);

            bool mappedPosition = parent.TrySectionToVolume(loc.Position, this.Section.section, out VolumePosition);
            if (!mappedPosition) //Remove locations we can't map
            {
                Trace.WriteLine("AddLocation: Location #" + loc.ID.ToString() + " was unmappable.", "WebAnnotation");
                return false;
            }

            loc.VolumeTransformID = parent.CurrentTransformUniqueID;
            loc.VolumeShape = loc.VolumeShape.MoveTo(VolumePosition);
            //loc.VolumePosition = VolumePosition;

            return true;
        }

        /// <summary>
        /// Map all of the control points for the geometry individually
        /// </summary>
        /// <param name="loc"></param>
        /// <returns></returns>
        private bool MapLocationByControlPoints(LocationObj loc)
        {
            //Don't bother mapping if the location was already mapped
            if (loc.VolumeTransformID == parent.CurrentTransformUniqueID)
                return true;

            GridVector2[] VolumePositions;
            GridVector2[] points = loc.MosaicShape.ToPoints();

            bool mappedPosition = parent.TrySectionToVolume(loc.MosaicShape.ToPoints(), this.Section.section, out VolumePositions);
            if (!mappedPosition) //Remove locations we can't map
            {
                Trace.WriteLine("AddLocation: Location #" + loc.ID.ToString() + " was unmappable.", "WebAnnotation");
                return false;
            }

            loc.VolumeTransformID = parent.CurrentTransformUniqueID;
            //loc.VolumePosition = VolumePosition;
            loc.VolumeShape = SqlGeometryUtils.GeometryExtensions.ToGeometry(loc.MosaicShape.STGeometryType(), VolumePositions);

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
            bool UpdateVolumeLocations = false;
            bool HaveUpdatedVolumePositionsToSubmit = false;
            long VolumePositionUpdatedCount = 0;


            if (this.SubmitUpdatedVolumePositions && this.parent.CurrentVolumeTransform == this.Section.VolumeViewModel.DefaultVolumeTransform)
            {
                UpdateVolumeLocations = true;
            }  
                        
            foreach(LocationObj loc in listLocations)
            {

                if(AddLocation(loc, Subscribe, UpdateVolumeLocations))
                {
                    VolumePositionUpdatedCount++; 
                    HaveUpdatedVolumePositionsToSubmit |= true; 
                }
                //Add the location to our mapping if the location is on our section
                
                   
                   // Debug.Assert(Added);

                //    AddLocationLinks(locView);
                
            }

            if (UpdateVolumeLocations && HaveUpdatedVolumePositionsToSubmit)
            {
                //System.Threading.ThreadPool.QueueUserWorkItem( f => { Store.Locations.Save(); } );

                //System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Func<bool>(Store.Locations.Save), System.Windows.Threading.DispatcherPriority.Background, null);

                Trace.WriteLine("Updated " + VolumePositionUpdatedCount.ToString() + " volume positions");
                Store.Locations.Save(); 
            } 
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
             
            if (UpdateVolumeLocations && FirstMapping)
            {
                if (GridVector2.DistanceSquared(original, loc.VolumePosition) > 0)
                {
                    loc.SubmitOnNextUpdate();
                    UpdatedVolumeLocation = true;
                }
            }

            //Add location if it hasn't been seen before
            LocationCanvasView locView = AnnotationViewFactory.Create(loc);
            LocationCanvasView locAdjacentView = AnnotationViewFactory.CreateAdjacent(loc);

            RTree.Rectangle bbox = locView.BoundingBox.ToRTreeRect((float)loc.Z);

            if (this.LocationViews.TryAdd(bbox, locView))
            {
                this.AdjacentLocationViews.TryAdd(locAdjacentView.BoundingBox.ToRTreeRect((float)loc.Z), locAdjacentView);
                if (Subscribe)
                {
                    //locView.RegisterForLocationEvents();
                    SubscribeToLocationChangeEvents(loc);
                }


                ConcurrentDictionary<long, LocationCanvasView> KnownLocationsForStructure;
                KnownLocationsForStructure = LocationsForStructure.GetOrAdd(loc.ParentID.Value, (key) => { return new ConcurrentDictionary<long, LocationCanvasView>(); });
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

        private bool RemoveLocation(RTree.RTree<LocationCanvasView> rtree, LocationCanvasView locView, bool Unsubscribe)
        {
            LocationCanvasView RemovedValue = null;
            bool RemoveSuccess = rtree.Delete(locView, out RemovedValue);
            if (RemoveSuccess)
            {
                RemovedValue.DeregisterForLocationEvents();

                if (Unsubscribe)
                {
                    UnsubscribeToLocationChangeEvents(RemovedValue.modelObj);
                }
            }

            return RemoveSuccess;
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
                    RemoveLocation(LocationViews, AnnotationViewFactory.Create(loc), Unsubscribe);
                    RemoveLocation(AdjacentLocationViews, AnnotationViewFactory.CreateAdjacent(loc), Unsubscribe);                    
                }
                
                ConcurrentDictionary<long, LocationCanvasView> KnownLocationsForStructure = null;
                bool Success = LocationsForStructure.TryGetValue(loc.ParentID.Value, out  KnownLocationsForStructure);
                if (Success)
                {
                    LocationCanvasView removedLoc;
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

        public ICollection<LocationCanvasView> GetLocations()
        {
            return LocationViews.Items; 
        }

        public ICollection<LocationCanvasView> GetLocations(GridRectangle bounds)
        {  
            return LocationViews.Intersects(bounds.ToRTreeRect((float)this.Section.Number));
        }

        public ICollection<LocationCanvasView> GetLocations(GridVector2 point)
        {
            return LocationViews.Intersects(point.ToRTreeRect((float)this.Section.Number));
        }

        public ICollection<LocationCanvasView> GetAdjacentLocations()
        {
            return AdjacentLocationViews.Items;
        }

        public ICollection<LocationCanvasView> GetAdjacentLocations(GridRectangle bounds)
        {
            return AdjacentLocationViews.Intersects(bounds.ToRTreeRect((float)this.Section.Number));
        }

        public ICollection<LocationCanvasView> GetAdjacentLocations(GridVector2 point)
        {
            return AdjacentLocationViews.Intersects(point.ToRTreeRect((float)this.Section.Number));
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
        public bool TryGetPositionForLocation(LocationCanvasView loc, out GridVector2 position)
        {
            position = loc.modelObj.VolumePosition;

            return true;
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

        public GridVector2 GetPositionForLocation(LocationCanvasView loc)
        {
            return loc.modelObj.VolumePosition;
        }

        public IUIObjectBasic GetNearestAnnotation(GridVector2 WorldPosition, out double distance)
        {

            double linkDistance = double.MaxValue;
            distance = double.MaxValue;
            StructureLink NearestLink = null;
            List<StructureLink> intersecting_candidates = StructureLinksSearch.Intersects(WorldPosition.ToRTreeRect(this.SectionNumber)).Where(l => l.lineSegment.IsNearestPointWithinLineSegment(WorldPosition) && l.lineSegment.DistanceToPoint(WorldPosition) <= l.Radius).ToList();            
            NearestLink = intersecting_candidates.OrderBy(l => l.lineSegment.DistanceToPoint(WorldPosition) / l.Radius).FirstOrDefault();
            
            if (NearestLink != null)
            {
                linkDistance = NearestLink.lineSegment.DistanceToPoint(WorldPosition);
            }

            
            IUIObjectBasic FoundObject = null;
            double locDistance = double.MaxValue;
            LocationCanvasView NearestLocationObj = GetNearestLocation(WorldPosition, out locDistance);
            if (NearestLocationObj != null)
            {
                FoundObject = NearestLocationObj as IUIObjectBasic;
            }

            //Figure out which object we are closer to the center of, the location or the link
            if (NearestLink != null && NearestLocationObj != null)
            {
                if(linkDistance / NearestLink.Radius <= NearestLocationObj.DistanceFromCenterNormalized(WorldPosition))
                {
                    NearestLocationObj = null;
                }
                else
                {
                    NearestLink = null; 
                }

            }
            
            if(NearestLink != null)
            {
                distance = NearestLink.lineSegment.DistanceToPoint(WorldPosition);
                return NearestLink;
            }
            else if (NearestLocationObj != null)
            {
                distance = locDistance;
                return NearestLocationObj;
            }

            return null;
        }

        

        /// <summary>
        /// Gets the nearest location, preferring locations on the same section, then checking other sections
        /// </summary>
        /// <param name="WorldPosition"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="font"></param>
        /// <param name="locPosition"></param>
        /// <returns></returns>
        public LocationCanvasView GetNearestLocation(GridVector2 WorldPosition, out double distance)
        {
            distance = double.MaxValue; 
//            double minDistance = double.MaxValue;

            if (LocationViews == null)
                return null;

            /*Check to see if we clicked a location*/

            List<LocationCanvasView> candidates = LocationViews.Intersects(WorldPosition.ToRTreeRect(this.SectionNumber));

            //TODO: Put the SQL intersection test 
            List<LocationCanvasView> intersecting_candidates = candidates.Where(c => c.Intersects(WorldPosition)).ToList();
            LocationCanvasView nearest = intersecting_candidates.OrderBy(c => c.DistanceFromCenterNormalized(WorldPosition)).FirstOrDefault();
            if(nearest == null)
                return null;
            else
            {
                distance = nearest.Distance(WorldPosition);
            }

            return nearest;
        }
          
        #endregion


        /// <summary>
        /// Load the annotations for the passed section and its reference sections
        /// </summary>
        /// <param name="section"></param>
        internal void LoadSectionAnnotations(bool LoadStructures)
        {
            Trace.WriteLine("LoadSectionAnnotations: " + Section.Number.ToString(), "WebAnnotation");


            //            Task.Factory.StartNew(() => { 
            //Store.Structures.GetObjectsForSection(Section.Number); 
            //Store.Locations.GetObjectsForSectionAsynch(Section.Number); 
            //});

            //
            if (LoadStructures)
            {
                MixedLocalAndRemoteQueryResults<long, StructureObj> structure_results = Store.Structures.GetObjectsForSectionAsynch(Section.Number);
            }
            //Store.Structures.GetObjectsForSection(Section.Number);
#if DEBUG
            
            //structure_results.ServerRequestResult.AsyncWaitHandle.WaitOne();
            //Store.Structures.GetObjectsForSection(Section.Number); 
#else
            
#endif
            //        
            
            //Have to let the Lock go before we call the location store or we can get a deadlock.  Don't modify 
            //data structures after this point. 

            //if (!HaveLoadedSectionAnnotations)
            //    AddLocations(Store.Locations.GetLocationsForSection(Section.Number).Values);
       
            MixedLocalAndRemoteQueryResults<long, LocationObj> results = Store.Locations.GetObjectsForSectionAsynch(Section.Number);
            //ConcurrentDictionary<long,LocationObj> locations = Store.Locations.GetObjectsForSection(Section.Number);
            //this.AddLocations(locations.Values);

            //Store.LocationLinks.GetObjectsForSection(Section.Number);

            //ConcurrentDictionary<long, LocationObj> KnownObjects = Store.Locations.GetLocalObjectsForSection(Section.Number);

            
            //System.Threading.Tasks.Task.Factory.StartNew(() => this.AddLocations(results.KnownObjects.Values));
            //System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => this.AddLocations(results.KnownObjects.Values)));
            this.AddLocations(results.KnownObjects.Values);

            HaveLoadedSectionAnnotations = true;
        }

        /// <summary>
        /// Return true if we should stop loading regions
        /// </summary>
        /// <returns></returns>
        private bool ShouldCancelLoadingRegions(AnnotationRegions regions, double DevicePixelWidth, double DevicePixelHeight)
        {
            if (regions.CancelRunningOperations)
                return true;

            if (DevicePixelHeight != parent.Scene.DevicePixelHeight ||
               DevicePixelWidth != parent.Scene.DevicePixelWidth)
                return true;

            return false; 
        }

        internal void LoadSectionAnnotationsInRegion(VikingXNA.Scene scene)
        {
            if (LastSceneViewport.Height != scene.Viewport.Bounds.Height ||
            LastSceneViewport.Width != scene.Viewport.Bounds.Width)
            {
                ResetRegionPyramid();
                LastSceneViewport = scene.Viewport.Bounds;
                string TraceString = string.Format("LoadSectionAnnotations, Reset Region Pyramid: {0}", Section.Number);
                Trace.WriteLine(TraceString, "WebAnnotation");
            }

            if(!HaveLoadedSectionAnnotations)
            {
                var localLocations = Store.Locations.GetLocalObjectsForSection(Section.Number);
                if (localLocations.Count > 0)
                {
                    Task.Factory.StartNew(() => this.AddLocations(localLocations.Values));
                }
                HaveLoadedSectionAnnotations = true;
            }

            var RegionPyramid = this.RegionQueries;
            //If we change the magnification factor we should stop loading regions
            double StartingDevicePixelWidth = parent.Scene.DevicePixelWidth;
            double StartingDevicePixelHeight = parent.Scene.DevicePixelHeight;

            var level = RegionPyramid.GetLevelForVolumeBounds(scene.VisibleWorldBounds, scene.DevicePixelWidth);
            GridRange<RegionRequestData> gridRange = level.SubGridForRegion(scene.VisibleWorldBounds);

            DateTime currentTime = DateTime.UtcNow;

            for (int iY = gridRange.Indicies.iMinY; iY < gridRange.Indicies.iMaxY; iY++)
            {
                for (int iX = gridRange.Indicies.iMinX; iX < gridRange.Indicies.iMaxX; iX++)
                {
                    RegionRequestData cell = level.Cells[iX, iY];
                    GridRectangle cellBounds = level.CellBounds(iX, iY);

                    if (ShouldCancelLoadingRegions(RegionPyramid,StartingDevicePixelWidth, StartingDevicePixelHeight))
                        return; 

                    //Check with the server every 120 seconds if we've already loaded the annotations and there is no outstanding query
                    if (cell == null ||
                        (!cell.OutstandingQuery && System.TimeSpan.FromTicks(DateTime.UtcNow.Ticks - cell.LastQuery.Ticks).Seconds > 120))
                    {
                        DateTime? LastQueryUtc = cell == null ? new DateTime?() : level.Cells[iX, iY].LastQuery;
                        MixedLocalAndRemoteQueryResults<long, LocationObj> locations = Store.Locations.GetObjectsInRegionAsync(Section.Number, cellBounds, level.MinRadius, LastQueryUtc);
                        if (locations.KnownObjects.Values.Count > 0)
                            Task.Factory.StartNew(() => this.AddLocations(locations.KnownObjects.Values));

                        level.Cells[iX, iY] = new RegionRequestData(currentTime, locations.ServerRequestResult);

                        string TraceString = string.Format("LoadSectionAnnotations: {0} ({1},{2}) Level:{3} MinRadius:{4}", Section.Number, iX, iY, level.Level, level.MinRadius);
                        Trace.WriteLine(TraceString, "WebAnnotation");

                        /*
                        ConcurrentDictionary<long, LocationObj> locations = Store.Locations.GetObjectsInRegion(Section.Number, cellBounds, level.MinRadius, LastQueryUtc);
                        
                        if (locations.Values.Count > 0)
                            Task.Factory.StartNew(() => this.AddLocations(locations.Values));

                        level.Cells[iX, iY].OutstandingQuery = false;
                        */
                    }
                }
            } 
        }

        /// <summary>
        /// Call this when the viewport size changes, which means the MinRadius value has changed for the GetObjectsInRegion style calls
        /// </summary>
        public void ResetRegionPyramid()
        {
            lock(this.RegionQueries)
            {
                this.RegionQueries.CancelRunningOperations = true; 
                this.RegionQueries = new AnnotationRegions(this.RegionQueries.RegionBounds,
                                                                          new GridCellDimensions(this.RegionQueries.RegionBounds.Width / 2.0, this.RegionQueries.RegionBounds.Height / 2.0));
            }
        }

        /// <summary>
        /// Return all the line segments visible in the passed bounds
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public List<StructureLink> VisibleStructureLinks(GridRectangle bounds)
        {
            return StructureLinksSearch.Intersects(bounds.ToRTreeRect(this.SectionNumber)).ToList(); 
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

                StructureLink StructLink = CreateStructureLinkWithLocations(structLinkObj);
                if (StructLink == null)
                    continue; 
                 
                //An error can occur if two structures are linked to each other twicea, once as a source and once as a destination.
                StructureLinksSearch.TryAdd(StructLink.BoundingBox.ToRTreeRect(this.SectionNumber), StructLink);
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

        internal StructureLink CreateStructureLinkWithLocations(StructureLinkObj structLinkObj)
        {
            if (structLinkObj.SourceID == structLinkObj.TargetID)
            {
                Trace.WriteLine("Something is wrong on the server, struct ID links to itself: " + structLinkObj.SourceID.ToString());
                Store.StructureLinks.Remove(structLinkObj);
                Store.StructureLinks.Save();
                return null; 
            }

            //The link may have been created to a structure on an adjacent section
            ConcurrentDictionary<long, LocationCanvasView> SourceLocations = null;
            bool Success = LocationsForStructure.TryGetValue(structLinkObj.SourceID, out SourceLocations);
            if (Success == false)
                return null;

            ConcurrentDictionary<long, LocationCanvasView> TargetLocations = null;
            Success = LocationsForStructure.TryGetValue(structLinkObj.TargetID, out TargetLocations);
            if (Success == false)
                return null;

            if (SourceLocations.Count == 0 || TargetLocations.Count == 0)
                return null;

            //Brute force a search for the shortest distance between the two structures.
            double MinDistance = double.MaxValue;
            LocationCanvasView BestSourceLoc = null;
            LocationCanvasView BestTargetLoc = null;

            foreach (LocationCanvasView SourceLoc in SourceLocations.Values)
            {
                foreach (LocationCanvasView TargetLoc in TargetLocations.Values)
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
            return new StructureLink(structLinkObj, BestSourceLoc.modelObj, BestTargetLoc.modelObj);
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

                if (StructureLinksSearch != null)
                {
                    StructureLink link = CreateStructureLinkWithLocations(structLinkObj);
                    if (link == null)
                        continue; 
                    StructureLinksSearch.Delete(link, out link);
                }

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
    }
}
