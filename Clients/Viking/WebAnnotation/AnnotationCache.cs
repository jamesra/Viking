using System;
using System.Collections.Generic;
using System.Collections.Concurrent; 
using System.Linq;
using System.Text;
using System.Diagnostics;
using WebAnnotationModel.Objects;
using Viking;
using Viking.Common;
using Geometry;
using WebAnnotation.UI;
using Viking.ViewModels; 

namespace WebAnnotation
{
    /// <summary>
    /// This static class stores a local copy of transformed annotations retrieved from the server
    /// This class being static poses problems because it means we can only have one section viewer open at a time. 
    /// At some point it should be switched to a non-static because static is providing no advantages anymore
    /// </summary>
    class AnnotationCache
    {
        //*****************************************************************************************
        //*****************************************************************************************
        //****************** THIS CLASS IS NOT USED *** IT IS FOR REFERENCE ONLY ******************
        //*****************************************************************************************
        //*****************************************************************************************

        /// <summary>
        /// The currently visible section
        /// <summary>
        public static SectionViewModel Section;

        /// <summary>
        /// Locations on the section we are providing an overlay for
        /// </summary>
        //private static SortedList<long, LocationObj> Locations = new SortedList<long, LocationObj>(0);
        private static ConcurrentDictionary<long, LocationObj> Locations
        {
            get
            {
                return Store.Locations.GetLocationsForSection(Section.Number); 
            }
        }

        /// <summary>
        /// Locations on the reference sections above and below our current section
        /// </summary>
        //private static SortedList<long, LocationObj> ReferenceLocations = new SortedList<long, LocationObj>(0);

        /// <summary>
        /// Maps a structureID to all the locations for that structure on the visible section
        /// </summary>
        private static ConcurrentDictionary<long, ConcurrentDictionary<long, LocationObj>> LocationsForStructure = new ConcurrentDictionary<long, ConcurrentDictionary<long, LocationObj>>(); 

        /// <summary>
        /// Locations are stored in section space.  If the section we are viewing has been warped to volume space 
        /// we cache the warped locations. Contains all locations including reference
        /// </summary>
        private static ConcurrentDictionary<long, GridVector2> TransformedLocationPositionDict = new ConcurrentDictionary<long, GridVector2>();

        /// <summary>
        /// A quad tree which maps a point to the nearest Location on any section
        /// </summary>
        private static QuadTree<long> TransformedRefLocationQuadTree = null;

        /// <summary>
        /// A quad tree which maps a point to the nearest Location on this section
        /// </summary>
        private static QuadTree<long> TransformedLocationQuadTree = null;

        /// <summary>
        /// This lock must be taken any time a datastructure is accessed.  The Add/Remove locations calls can be invoked Asynch
        /// </summary>
        private static object LockObject = new object(); 

//        public static SortedDictionary<long, SortedList<long, RoundLineCode.RoundLine> > LocationLinesDict = null;

        /// <summary>
        /// Allows us to describe all the locationlinks visible on a screen
        /// </summary>
        private static LineSearchGrid<LocationLinkObj> LocationLinksSearch = null;

        /// <summary>
        /// Allows us to describe all the StructureLinks visible on a screen
        /// </summary>
        private static LineSearchGrid<StructureLinkObj> StructureLinksSearch = null;

        /// <summary>
        /// Fired when an annotation has changed.  Overlays should refresh thier display
        /// </summary>
        public static event EventHandler AnnotationChanged;

        private static EventHandler LocationLinkDeletedEventHandler;
        private static EventHandler StructureLinkDeletedEventHandler;

        /// <summary>
        /// This is a symptom of the annotation cache being static when it shouldn't be.  This is a pointer to the 
        /// parent section viewer control which can perform transforms
        /// </summary>
        public static Viking.UI.Controls.SectionViewerControl parent;

        /*
        static AnnotationCache()
        {
            Store.Locations.OnAddUpdateRemoveKey += new AddUpdateRemoveKeyEventHandler(AnnotationCache.OnStoreAddRemoveKey);
            Store.Locations.OnAllUpdatesCompleted += new OnAllUpdatesCompletedEventHandler(AnnotationCache.OnAllUpdatesCompleted);

            LocationLinkDeletedEventHandler = new EventHandler(OnLocationLinkDeleted);
            StructureLinkDeletedEventHandler = new EventHandler(OnStructureLinkDeleted);
        }
        */

        #region Cache updates

        //Called when a key is added or removed from the store
        static protected void OnStoreAddRemoveKey(object sender, AddUpdateRemoveKeyEventArgs e)
        {
            LocationObj obj = sender as LocationObj;

            switch (e.ChangeAction)
            {
                case AddUpdateRemoveKeyEventArgs.Action.ADD:
                    Debug.Assert(obj != null); //Why is location store sending out a null object when it shouldn't?
                    if (obj == null)
                        return;

                    AddLocation(obj);
                    break;

                case AddUpdateRemoveKeyEventArgs.Action.UPDATE:
                    Debug.Assert(obj != null); //Why is location store sending out a null object when it shouldn't?
                    if (obj == null)
                        return;

                    AddLocation(obj);
                    break;
                
                case AddUpdateRemoveKeyEventArgs.Action.REMOVE:
                    RemoveLocation(e.ID);
                    break;

                default:
                    Debug.Assert(false, "Unexpected change action in OnStoreAddRemoveKey");
                    break; 
            }
        }

        private static void ClearLocationLinks()
        {
            GridLineSegment[] lines = LocationLinksSearch.Lines;
            foreach (GridLineSegment l in lines)
            {
                LocationLinkObj obj = LocationLinksSearch[l];
                obj.OnAfterDelete -= LocationLinkDeletedEventHandler;
                LocationLinksSearch.Remove(l); 
            }

            LocationLinksSearch.Clear(); 
        }

        private static void ClearStructureLinks()
        {
            GridLineSegment[] lines = StructureLinksSearch.Lines;
            foreach (GridLineSegment l in lines)
            {
                StructureLinkObj obj = StructureLinksSearch[l];
                obj.AfterDelete -= StructureLinkDeletedEventHandler;
                StructureLinksSearch.Remove(l);
            }

            StructureLinksSearch.Clear(); 
        }
        
        //This is called when all locations in a query to the  locations store have been updated.
        static protected void OnAllUpdatesCompleted(object sender, OnAllUpdatesCompletedEventArgs e)
        {
            Trace.WriteLine("OnAllUpdatesCompleted", "WebAnnotation");

            //Don't update if it isn't the section we are viewing
            if (e.SectionNumber.HasValue)
            {
                if (e.SectionNumber != Section.Number)
                    return;
            }

            lock (LockObject)
            {
                ClearLocationLinks();

                foreach (LocationObj loc in Locations.Values)
                {
                    AddLocationLinks(loc);
                }

                ClearStructureLinks(); 

                foreach (long structID in LocationsForStructure.Keys)
                {
                    StructureObj obj = Store.Structures.GetObjectByID(structID);
                    if (obj == null)
                        continue;

                    AddStructureLinks(obj); 
                }
            }

            //Store.Locations.Save(); 

            Trace.WriteLine("End OnAllUpdatesCompleted", "WebAnnotation"); 
        }

        /// <summary>   
        ///  A key is about to be added to the location store or we have retrieved a key from the 
        ///  location store and want to add it to our cache.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static protected void AddLocation(LocationObj obj)
        {
            //Trace.WriteLine("AddLocation: " + obj.ToString(), "WebAnnotation");
           // lock (LockObject)
            {
                //Add the location to our mapping if the location is on our section
                if (obj.Section == AnnotationCache.Section.Number)
                {
                    GridVector2 VolumePosition = new GridVector2(-1, -1);
                    try
                    {
                        VolumePosition = parent.SectionToVolume(new GridVector2(obj.X, obj.Y));
                    }
                    catch (ArgumentException except) //Remove locations we can't map
                    {
                        /*PORT Concurrent Collections
                        if (Locations.ContainsKey(obj.ID))
                            Locations.Remove(obj.ID);
                        */
                        GridVector2 junkVal;
                        TransformedLocationPositionDict.TryRemove(obj.ID, out junkVal);
                        TransformedLocationQuadTree.TryRemove(obj.ID);

                        return;
                    }
                    finally
                    {
                        obj.VolumeX = VolumePosition.X;
                        obj.VolumeY = VolumePosition.Y;
                    }

                    //Add location if it hasn't been seen before

                    bool Added = TransformedLocationPositionDict.TryAdd(obj.ID, VolumePosition);

                    if (Added)
                    {
                        TransformedLocationQuadTree.TryAdd(VolumePosition, obj.ID);
                    }
                    else
                    {
                        //We had this location already, update it
                        //Update the position, then move on
                        if (TransformedLocationPositionDict[obj.ID] != VolumePosition)
                        {
                            //Remove from our data structures, update position, and then re-add
                            RemoveLocationLinks(obj);
                            //This is a temporary hack to deal with the ID number change when inserting values into the database
                            TransformedLocationQuadTree.TryRemove(obj.ID);

                            TransformedLocationPositionDict[obj.ID] = VolumePosition;

                            TransformedLocationQuadTree.Add(VolumePosition, obj.ID);
                        }
                    }

                    ConcurrentDictionary<long, LocationObj> KnownLocationsForStructure;
                    KnownLocationsForStructure = LocationsForStructure.GetOrAdd(obj.ParentID.Value, new ConcurrentDictionary<long, LocationObj>());
                    KnownLocationsForStructure.TryAdd(obj.ID, obj);

                    AddLocationLinks(obj);
                }
                else
                {

                    Viking.VolumeModel.Section ReferenceSection = null;
                    if (AnnotationCache.Section.ReferenceSectionAbove != null)
                    {
                        if (obj.Section == AnnotationCache.Section.ReferenceSectionAbove.Number)
                            ReferenceSection = AnnotationCache.Section.ReferenceSectionAbove;
                    }
                    if (AnnotationCache.Section.ReferenceSectionBelow != null)
                    {
                        if (obj.Section == AnnotationCache.Section.ReferenceSectionBelow.Number)
                            ReferenceSection = AnnotationCache.Section.ReferenceSectionBelow;
                    }

                    if (ReferenceSection != null)
                    {
                        GridVector2 VolumePosition = new GridVector2(-1, -1);

                        try
                        {
                            VolumePosition = parent.SectionToVolume(new GridVector2(obj.X, obj.Y),
                                                                    ReferenceSection);
                        }
                        catch (ArgumentOutOfRangeException except) //Remove locations we can't map
                        {
                            GridVector2 outParam;
                            TransformedLocationPositionDict.TryRemove(obj.ID, out outParam);
                            TransformedRefLocationQuadTree.TryRemove(obj.ID);

                            return;
                        }
                        finally
                        {

                            obj.VolumeX = VolumePosition.X;
                            obj.VolumeY = VolumePosition.Y;
                        }

                        bool AddSuccess = TransformedLocationPositionDict.TryAdd(obj.ID, VolumePosition);
                        TransformedRefLocationQuadTree.TryAdd(VolumePosition, obj.ID);

                        if (AddSuccess == false)
                        {
                            if (TransformedLocationPositionDict[obj.ID] != VolumePosition)
                            {
                                TransformedLocationPositionDict[obj.ID] = VolumePosition;
                                //This is a temporary hack to deal with the ID number change when inserting values into the database
                                TransformedRefLocationQuadTree.TryRemove(obj.ID);
                                TransformedRefLocationQuadTree.Add(VolumePosition, obj.ID);
                            }
                        }

                        AddLocationLinks(obj);
                    }
                }
            }

            if (AnnotationChanged != null)
                AnnotationChanged(obj, new EventArgs());

            //            Trace.WriteLine("End AddLocation: " + obj.ToString(), "WebAnnotation");
        }

        /// <summary>
        /// A key is about to be removed from the location store.  Remove it from our cache as well
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static protected void RemoveLocation(long ID)
        {
            LocationObj obj = null;

            lock (LockObject)
            {
                Trace.WriteLine("RemoveLocation: " + ID.ToString(), "WebAnnotation");

                obj = Store.Locations.GetObjectByID(ID);

                if (obj != null)
                {
                    if (obj.Z == Section.Number)
                    {
                        bool RemoveSuccess = TransformedLocationQuadTree.TryRemove(obj.ID);
                        if(RemoveSuccess)
                        {
                            RemoveLocationLinks(obj);
                        }
                    }
                    else
                    {
                        TransformedRefLocationQuadTree.TryRemove(obj.ID);
                    }
                    
                    ConcurrentDictionary<long, LocationObj> KnownLocationsForStructure = null;
                    bool Success = LocationsForStructure.TryGetValue(obj.ParentID.Value, out  KnownLocationsForStructure);
                    if(Success)
                    {
                        LocationObj removedLoc;
                        Success = KnownLocationsForStructure.TryRemove(ID, out removedLoc);

                        if(Success)
                        {
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
                }

                GridVector2 removeOutParam;
                TransformedLocationPositionDict.TryRemove(ID, out removeOutParam); 
            }

            if (AnnotationChanged != null && obj != null)
                AnnotationChanged(obj, new EventArgs());

            Trace.WriteLine("End RemoveLocation: " + ID.ToString(), "WebAnnotation");
        }

        #endregion 

        #region Queries

        public static LocationObj[] GetLocations()
        {
            lock (LockObject)
            {
                return Locations.Values.ToArray(); 
            }
        }

        public static LocationObj[] GetReferenceLocations()
        {
            List<LocationObj> listRefLocations = new List<LocationObj>(); 

            if (Section.ReferenceSectionAbove != null)
            {
                listRefLocations.AddRange(Store.Locations.GetLocationsForSection(Section.ReferenceSectionAbove.Number).Values);
            }

            if (Section.ReferenceSectionBelow != null)
            {
                listRefLocations.AddRange(Store.Locations.GetLocationsForSection(Section.ReferenceSectionBelow.Number).Values);
            }

            return listRefLocations.ToArray(); 
        }

        /// <summary>
        /// Returns the position of the requested locationID in the current transform
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        public static GridVector2 GetPositionForLocation(long ID)
        {
            GridVector2 position;
            bool Success;
            Success = TransformedLocationPositionDict.TryGetValue(ID, out position);

            if (!Success)
            {
                //Hmm... why don't we have it?
                Trace.WriteLine("Could not find position for location: " + ID.ToString()); 

            }

            return position;
        }

        static public IUIObjectBasic GetNearestAnnotation(GridVector2 WorldPosition, out double distance)
        {
            distance = double.MaxValue; 
            IUIObjectBasic FoundObject = null;
            double locDistance = double.MaxValue; 
            LocationObj NearestLocationObj = GetNearestLocation(WorldPosition);
            if (NearestLocationObj != null)
            {
                locDistance = GridVector2.Distance(GetPositionForLocation(NearestLocationObj.ID), WorldPosition);
                FoundObject = NearestLocationObj as IUIObjectBasic;
                distance = locDistance;
            }

            double linkDistance;
            IUIObjectBasic FoundLink = GetNearestLink(WorldPosition, out linkDistance);
            if (FoundLink != null && linkDistance < locDistance)
            {
                FoundObject = FoundLink;
                distance = linkDistance; 
            }

            return FoundObject; 
        }

        static public IUIObjectBasic GetNearestLink(GridVector2 WorldPosition, out double distance)
        {
            double minDistance = double.MaxValue; 
            GridVector2 NearestIntersection;
            IUIObjectBasic FoundLink = null; 
            //LocationLinksSearch
            //StructureLinkSearch

            LocationLinkObj locLinkObj = LocationLinksSearch.GetNearest(WorldPosition, out NearestIntersection, out distance);
            if (locLinkObj != null)
            {
                if (distance > locLinkObj.Radius)
                {
                    locLinkObj = null;
                }
                else
                {
                    minDistance = distance;
                    FoundLink = locLinkObj as IUIObjectBasic;
                }
            }

            StructureLinkObj structLinkObj = StructureLinksSearch.GetNearest(WorldPosition, out NearestIntersection, out distance);
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
        }

        /// <summary>
        /// Gets the nearest location, preferring locations on the same section, then checking other sections
        /// </summary>
        /// <param name="WorldPosition"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="font"></param>
        /// <param name="locPosition"></param>
        /// <returns></returns>
        static public LocationObj GetNearestLocation(GridVector2 WorldPosition)
        {
            LocationObj BestLoc = null; 
            double distance;
            lock (LockObject)
            {

                LocationObj loc = null;
                double minDistance = double.MaxValue;

                if (TransformedLocationQuadTree == null)
                    return null; 

                /*Check to see if we clicked a location*/
                long BestLocID = AnnotationCache.TransformedLocationQuadTree.FindNearest(WorldPosition, out distance);
                if (BestLocID != default(long))
                {
                    BestLoc = Store.Locations.GetObjectByID(BestLocID);

                    if (BestLoc != null)
                    {
                        if (distance < BestLoc.Radius)
                        {
                            minDistance = distance;
                        }
                        else
                        {
                            BestLoc = null;
                        }
                    }
                }

                //If we're still here check locations on other sections
                long locID = AnnotationCache.TransformedRefLocationQuadTree.FindNearest(WorldPosition, out distance);
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

        /// <summary>
        /// Gets the nearest reference location
        /// </summary>
        /// <param name="WorldPosition"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="font"></param>
        /// <param name="locPosition"></param>
        /// <returns></returns>
        static public LocationObj GetNearestReferenceLocation(GridVector2 WorldPosition, bool MustBeWithinRadius)
        {
            LocationObj BestLoc = null;
            double distance;
            lock (LockObject)
            {

                LocationObj loc = null;
                double minDistance = double.MaxValue;

                //If we're still here check locations on other sections
                long locID = AnnotationCache.TransformedRefLocationQuadTree.FindNearest(WorldPosition, out distance);
                if (locID != default(long))
                {
                    loc = Store.Locations.GetObjectByID(locID);
                    if (loc != null)
                    {
                        minDistance = distance;
                        if (MustBeWithinRadius)
                        {
                            if (distance <= loc.OffSectionRadius)
                            {
                                BestLoc = loc;
                            }
                        }
                    }
                }
            }

            return BestLoc;
        }
        

        #endregion


        /// <summary>
        /// Load the annotations for the passed section and its reference sections
        /// </summary>
        /// <param name="section"></param>
        internal static void LoadSectionAnnotations(SectionViewModel section)
        {
            Trace.WriteLine("LoadSectionAnnotations: " + section.Number.ToString(), "WebAnnotation"); 
            lock (LockObject)
            {
                if (LocationsForStructure != null)
                {
                    LocationsForStructure.Clear(); 
                }

                if (TransformedLocationPositionDict != null)
                {
                    TransformedLocationPositionDict.Clear();
                }

                if (TransformedRefLocationQuadTree != null)
                {
                    TransformedRefLocationQuadTree = null;
                }

                if (TransformedLocationQuadTree != null)
                {
                    TransformedLocationQuadTree = null;
                }

                if (LocationLinksSearch != null)
                {
                    ClearLocationLinks();
                    LocationLinksSearch = null;
                }

                if (StructureLinksSearch != null)
                {
                    ClearStructureLinks(); 
                    StructureLinksSearch = null;
                }

                AnnotationCache.Section = section;
                
                if (Section == null)
                    return;
                
                //Create the datastructures
                GridRectangle bounds = QuadTreeBounds();

                /*PORT Concurrent Collections
                if (Locations == null)
                {
                    Locations = new SortedList<long, LocationObj>();
                }

                if (ReferenceLocations == null)
                {
                    ReferenceLocations = new SortedList<long, LocationObj>();
                }
                */

                if (LocationsForStructure == null)
                {
                    LocationsForStructure = new ConcurrentDictionary<long, ConcurrentDictionary<long, LocationObj>>();
                }
               
                TransformedRefLocationQuadTree = new QuadTree<long>(bounds);
                TransformedLocationQuadTree = new QuadTree<long>(bounds);

                bounds.Scale(.2);
                LocationLinksSearch = new LineSearchGrid<LocationLinkObj>(bounds, 10000);
                StructureLinksSearch = new LineSearchGrid<StructureLinkObj>(bounds, 10000);
            } //Have to let the Lock go before we call the location store or we can get a deadlock.  Don't modify 
              //data structures after this point. 


            //Load reference locations

            /* PORT Concurrent Collections
            List<LocationObj> RefLocationArray = new List<LocationObj>();

            if (section.ReferenceSectionBelow != null)
                RefLocationArray.AddRange(Store.Locations.GetLocationsForSection(section.ReferenceSectionBelow.Number));

            if (section.ReferenceSectionAbove != null)
                RefLocationArray.AddRange(Store.Locations.GetLocationsForSection(section.ReferenceSectionAbove.Number));

            //            ReferenceLocations = new SortedList<long, LocationObj>();
            */

            LocationObj[] RefLocations = GetReferenceLocations(); 
            foreach (LocationObj loc in RefLocations)
            {
                AddLocation(loc);
            }

            foreach (LocationObj loc in Locations.Values)
            {
                AddLocation(loc);
            }
            
//            PopulateLocationLinks();
        }

        /// <summary>
        /// Allocates a new quad tree based on the current section parameters
        /// </summary>
        private static GridRectangle QuadTreeBounds()
        {
            GridRectangle bounds;
            lock (LockObject)
            {
                //Figure out the new boundaries for our quad-tree
                bounds = parent.SectionBounds(Section.section);
                if (Section.ReferenceSectionAbove != null)
                {
                    bounds = GridRectangle.Union(bounds, parent.SectionBounds(Section.ReferenceSectionAbove));
                }
                if (Section.ReferenceSectionBelow != null)
                {
                    bounds = GridRectangle.Union(bounds, parent.SectionBounds(Section.ReferenceSectionBelow));
                }

                //Give ourselves extra room in case locations fall outside mappable space
                bounds.Scale(5);

            }

            
            return bounds; 
        }

        /// <summary>
        /// Return all the line segments visible in the passed bounds
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static LocationLinkObj[] VisibleLocationLinks(GridRectangle bounds)
        {
            if (LocationLinksSearch != null)
            {
                List<LocationLinkObj> LinkList = LocationLinksSearch.GetValues(new GridLineSegment(
                    new GridVector2(bounds.Left, bounds.Bottom),
                    new GridVector2(bounds.Right, bounds.Top)));

                return LinkList.ToArray();
            }

            return new LocationLinkObj[0]; 
            
        }

        /// <summary>
        /// Return all the line segments visible in the passed bounds
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static StructureLinkObj[] VisibleStructureLinks(GridRectangle bounds)
        {
            
            List<StructureLinkObj> LinkList = StructureLinksSearch.GetValues(new GridLineSegment(
                new GridVector2(bounds.Left, bounds.Bottom),
                new GridVector2(bounds.Right, bounds.Top)));
            return LinkList.ToArray();
        }

        /// <summary>
        /// All locations which are linked get a line between them
        /// </summary>
        internal static void AddLocationLinks(LocationObj obj)
        {
            //Only add objects in the same section
            if (obj.Z != Section.Number)
            {
                return;
            }

            foreach (long linkedID in obj.Links)
            {
                GridVector2 position;
                GridVector2 linkedPosition;

                bool success = TransformedLocationPositionDict.TryGetValue(obj.ID, out position);
                if (!success)
                    continue;

                success = TransformedLocationPositionDict.TryGetValue(linkedID, out linkedPosition);
                if (!success)
                    continue; 
                                        
                GridLineSegment lineSegment = new GridLineSegment(position, linkedPosition);
                LocationLinkObj locLink = new LocationLinkObj(obj, linkedID, lineSegment);

                locLink.OnAfterDelete += LocationLinkDeletedEventHandler;

                LocationLinkObj oldLink = null;
                success = LocationLinksSearch.TryRemove(lineSegment, out oldLink);
                if (oldLink != null)
                {
                    oldLink.OnAfterDelete -= LocationLinkDeletedEventHandler;
                }

                success = LocationLinksSearch.TryAdd(lineSegment, locLink);
            }
            
        }

        /// <summary>
        /// All locations which are linked get a line between them
        /// </summary>
        internal static void RemoveLocationLinks(LocationObj obj)
        {
            foreach (long linkedID in obj.Links)
            {
                GridVector2 position;
                GridVector2 linkedPosition;

                bool success = TransformedLocationPositionDict.TryGetValue(obj.ID, out position);
                if (!success)
                    continue;

                success = TransformedLocationPositionDict.TryGetValue(linkedID, out linkedPosition);
                if (!success)
                    continue; 
                    
                GridLineSegment lineSegment = new GridLineSegment(position, linkedPosition);
                LocationLinkObj oldLink = null;
                success = LocationLinksSearch.TryRemove(lineSegment, out oldLink);
                if(success)
                {
                    oldLink.OnAfterDelete -= LocationLinkDeletedEventHandler;
                }
            }
        }


        /// <summary>
        /// All locations which are linked get a line between them
        /// </summary>
        internal static void AddStructureLinks(StructureObj obj)
        {
            if (obj.Links.Length == 0 && obj.ParentID == null)
                return; 

            lock (LockObject)
            {
                //The link may have been created to a structure on an adjacent section
                ConcurrentDictionary<long, LocationObj> LocationsOnSection = null;
                bool Success = LocationsForStructure.TryGetValue(obj.ID, out LocationsOnSection);
                if (Success == false)
                    return;

                SortedList<long, GridVector2> SourcePositions = new SortedList<long, GridVector2>();
                
                foreach (long locID in LocationsOnSection.Keys)
                {
                    GridVector2 position;
                    Success = TransformedLocationPositionDict.TryGetValue(locID, out position);
                    if(Success)
                        SourcePositions.Add(locID, TransformedLocationPositionDict[locID]);
                }

                if (obj.ParentID != null)
                {
                    if (obj.ParentID.HasValue)
                    {
                        AddStructureLink(obj, SourcePositions, obj.ParentID.Value);
                    }
                }

    
                foreach (WebAnnotation.Service.StructureLink link in obj.Links)
                {
                    //Just look for links where this structure is the source, cuts search in half
                    if (link.TargetID == obj.ID)
                        continue;

                    AddStructureLink(obj, SourcePositions, link.TargetID); 
                    
                }
            }
        }

        
        private static void AddStructureLink(StructureObj SourceObj, SortedList<long, GridVector2> SourcePositions, long TargetStructID)
        {
            ConcurrentDictionary<long, LocationObj> LinkedLocationsOnSection; 
            bool success = LocationsForStructure.TryGetValue(TargetStructID, out LinkedLocationsOnSection);
            if(success == false)
                return; 

            SortedList<long, GridVector2> TargetPositions = new SortedList<long, GridVector2>(LinkedLocationsOnSection.Count);

            foreach (long locID in LinkedLocationsOnSection.Keys)
            {
                GridVector2 position; 
                bool Success = TransformedLocationPositionDict.TryGetValue(locID, out position);
                
                if(Success)
                    TargetPositions.Add(locID, position);
            }

            long BestSourceLocID = -1;
            long BestTargetLocID = -1;
            GridVector2 Origin = new GridVector2();
            GridVector2 Destination = new GridVector2();
            double MinDistance = double.MaxValue;

            //Brute force a search for the shortest distance between the two structures.
            foreach (long SourceID in SourcePositions.Keys)
            {
                GridVector2 SourcePos = SourcePositions[SourceID];

                foreach (long TargetID in TargetPositions.Keys)
                {
                    GridVector2 TargetPos = TargetPositions[TargetID];

                    double dist = GridVector2.Distance(SourcePos, TargetPos);
                    if (dist < MinDistance)
                    {
                        BestSourceLocID = SourceID;
                        BestTargetLocID = TargetID;
                        Origin = SourcePos;
                        Destination = TargetPos;
                        MinDistance = dist;
                    }
                }
            }

            //Could not find a pair
            if (MinDistance == double.MaxValue)
                return;

            StructureObj TargetStruct = Store.Structures.GetObjectByID(TargetStructID);
            GridLineSegment lineSegment = new GridLineSegment(Origin, Destination);
            StructureLinkObj StructLink = new StructureLinkObj(SourceObj.ID, TargetStructID,
                                                               Locations[BestSourceLocID],
                                                               Locations[BestTargetLocID],
                                                               lineSegment);

            StructLink.AfterDelete += StructureLinkDeletedEventHandler;

            if (StructureLinksSearch.Contains(lineSegment))
            {
               StructureLinkObj oldLink = StructureLinksSearch[lineSegment];
               oldLink.AfterDelete -= StructureLinkDeletedEventHandler;
               StructureLinksSearch.Remove(lineSegment);
            }

            StructureLinksSearch.Add(lineSegment, StructLink);
        }

        private static void OnLocationLinkDeleted(object sender, EventArgs e)
        {
            LocationLinkObj linkObj = sender as LocationLinkObj;
            if (linkObj == null)
                return; 

            lock (LockObject)
            {
                if (LocationLinksSearch.Contains(linkObj))
                {
                    GridLineSegment line = LocationLinksSearch[linkObj];
                    LocationLinksSearch.Remove(line); 
                }
            }
        }

        private static void OnStructureLinkDeleted(object sender, EventArgs e)
        {
            StructureLinkObj linkObj = sender as StructureLinkObj;
            if (linkObj == null)
                return; 

            lock (LockObject)
            {
                
                if (StructureLinksSearch.Contains(linkObj))
                {
                    GridLineSegment line = StructureLinksSearch[linkObj];
                    StructureLinksSearch.Remove(line);
                }
            }
        }
    }
}
