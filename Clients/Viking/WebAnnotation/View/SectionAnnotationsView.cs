
#define SUBMITVOLUMEPOSITION

using Geometry;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes;
using Viking.Common;
using Viking.ViewModels;
using Viking.VolumeModel;
using WebAnnotation.View;
using WebAnnotationModel;

namespace WebAnnotation.ViewModel
{
    public interface ISectionAnnotationsView
    {
        void AddLocations(ICollection<LocationObj> locations);
        void AddLocation(LocationObj loc);

        bool RemoveLocations(ICollection<LocationObj> locations);
        bool RemoveLocation(LocationObj loc);

        List<HitTestResult> GetAnnotationsAtPosition(GridVector2 WorldPosition);
    }

    abstract class SectionAnnotationsViewBase : System.Windows.IWeakEventListener, ICanvasViewHitTesting
    {
        public abstract int SectionNumber { get; }

        public abstract Viking.VolumeModel.IVolumeToSectionTransform mapper
        {
            get;
        }

        public abstract void Init();

        public virtual void LoadAnnotationsInRegion(VikingXNA.Scene scene)
        {
            //We get an exception if the rectangle cannot be mapped to mosaic space, for example if it is out of bounds.  
            //We should fallback by mapping as many points as possible, and then using those to make an equivalent sized rectangle.
            //If we cannot map any points we shouldn't bother with the request.

            GridRectangle? VisibleMosaicBounds = scene.VisibleWorldBounds.ApproximateVisibleMosaicBounds(this.mapper);

            if (!VisibleMosaicBounds.HasValue)
                return;

            Store.LocationsByRegion.LoadSectionAnnotationsInRegion(VisibleMosaicBounds, scene.ScreenPixelSizeInVolume, this.SectionNumber, null, AddLocationsInLocalCache); // this.AddLocations, null);
        }

        protected abstract void AddLocationsInLocalCache(IEnumerable<LocationObj> locations);

        public abstract void AddLocations(IEnumerable<LocationObj> locations);

        public abstract void RemoveLocations(IEnumerable<LocationObj> locations);

        public abstract List<HitTestResult> GetAnnotations(GridVector2 WorldPosition);

        public abstract List<HitTestResult> GetAnnotations(GridLineSegment line);

        public abstract List<HitTestResult> GetAnnotations(GridRectangle line);

        private readonly KeyTracker<long> SubscribedLocations = new KeyTracker<long>();

        private readonly RefCountingKeyTracker<long> SubscribedStructures = new RefCountingKeyTracker<long>();

        protected bool IsSubscribed(LocationObj loc)
        {
            return SubscribedLocations.Contains(loc.ID);
        }

        protected bool SubscribeToLocationChangeEvents(LocationObj loc)
        {
            return SubscribedLocations.TryAdd(loc.ID, () => loc.SubscribeToPropertyChangeEvents(this));
        }

        protected bool UnsubscribeToLocationChangeEvents(LocationObj loc)
        {
            return SubscribedLocations.TryRemove(loc.ID, () => loc.UnsubscribeToPropertyChangeEvents(this));
        }

        protected void SubscribeToStructureChangeEvents(LocationObj loc)
        {
            SubscribedStructures.AddRef(loc.ParentID.Value, (StructureID) => loc.Parent.SubscribeToPropertyChangeEvents(this));
        }

        protected bool UnsubscribeToStructureChangeEvents(LocationObj loc)
        {
            return SubscribedStructures.ReleaseRef(loc.ParentID.Value, (StructureID) => loc.Parent.UnsubscribeToPropertyChangeEvents(this));
        }

        public abstract bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e);
    }

    /// <summary>
    /// This class manages Annotations on an adjacent section used on a canvas
    /// </summary>
    class AdjacentSectionAnnotationsView : SectionAnnotationsViewBase, System.Windows.IWeakEventListener
    {
        /// <summary>
        /// The section that is visible
        /// </summary>
        public int PrimarySectionNumber;

        /// <summary>
        /// The adjacent section this class is storing annotations for
        /// <summary>
        public readonly SectionViewModel AdjacentSection;

        public override int SectionNumber
        {
            get { return AdjacentSection.Number; }
        }

        public override string ToString()
        {
            return string.Format("Annotations on {0} seen from {1}", this.AdjacentSection.Number, this.SectionNumber);
        }

        protected readonly KeyTracker<long> KnownLocations = new KeyTracker<long>();
        protected readonly RTree.RTree<long> LocationsSearch = new RTree.RTree<long>();
        protected readonly ConcurrentDictionary<long, LocationCanvasView> LocationViews = new ConcurrentDictionary<long, LocationCanvasView>();

        /// <summary>
        /// Mapping interface for moving geometry between volume and section space
        /// </summary>
        public override Viking.VolumeModel.IVolumeToSectionTransform mapper
        {
            get
            {
                return this.AdjacentSection.ActiveSectionToVolumeTransform;
            }
        }

        public AdjacentSectionAnnotationsView(int primary_section_number, SectionViewModel AdjacentSection)
        {
            this.PrimarySectionNumber = primary_section_number;
            this.AdjacentSection = AdjacentSection;
            Init();
        }

        public override void Init()
        {
            ConcurrentDictionary<long, LocationObj> local = Store.Locations.GetLocalObjectsForSection(this.SectionNumber);
            if (local.Count > 0)
            {
                Task.Run(() => AddLocations(local.Values));
            }
        }

        private IEnumerable<LocationObj> LinkedLocationsOnPrimary(ICollection<long> LinkedIDs)
        {
            return Store.Locations.GetObjectsByIDs(LinkedIDs, false).Where(l => (int)l.Z == this.PrimarySectionNumber);
        }

        private IEnumerable<LocationObj> LinkedLocationsOnAdjacent(ICollection<long> LinkedIDs)
        {
            return Store.Locations.GetObjectsByIDs(LinkedIDs, false).Where(l => (int)l.Z == this.AdjacentSection.Number);
        }

        /// <summary>
        /// Load 
        /// </summary>
        /// <param name="locationObjs"></param>
        protected override void AddLocationsInLocalCache(IEnumerable<LocationObj> locationObjs)
        {
            LocationObj[] unknownObjs = locationObjs.Where(l => !this.KnownLocations.Contains(l.ID)).ToArray();
            if (unknownObjs.Length > 0)
            {
                AddLocations(unknownObjs);
            }
        }

        public override void AddLocations(IEnumerable<LocationObj> locations)
        {
            foreach (LocationObj loc in locations)
            {
                AddLocation(loc, true);
            }
        }

        protected void AddLocation(LocationObj loc, bool subscribe)
        {
            if (loc.Z == this.AdjacentSection.Number)
            {
                AddLocationOnAdjacent(loc, subscribe);
            }
            else if (loc.Z == this.PrimarySectionNumber)
            {
                //AddLocationOnPrimary(loc, subscribe);
                return;
            }
            else
            {
                throw new ArgumentException("Location does not belong to section");
            }
        }

        protected void AddLocationOnAdjacent(LocationObj loc, bool subscribe)
        { 
            KnownLocations.TryAdd(loc.ID, () =>
            {
                bool AnyOverlap = false; 
                if (!AnyOverlap)
                {
                    AddNonOverlappedOrUnlinkedLocation(loc);
                }

                ///Do not add an object if we are already tracking it
                if (subscribe)
                {
                    SubscribeToLocationChangeEvents(loc);
                }
            });
        }

        public override void RemoveLocations(IEnumerable<LocationObj> locations)
        {
            foreach (LocationObj loc in locations)
            {
                RemoveLocation(loc, true);
            }
        }

        protected bool RemoveLocation(LocationObj loc, bool unsubscribe)
        {
            if (loc.Z == this.AdjacentSection.Number)
            {
                return RemoveLocationOnAdjacent(loc, unsubscribe);
            }
            else if (loc.Z == this.PrimarySectionNumber)
            {
                //return RemoveLocationOnPrimary(loc, unsubscribe);
                return false;
            }
            else
            {
                throw new ArgumentException("Location does not belong to section");
            }
        }

        protected bool RemoveLocationOnAdjacent(LocationObj loc, bool unsubscribe)
        {
            if (loc.Z != this.SectionNumber)
            {
                throw new ArgumentException("Location does not belong to adjacent section");
            }

            return KnownLocations.TryRemove(loc.ID, () =>
            {
                if (unsubscribe)
                {
                    UnsubscribeToLocationChangeEvents(loc);
                }

                bool AnyOverlap = false;
                if (!AnyOverlap)
                {
                    RemoveNonOverlappedOrUnlinkedLocation(loc);
                }

                /*
                if (!AnyRemoved)
                {
                    Trace.WriteLine(string.Format("Location should exist, but was missing from our view {0}, Z={1}", loc.ToString(), ((int)loc.Z).ToString()));
                }
                */
            });
        }

        private bool AddNonOverlappedOrUnlinkedLocation(LocationObj loc)
        {
            LocationCanvasView locView = null;
            try
            {
                locView = AnnotationViewFactory.CreateAdjacent(loc, mapper);
            }
            catch (ArgumentOutOfRangeException except)
            {
                //Thrown when the point cannot be mapped.
                Trace.WriteLine(string.Format("Could not map location {0} on section {1}", loc.ID, loc.Section));
                return false;
            }

            LocationsSearch.Add(locView.BoundingBox.ToRTreeRect(loc.Section), loc.ID);
            bool added = LocationViews.TryAdd(loc.ID, locView);
            Debug.Assert(added);
            //NonOverlappedAnnotationsSearch.Add(locView.BoundingBox.ToRTreeRect((float)loc.Z), locView);

            return added;
        }

        private bool RemoveNonOverlappedOrUnlinkedLocation(LocationObj loc)
        {
            long RemovedID;
            LocationCanvasView locView;
            LocationViews.TryRemove(loc.ID, out locView);
            return LocationsSearch.Delete(loc.ID, out RemovedID);
        }

        public override List<HitTestResult> GetAnnotations(GridVector2 WorldPosition)
        {
            IEnumerable<long> intersecting_IDs = LocationsSearch.Intersects(WorldPosition.ToRTreeRect(this.SectionNumber));
            IEnumerable<LocationCanvasView> intersecting_locations = intersecting_IDs.Select(id => LocationViews[id]).Where(l => l.Contains(WorldPosition));

            List<HitTestResult> listHitResults = intersecting_locations.Select(l => new HitTestResult(l, (int)l.Z, l.VisualHeight, l.DistanceFromCenterNormalized(WorldPosition))).ToList();
            return listHitResults;
        }

        public override List<HitTestResult> GetAnnotations(GridLineSegment world_line)
        {
            IEnumerable<long> intersecting_IDs = LocationsSearch.Intersects(world_line.BoundingBox.ToRTreeRect(this.SectionNumber));
            IEnumerable<LocationCanvasView> intersecting_locations = intersecting_IDs.Select(id => LocationViews[id]).Where(l => l.Intersects(world_line));

            List<HitTestResult> listHitResults = intersecting_locations.Select(l => new HitTestResult(l, (int)l.Z, l.VisualHeight, l.DistanceFromCenterNormalized(world_line.A))).ToList();
            return listHitResults;
        }

        public override List<HitTestResult> GetAnnotations(GridRectangle world_rect)
        {
            IEnumerable<long> intersecting_IDs = LocationsSearch.Intersects(world_rect.ToRTreeRect(this.SectionNumber));
            IEnumerable<LocationCanvasView> intersecting_locations = intersecting_IDs.Select(id => LocationViews[id]);

            List<HitTestResult> listHitResults = intersecting_locations.Select(l => new HitTestResult(l, (int)l.Z, l.VisualHeight, 0)).ToList();
            return listHitResults;
        }

        public ICollection<LocationCanvasView> AnnotationsInRegion(GridRectangle worldRect)
        {
            List<long> loc_IDs = this.LocationsSearch.Intersects(worldRect.ToRTreeRect(this.SectionNumber));

            ICollection<LocationCanvasView> locations = loc_IDs.Select(id => this.LocationViews[id]).ToList();
            return locations;
        }

        public ICollection<long> LocationIdsInRegion(GridRectangle worldRect)
        {
            return this.LocationsSearch.Intersects(worldRect.ToRTreeRect(this.SectionNumber));
        }

        public ICollection<LocationCanvasView> LocationViewsForIds(ICollection<long> loc_IDs)
        {
            ICollection<LocationCanvasView> locations = loc_IDs.Select(id => this.LocationViews[id]).ToList();
            return locations;
        }

        protected void OnLocationPropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            LocationObj loc = sender as LocationObj;
            if (loc == null)
                return;

            //Update if a position or everything has changed
            if (LocationObj.IsGeometryProperty(e.PropertyName))
            {
                RemoveLocation(loc, false);

                SectionAnnotationsView PrimarySectionAnnotationView = AnnotationOverlay.GetAnnotationsForSection(PrimarySectionNumber);
                if (PrimarySectionAnnotationView != null)
                    PrimarySectionAnnotationView.SectionLocationLinks.RemoveLocationLinks(new LocationObj[] { loc });
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
                loc.ResetVolumePositionHasBeenCalculated();
                AddLocation(loc, false);

                SectionAnnotationsView PrimarySectionAnnotationView = AnnotationOverlay.GetAnnotationsForSection(PrimarySectionNumber);
                if (PrimarySectionAnnotationView != null)
                    PrimarySectionAnnotationView.SectionLocationLinks.AddLocationLinks(new LocationObj[] { loc });
            }
        }

        public override bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
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

    /// <summary>
    /// This class manages LocationViewModels used on a canvas.  
    /// It handles hit detection, search, and positioning using canvas transforms
    /// </summary>
    class SectionAnnotationsView : SectionAnnotationsViewBase, System.Windows.IWeakEventListener
    {
        /// <summary>
        /// The section we store annotations for
        /// <summary>
        public readonly SectionViewModel Section;

        public readonly AdjacentSectionAnnotationsView SectionAbove;

        public readonly AdjacentSectionAnnotationsView SectionBelow;

        public readonly SectionLocationLinkAnnotationsViewModel SectionLocationLinks;

        public readonly SectionStructureLinkAnnotationsViewModel SectionStructureLinks;

        protected readonly KeyTracker<long> KnownLocations = new KeyTracker<long>();
        /// <summary>
        /// Locations on the section we are providing an overlay for
        /// </summary>
        private readonly RTree.RTree<long> LocationViewSearch = new RTree.RTree<long>();
        protected readonly ConcurrentDictionary<long, LocationCanvasView> LocationViews = new ConcurrentDictionary<long, LocationCanvasView>();

        /// <summary>
        /// Maps a structureID to all the locations for that structure on the visible section
        /// </summary>
        private readonly ConcurrentDictionary<long, KeyTracker<long>> LocationsForStructure = new ConcurrentDictionary<long, KeyTracker<long>>();


        public ICollection<LocationLinkView> NonOverlappedLocationLinks
        {
            get
            {
                return SectionLocationLinks.NonOverlappedLinks;
            }
        }

        public ICollection<LocationLinkView> NonOverlappedLocationLinksInRegion(GridRectangle bounds)
        {
            return SectionLocationLinks.NonOverlappedLinksInRegion(bounds);
        }

        /// <summary>
        /// Mapping interface for moving geometry between volume and section space
        /// </summary>
        public override Viking.VolumeModel.IVolumeToSectionTransform mapper
        {
            get
            {
                return this.Section.ActiveSectionToVolumeTransform;
            }
        }

        public override int SectionNumber { get { return this.Section.Number; } }

        public override string ToString()
        {
            return string.Format("Section {0} annotations", this.SectionNumber);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="section"></param>
        /// <param name="Parent"></param>
        private bool SubmitUpdatedVolumePositions = false;

        public SectionAnnotationsView(SectionViewModel section)
        {
            Trace.WriteLine("Create SectionLocationsViewModel for " + section.Number.ToString());
            this.Section = section;

            SectionLocationLinks = new SectionLocationLinkAnnotationsViewModel(section);
            SectionStructureLinks = new SectionStructureLinkAnnotationsViewModel(this);

            this.SubmitUpdatedVolumePositions = section.VolumeViewModel.UpdateServerVolumePositions;

            if (this.Section.ReferenceSectionAbove != null)
                this.SectionAbove = new AdjacentSectionAnnotationsView(section.Number, Viking.UI.State.volume.SectionViewModels[this.Section.ReferenceSectionAbove.Number]);
            if (this.Section.ReferenceSectionBelow != null)
                this.SectionBelow = new AdjacentSectionAnnotationsView(section.Number, Viking.UI.State.volume.SectionViewModels[this.Section.ReferenceSectionBelow.Number]);

            CollectionChangedEventManager.AddListener(Store.Structures, this);
            CollectionChangedEventManager.AddListener(Store.StructureLinks, this);

            Init();
        }

        public override void Init()
        {
            ConcurrentDictionary<long, LocationObj> local = Store.Locations.GetLocalObjectsForSection(this.SectionNumber);
            if (local.Count > 0)
            {
                Task.Run(() => AddLocationBatch(local.Values));
            }
        }

        #region Structure Property Changes


        protected void OnStructurePropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            StructureObj s = sender as StructureObj;
            if (s == null)
                return;
        }

        protected void OnStructurePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            StructureObj s = sender as StructureObj;
            if (s == null)
                return;

            if (LocationsForStructure.ContainsKey(s.ID))
            {
                KeyTracker<long> locIDs = this.LocationsForStructure[s.ID];

                foreach (long locID in locIDs.ValuesCopy())
                {
                    LocationCanvasView locView;
                    if (this.LocationViews.TryGetValue(locID, out locView))
                    {
                        locView.OnParentPropertyChanged(sender, e);
                    }
                }
            }
        }

        #endregion

        #region Location Property Changes


        protected void OnLocationPropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            LocationObj loc = sender as LocationObj;
            if (loc == null)
                return;

            //Update if a position or everything has changed
            if (LocationObj.IsGeometryProperty(e.PropertyName))
            {
                SectionAbove?.RemoveLocations(Store.Locations.GetObjectsByIDs(loc.LinksCopy, false).Where(l => l.Z == SectionAbove.SectionNumber));
                SectionBelow?.RemoveLocations(Store.Locations.GetObjectsByIDs(loc.LinksCopy, false).Where(l => l.Z == SectionBelow.SectionNumber));
                //                Location locView = new Location(loc);
                LocationObj[] locs = new LocationObj[] { loc };
                RemoveOverlappedLocations(locs);
                SectionStructureLinks.RemoveStructureLinks(locs);
                SectionLocationLinks.RemoveLocationLinks(locs);
                RemoveLocations(new LocationObj[] { loc }, false);
            }
            else
            {
                LocationCanvasView locView;
                if (this.LocationViews.TryGetValue(loc.ID, out locView))
                {
                    locView.OnObjPropertyChanging(sender, e);
                }
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
                loc.ResetVolumePositionHasBeenCalculated();
                LocationObj[] locs = new LocationObj[] { loc };
                AddLocationBatch(locs);

                SectionAbove?.AddLocations(Store.Locations.GetObjectsByIDs(loc.LinksCopy, false).Where(l => l.Z == SectionAbove.SectionNumber));
                SectionBelow?.AddLocations(Store.Locations.GetObjectsByIDs(loc.LinksCopy, false).Where(l => l.Z == SectionBelow.SectionNumber));
            }
            else
            {
                LocationCanvasView locView;
                if (this.LocationViews.TryGetValue(loc.ID, out locView))
                {
                    locView.OnObjPropertyChanged(sender, e);
                }
            }
        }


        #endregion

        #region Cache updates

        private List<LocationObj> LocationsOnOurSectionLinkedFromSet(IEnumerable<LocationObj> locations)
        {
            List<long> LocationIDs = locations.SelectMany(l => l.LinksCopy).Where(id => this.KnownLocations.Contains(id)).Distinct().ToList();
            return Store.Locations.GetObjectsByIDs(LocationIDs, false);
        }

        private void AddLocationBatch(IEnumerable<LocationObj> locations)
        {
            AddLocations(locations);
            IEnumerable<LocationObj> locsOnOurSection = locations.Where(l => l.Z == this.SectionNumber);
            SectionStructureLinks.AddStructureLinks(locsOnOurSection);

            IEnumerable<LocationObj> locsOnOurSectionOrLinkedByInputLocations = locsOnOurSection.Union(LocationsOnOurSectionLinkedFromSet(locations));
            SectionLocationLinks.AddLocationLinks(locsOnOurSectionOrLinkedByInputLocations);
            AddOverlappedLocations(locsOnOurSectionOrLinkedByInputLocations);
        }

        private void RemoveLocationBatch(IEnumerable<LocationObj> locations)
        {
            IEnumerable<LocationObj> locsOnOurSection = locations.Where(l => l.Z == this.SectionNumber);
            IEnumerable<LocationObj> locsLinkedByInputLocations = LocationsOnOurSectionLinkedFromSet(locations);
            IEnumerable<LocationObj> locsOnOurSectionOrLinkedByInputLocations = locsOnOurSection.Union(locsLinkedByInputLocations);

            RemoveOverlappedLocations(locsOnOurSectionOrLinkedByInputLocations);
            SectionLocationLinks.RemoveLocationLinks(locsOnOurSection);

            SectionStructureLinks.RemoveStructureLinks(locsOnOurSection);
            RemoveLocations(locations);

            AddOverlappedLocations(locsOnOurSectionOrLinkedByInputLocations);
        }

        //Called when a key is added or removed from the store
        public void OnLocationsStoreChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            IEnumerable<LocationObj> listNewObjs;
            IEnumerable<LocationObj> listOldObjs;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    listNewObjs = e.NewItems.Cast<LocationObj>();
                    AddLocationBatch(listNewObjs);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    IEnumerable<LocationObj> OldItems = e.OldItems.Cast<LocationObj>();
                    IEnumerable<LocationObj> NewItems = e.NewItems.Cast<LocationObj>();
                    RemoveLocationBatch(OldItems);
                    AddLocationBatch(NewItems);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    listOldObjs = e.OldItems.Cast<LocationObj>();
                    RemoveLocationBatch(listOldObjs);
                    break;

                default:
                    Debug.Assert(false, "Unexpected change action in OnStoreAddRemoveKey");
                    break;
            }
        }

        //Called when a key is added or removed from the store
        public void OnLocationLinksStoreChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    IEnumerable<LocationLinkObj> listNewObjs = e.NewItems.Cast<LocationLinkObj>();
                    SectionLocationLinks.AddLocationLinks(listNewObjs.Select(link => link.ID));
                    AddOverlappedLocations(listNewObjs.Select(link => link.ID));
                    break;
                case NotifyCollectionChangedAction.Replace:
                    IEnumerable<LocationLinkObj> OldItems = e.OldItems.Cast<LocationLinkObj>();
                    IEnumerable<LocationLinkObj> NewItems = e.NewItems.Cast<LocationLinkObj>();
                    RemoveOverlappedLocations(OldItems.Select(link => link.ID));
                    SectionLocationLinks.RemoveLocationLinks(OldItems.Select(link => link.ID));
                    SectionLocationLinks.AddLocationLinks(NewItems.Select(link => link.ID));
                    AddOverlappedLocations(NewItems.Select(link => link.ID));
                    break;

                case NotifyCollectionChangedAction.Remove:
                    OldItems = e.OldItems.Cast<LocationLinkObj>();
                    RemoveOverlappedLocations(OldItems.Select(link => link.ID));
                    SectionLocationLinks.RemoveLocationLinks(OldItems.Select(link => link.ID));
                    AddOverlappedLocations(OldItems.Select(link => link.ID));
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
                    SectionStructureLinks.AddStructureLinks(e.NewItems.Cast<StructureObj>());
                    break;

                case NotifyCollectionChangedAction.Replace:
                    SectionStructureLinks.RemoveStructureLinks(e.OldItems.Cast<StructureObj>());
                    SectionStructureLinks.AddStructureLinks(e.NewItems.Cast<StructureObj>());
                    break;

                case NotifyCollectionChangedAction.Remove:
                    SectionStructureLinks.RemoveStructureLinks(e.OldItems.Cast<StructureObj>());
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
                    SectionStructureLinks.AddStructureLinks(e.NewItems.Cast<StructureLinkObj>());
                    break;

                case NotifyCollectionChangedAction.Replace:
                    SectionStructureLinks.RemoveStructureLinks(e.OldItems.Cast<StructureLinkObj>());
                    SectionStructureLinks.AddStructureLinks(e.NewItems.Cast<StructureLinkObj>());
                    break;

                case NotifyCollectionChangedAction.Remove:
                    SectionStructureLinks.RemoveStructureLinks(e.OldItems.Cast<StructureLinkObj>());
                    break;

                default:
                    Debug.Assert(false, "Unexpected change action in OnStoreAddRemoveKey");
                    break;
            }
        }


        public override void AddLocations(IEnumerable<LocationObj> listLocations)
        {
            AddLocations(listLocations.Where(l => l.Section == this.SectionNumber), true);

            if (SectionAbove != null)
                SectionAbove.AddLocations(listLocations.Where(l => l.Section == SectionAbove.SectionNumber));

            if (SectionBelow != null)
                SectionBelow.AddLocations(listLocations.Where(l => l.Section == SectionBelow.SectionNumber));
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
            /*
             * 10/17/2016: This update feature was replaced by the VikingAU command-line tool
            if (this.SubmitUpdatedVolumePositions)// && this.mapper.ID == this.Section.VolumeViewModel.DefaultVolumeTransform) TODO: Add the line back in to prevent saving transforms when the mosaic transform has been changed
            {
                UpdateVolumeLocations = true;
            }  
               */

            foreach (LocationObj loc in listLocations)
            {
                if (AddLocation(loc, Subscribe, UpdateVolumeLocations))
                {
                    VolumePositionUpdatedCount++;
                    HaveUpdatedVolumePositionsToSubmit |= true;
                }
            }
            /*
            if (UpdateVolumeLocations && HaveUpdatedVolumePositionsToSubmit)
            {
                //System.Threading.ThreadPool.QueueUserWorkItem( f => { Store.Locations.Save(); } );

                //System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Func<bool>(Store.Locations.Save), System.Windows.Threading.DispatcherPriority.Background, null);

                Trace.WriteLine("Updated " + VolumePositionUpdatedCount.ToString() + " volume positions");
                Store.Locations.Save(); 
            }*/
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

            return KnownLocations.TryAdd(loc.ID, () =>
            {
                //Add location if it hasn't been seen before
                LocationCanvasView locView = null;
                try
                {
                    locView = AnnotationViewFactory.Create(loc, this.mapper);
                }
                catch (ArgumentException e)
                {
                    //Could not add location, probably because of a transform mapping issue
                    Trace.WriteLine("ArgumentException adding location# " + loc.ToString());
                    return false;
                }

                bool AddedView = LocationViews.TryAdd(loc.ID, locView);
                Debug.Assert(AddedView == true);

                RTree.Rectangle bbox = locView.BoundingBox.ToRTreeRect((float)loc.Z);

                LocationViewSearch.Add(bbox, locView.ID);

                if (Subscribe)
                {
                    SubscribeToLocationChangeEvents(loc);
                    SubscribeToStructureChangeEvents(loc);
                }

                AddLocationsForStructure(loc.ParentID.Value, locView);
                return true;
            });
        }

        private void AddLocationsForStructure(long structureID, LocationCanvasView locView)
        { 
            var knownLocationsForStructure = LocationsForStructure.GetOrAdd(structureID, (key) => new KeyTracker<long>());
            knownLocationsForStructure.TryAdd(locView.ID);
            return;
        }

        private void RemoveLocationsForStructure(long structureID, long LocationID)
        {
            KeyTracker<long> KnownLocationsForStructure;
            if (LocationsForStructure.TryGetValue(structureID, out KnownLocationsForStructure))
            {
                KnownLocationsForStructure.TryRemove(LocationID);
                //TODO: Remove key tracker if the last location is removed?
            }

            return;
        }

        public override void RemoveLocations(IEnumerable<LocationObj> listLocations)
        {
            RemoveLocations(listLocations.Where(l => l.Section == this.SectionNumber), true);

            if (SectionAbove != null)
                SectionAbove.RemoveLocations(listLocations.Where(l => l.Section == SectionAbove.SectionNumber));

            if (SectionBelow != null)
                SectionBelow.RemoveLocations(listLocations.Where(l => l.Section == SectionBelow.SectionNumber));
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
                RemoveLocation(loc, Unsubscribe);
            }
        }

        protected void RemoveLocation(LocationObj loc, bool Unsubscribe)
        {
            KnownLocations.TryRemove(loc.ID, () =>
            {
                LocationCanvasView locView;
                bool Removed = LocationViews.TryRemove(loc.ID, out locView);
                Debug.Assert(Removed, "Missing location that was removed " + loc.ID.ToString());

                long RemovedID;
                bool RTreeRemoved = LocationViewSearch.Delete(loc.ID, out RemovedID);
                Debug.Assert(RTreeRemoved, "Could not remove location from RTree " + loc.ID.ToString());
                Debug.Assert(RemovedID == loc.ID);
                if (Unsubscribe)
                {
                    UnsubscribeToLocationChangeEvents(loc);
                    UnsubscribeToStructureChangeEvents(loc);
                }

                RemoveLocationsForStructure(loc.ParentID.Value, loc.ID);
            });
        }

        private static LocationObj GetLocationFromLinkOnThisSection(LocationLinkKey link, int SectionNumber)
        {
            LocationObj AOBj = Store.Locations.Contains(link.A) ? Store.Locations[link.A] : null;
            LocationObj BOBj = Store.Locations.Contains(link.B) ? Store.Locations[link.B] : null;

            if (AOBj == null || BOBj == null)
                return null;

            Debug.Assert(AOBj.Z != BOBj.Z);
            if (AOBj.Z == BOBj.Z)
                return null;

            if (AOBj.Z == SectionNumber)
                return AOBj;

            if (BOBj.Z == SectionNumber)
                return BOBj;

            return null;
        }

        private void AddOverlappedLocations(IEnumerable<LocationLinkKey> keys)
        {
            IEnumerable<LocationObj> locs = keys.Select(k => GetLocationFromLinkOnThisSection(k, this.SectionNumber)).Where(k => k != null);
            AddOverlappedLocations(locs);
        }

        private void RemoveOverlappedLocations(IEnumerable<LocationLinkKey> keys)
        {
            IEnumerable<LocationObj> locs = keys.Select(k => GetLocationFromLinkOnThisSection(k, this.SectionNumber)).Where(k => k != null);
            RemoveOverlappedLocations(locs);
        }

        private void AddOverlappedLocations(IEnumerable<LocationObj> locs)
        {

            foreach (LocationObj loc in locs)
            {
                ICollection<LocationLinkKey> overlapped_links = loc.Links.Select(l => new LocationLinkKey(l, loc.ID)).Where(linkKey => SectionLocationLinks.OverlappedLinkKeys.Contains(linkKey)).ToList();

                //long[] overlapped_links = loc.LinksCopy.Where(id => SectionLocationLinks.OverlappedAdjacentLocationIDs.Contains(id)).ToArray();
                if (overlapped_links.Count > 0)
                {
                    if (LocationViews.ContainsKey(loc.ID))
                    {
                        LocationCanvasView locView = LocationViews[loc.ID];
                        locView.OverlappedLinks = overlapped_links.Select(linkKey => linkKey.A == loc.ID ? linkKey.B : linkKey.A).ToList();
                    }
                    else
                    {
                        Trace.WriteLine("Location Views does not contain expected location: " + loc.ToString());
                    }
                }
            }
        }

        private void RemoveOverlappedLocations(IEnumerable<LocationObj> locs)
        {
            foreach (LocationObj loc in locs)
            {
                if (LocationViews.ContainsKey(loc.ID))
                {
                    LocationCanvasView locView = LocationViews[loc.ID];
                    locView.OverlappedLinks = new long[0];
                }
            }
        }

        #endregion

        #region Queries

        public ICollection<LocationCanvasView> GetLocations()
        {
            return LocationViews.Values;
        }

        public bool TryGetLocation(long ID, out LocationCanvasView outVal)
        {
            return this.LocationViews.TryGetValue(ID, out outVal);
        }

        public LocationCanvasView GetLocation(long ID)
        {
            LocationCanvasView outVal = null;
            if (this.LocationViews.TryGetValue(ID, out outVal))
                return outVal;
            return null;
        }

        public bool ContainsLocation(long ID)
        {
            return this.LocationViews.ContainsKey(ID);
        }

        public bool GetLocationsForStructure(long ID, out KeyTracker<long> child_locations)
        {
            child_locations = null;
            return LocationsForStructure.TryGetValue(ID, out child_locations);
        }

        public ICollection<LocationCanvasView> GetLocations(GridRectangle bounds)
        {
            List<long> intersectingIDs = LocationViewSearch.Intersects(bounds.ToRTreeRect((float)this.Section.Number));
            return intersectingIDs.Select(id => LocationViews[id]).ToList();
        }

        public ICollection<LocationCanvasView> GetLocations(GridVector2 point)
        {
            List<long> intersectingIDs = LocationViewSearch.Intersects(point.ToRTreeRect((float)this.Section.Number));
            return intersectingIDs.Select(id => LocationViews[id]).Where(l => l.Contains(point)).ToList();
        }

        public ICollection<LocationCanvasView> GetLocations(GridLineSegment line)
        {
            List<long> intersectingIDs = LocationViewSearch.Intersects(line.BoundingBox.ToRTreeRect((float)this.Section.Number));
            return intersectingIDs.Select(id => LocationViews[id]).Where(l => l.Intersects(line)).ToList();
        }

        public ICollection<StructureLinkViewModelBase> GetStructureLinks()
        {
            return SectionStructureLinks.GetStructureLinks();
        }

        public ICollection<StructureLinkViewModelBase> GetStructureLinks(GridRectangle bounds)
        {
            return SectionStructureLinks.GetStructureLinks(bounds);
        }

        public ICollection<StructureLinkViewModelBase> GetStructureLinks(GridVector2 point)
        {
            return SectionStructureLinks.GetStructureLinks(point);
        }

        public ICollection<StructureLinkViewModelBase> GetStructureLinks(GridLineSegment line)
        {
            return SectionStructureLinks.GetStructureLinks(line);
        }

        /// <summary>
        /// Return all the line segments visible in the passed bounds
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public List<StructureLinkViewModelBase> VisibleStructureLinks(VikingXNA.Scene scene)
        {
            return SectionStructureLinks.VisibleStructureLinks(scene);
        }

        /// <summary>
        /// Return a list of annotations that intersect the provided point
        /// </summary>
        /// <param name="WorldPosition"></param>
        /// <returns></returns>
        public override List<HitTestResult> GetAnnotations(GridVector2 WorldPosition)
        {
            List<HitTestResult> listIntersectingObjects = new List<HitTestResult>();
            listIntersectingObjects.AddRange(GetStructureLinks(WorldPosition).Select(o => new HitTestResult(o, this.SectionNumber, ((ICanvasView)o).VisualHeight, o.DistanceFromCenterNormalized(WorldPosition))));
            listIntersectingObjects.AddRange(GetLocations(WorldPosition).Select(o => new HitTestResult(o, (int)o.Z, o.VisualHeight, o.DistanceFromCenterNormalized(WorldPosition))));
            listIntersectingObjects.AddRange(GetAdjacentIntersectedAnnotations(WorldPosition));

            ICollection<LocationLinkView> listLocLinks = this.SectionLocationLinks.GetLocationLinks(WorldPosition);

            listIntersectingObjects.AddRange(listLocLinks.Select(ll => new HitTestResult(ll, this.SectionNumber, ((ICanvasView)ll).VisualHeight, ll.DistanceFromCenterNormalized(WorldPosition))));

            //Replace any container objects with the nested objects if the mouse is over a nested object

            return listIntersectingObjects;
        }

        /// <summary>
        /// Return a list of annotations on adjacent sections that intersect the provided point
        /// </summary>
        /// <param name="WorldPosition"></param>
        /// <returns></returns>
        public List<HitTestResult> GetAdjacentIntersectedAnnotations(GridVector2 WorldPosition)
        {
            List<HitTestResult> listAnnotations = new List<HitTestResult>();

            //            SortedDictionary<double, ICanvasView> dictNormDistanceToIntersectingObjects = new SortedDictionary<double, ICanvasView>();
            if (SectionAbove != null)
            {
                listAnnotations.AddRange(SectionAbove.GetAnnotations(WorldPosition));
            }

            if (SectionBelow != null)
            {
                listAnnotations.AddRange(SectionBelow.GetAnnotations(WorldPosition));
            }

            //Remove any Locations that we know are overlapped.
            return listAnnotations.Where(o =>
            {
                LocationCanvasView loc = o.obj as LocationCanvasView;
                if (loc == null)
                    return true;

                return !SectionLocationLinks.OverlappedAdjacentLocationIDs.Contains(loc.ID);
            }).ToList();
        }

        /// <summary>
        /// Return a list of annotations that intersect the provided line.  HitTestResults are ordered by the distance from the origin of the line, A.
        /// </summary>
        /// <param name="WorldPosition"></param>
        /// <returns></returns>
        public override List<HitTestResult> GetAnnotations(GridLineSegment world_line)
        {
            List<HitTestResult> listIntersectingObjects = new List<HitTestResult>();
            listIntersectingObjects.AddRange(GetStructureLinks(world_line).Select(o => new HitTestResult(o, this.SectionNumber, ((ICanvasView)o).VisualHeight, o.Distance(world_line.A))));
            listIntersectingObjects.AddRange(GetLocations(world_line).Select(o => new HitTestResult(o, (int)o.Z, o.VisualHeight, o.DistanceFromCenterNormalized(world_line.A))));
            listIntersectingObjects.AddRange(GetAdjacentIntersectedAnnotations(world_line));

            ICollection<LocationLinkView> listLocLinks = this.SectionLocationLinks.GetLocationLinks(world_line);

            listIntersectingObjects.AddRange(listLocLinks.Select(ll => new HitTestResult(ll, this.SectionNumber, ((ICanvasView)ll).VisualHeight, ll.DistanceFromCenterNormalized(world_line.A))));

            //Replace any container objects with the nested objects if the mouse is over a nested object

            return listIntersectingObjects;
        }

        /// <summary>
        /// Return a list of annotations that intersect the provided rectangle.  HitTestResults are not ordered and distance is always zero since they must intersect to return
        /// </summary>
        /// <param name="WorldPosition"></param>
        /// <returns></returns>
        public override List<HitTestResult> GetAnnotations(GridRectangle world_rect)
        {
            List<HitTestResult> listIntersectingObjects = new List<HitTestResult>();
            listIntersectingObjects.AddRange(GetStructureLinks(world_rect).Select(o => new HitTestResult(o, this.SectionNumber, ((ICanvasView)o).VisualHeight, 0)));
            listIntersectingObjects.AddRange(GetLocations(world_rect).Select(o => new HitTestResult(o, (int)o.Z, o.VisualHeight, 0)));
            listIntersectingObjects.AddRange(GetAdjacentIntersectedAnnotations(world_rect));

            ICollection<LocationLinkView> listLocLinks = this.SectionLocationLinks.GetLocationLinks(world_rect);

            listIntersectingObjects.AddRange(listLocLinks.Select(ll => new HitTestResult(ll, this.SectionNumber, ((ICanvasView)ll).VisualHeight, 0)));

            //Replace any container objects with the nested objects if the mouse is over a nested object

            return listIntersectingObjects;
        }

        /// <summary>
        /// Return a list of annotations on adjacent sections that intersect the provided line
        /// </summary>
        /// <param name="WorldPosition"></param>
        /// <returns></returns>
        public List<HitTestResult> GetAdjacentIntersectedAnnotations(GridLineSegment world_line)
        {
            List<HitTestResult> listAnnotations = new List<HitTestResult>();

            //            SortedDictionary<double, ICanvasView> dictNormDistanceToIntersectingObjects = new SortedDictionary<double, ICanvasView>();
            if (SectionAbove != null)
            {
                listAnnotations.AddRange(SectionAbove.GetAnnotations(world_line));
            }

            if (SectionBelow != null)
            {
                listAnnotations.AddRange(SectionBelow.GetAnnotations(world_line));
            }

            //Remove any Locations that we know are overlapped.
            return listAnnotations.Where(o =>
            {
                LocationCanvasView loc = o.obj as LocationCanvasView;
                if (loc == null)
                    return true;

                return !SectionLocationLinks.OverlappedAdjacentLocationIDs.Contains(loc.ID);
            }).ToList();
        }

        /// <summary>
        /// Return a list of annotations on adjacent sections that intersect the provided rectangle
        /// </summary>
        /// <param name="WorldPosition"></param>
        /// <returns></returns>
        public List<HitTestResult> GetAdjacentIntersectedAnnotations(GridRectangle world_rect)
        {
            List<HitTestResult> listAnnotations = new List<HitTestResult>();

            //            SortedDictionary<double, ICanvasView> dictNormDistanceToIntersectingObjects = new SortedDictionary<double, ICanvasView>();
            if (SectionAbove != null)
            {
                listAnnotations.AddRange(SectionAbove.GetAnnotations(world_rect));
            }

            if (SectionBelow != null)
            {
                listAnnotations.AddRange(SectionBelow.GetAnnotations(world_rect));
            }

            //Remove any Locations that we know are overlapped.
            return listAnnotations.Where(o =>
            {
                LocationCanvasView loc = o.obj as LocationCanvasView;
                if (loc == null)
                    return true;

                return !SectionLocationLinks.OverlappedAdjacentLocationIDs.Contains(loc.ID);
            }).ToList();
        }

        public ICollection<LocationCanvasView> AdjacentLocationsNotOverlappedInRegion(GridRectangle worldRect)
        {
            SortedSet<LocationCanvasView> adjacentLocations = new SortedSet<LocationCanvasView>();
            if (SectionAbove != null)
            {
                ICollection<LocationCanvasView> AnnotationsInRegion = SectionAbove.AnnotationsInRegion(worldRect);
                foreach (LocationCanvasView lv in AnnotationsInRegion)
                {
                    adjacentLocations.Add(lv);
                }
                //AnnotationsInRegion.Select(lv => adjacentLocations.Add(lv));
            }

            if (SectionBelow != null)
            {
                ICollection<LocationCanvasView> AnnotationsInRegion = SectionBelow.AnnotationsInRegion(worldRect);
                foreach (LocationCanvasView lv in AnnotationsInRegion)
                {
                    adjacentLocations.Add(lv);
                }
                //                AnnotationsInRegion.Select(lv => adjacentLocations.Add(lv));
            }

            return adjacentLocations.Where(l => !SectionLocationLinks.OverlappedAdjacentLocationIDs.Contains(l.ID)).ToList();
        }

        #endregion

        public override void LoadAnnotationsInRegion(VikingXNA.Scene scene)
        {
            //Store.LocationsByRegion.LoadSectionAnnotationsInRegion(scene.VisibleWorldBounds, scene.ScreenPixelSizeInVolume, this.SectionNumber, this.AddLocationsInRegionCallback);
            GridRectangle? VisibleMosaicBounds = scene.VisibleWorldBounds.ApproximateVisibleMosaicBounds(this.mapper);

            Store.LocationsByRegion.LoadSectionAnnotationsInRegion(VisibleMosaicBounds, scene.ScreenPixelSizeInVolume, this.SectionNumber, null, AddLocationsInLocalCache);// this.AddLocationsInRegionCallback);


            if (this.SectionAbove != null)
            {
                this.SectionAbove.LoadAnnotationsInRegion(scene);
            }

            if (this.SectionBelow != null)
            {
                this.SectionBelow.LoadAnnotationsInRegion(scene);
            }
        }

        /// <summary>
        /// Load 
        /// </summary>
        /// <param name="locationObjs"></param>
        protected override void AddLocationsInLocalCache(IEnumerable<LocationObj> locationObjs)
        {
            LocationObj[] unknownObjs = locationObjs.Where(l => !this.KnownLocations.Contains(l.ID)).ToArray();
            if (unknownObjs.Length > 0)
                AddLocationBatch(unknownObjs);
        }

        private void AddLocationsInRegionCallback(IEnumerable<LocationObj> locationObjs)
        {
            AddLocationBatch(locationObjs);
        }

        public override bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
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
                else if (sender.GetType() == typeof(StructureObj))
                {
                    OnStructurePropertyChanged(sender, PropertyChangedArgs);
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
                else if (sender.GetType() == typeof(StructureObj))
                {
                    OnStructurePropertyChanging(sender, PropertyChangingArgs);
                    return true;
                }
            }

            Debug.Fail("Weak Event not handled");
            return false;
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene,
                                BasicEffect basicEffect, VikingXNAGraphics.OverlayShaderEffect overlayEffect,
                                RoundLineCode.RoundLineManager overlayLineManager, RoundCurve.CurveManager overlayCurveManager
                                )
        {

        }
    }
}
