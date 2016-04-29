using System;
using System.Windows.Forms; 
using System.Collections.Generic;
using System.Collections.Specialized; 
using System.Linq;
using System.Text;
using System.Diagnostics;
using Viking; 
using Viking.Common;
using Viking.ViewModels;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Geometry;
using WebAnnotation.UI.Commands;
using WebAnnotation.ViewModel;
using WebAnnotation.View;
using WebAnnotationModel;
using connectomes.utah.edu.XSD.WebAnnotationUserSettings.xsd;
using System.Threading.Tasks;
using SqlGeometryUtils;
using VikingXNAGraphics;
using WebAnnotation.Actions;

namespace WebAnnotation
{
    [Viking.Common.SectionOverlay("Annotation")]
    class AnnotationOverlay : Viking.Common.ISectionOverlayExtension
    {
        static public float LocationTextScaleFactor = 5;
        static public float ReferenceLocationTextScaleFactor = 2.5f;
         
        Viking.UI.Controls.SectionViewerControl _Parent;
        public Viking.UI.Controls.SectionViewerControl Parent { get { return _Parent; } }

        protected TransformChangedEventHandler SectionChangedEventHandler;
        private EventHandler AnnotationChangedEventHandler;

        //private static SectionLocationViewModelCache cacheSectionAnnotations = new SectionLocationViewModelCache(); 
        private static SectionAnnotationsViewModelCache cacheSectionAnnotations = new SectionAnnotationsViewModelCache();
        //private static LocationLinksViewModel linksView;

        private static AnnotationOverlay _CurrentOverlay = null; 
        public static AnnotationOverlay CurrentOverlay { get { return _CurrentOverlay;}}

        GridVector2 LastMouseDownCoords;
        GridVector2 LastMouseMoveVolumeCoords;

        /// <summary>
        /// The last object the mouse was over, if any
        /// </summary>
        internal static ICanvasView LastMouseOverObject = null;

        private MouseOverLocationCanvasViewEffect mouseOverEffect = new MouseOverLocationCanvasViewEffect();

        static AnnotationOverlay()
        {
            cacheSectionAnnotations.MaxCacheSize = Global.NumSectionsInMemory;
        }

        public AnnotationOverlay()
        {
            SectionChangedEventHandler = new TransformChangedEventHandler(this.OnSectionTransformChanged);

            AnnotationChangedEventHandler = new EventHandler(OnAnnotationChanged);

            Store.Locations.OnCollectionChanged += new NotifyCollectionChangedEventHandler(OnLocationCollectionChanged);
            Store.LocationLinks.OnCollectionChanged += new NotifyCollectionChangedEventHandler(OnLocationLinksCollectionChanged);

            //    AnnotationCache.AnnotationChanged += AnnotationChangedEventHandler;      
            _CurrentOverlay = this;
        }

        string Viking.Common.ISectionOverlayExtension.Name()
        {
            return Global.EndpointName; 
        } 

        int Viking.Common.ISectionOverlayExtension.DrawOrder()
        {
            return 10;
        }
        
        static public void  GoToLocation(LocationObj loc)
        {
            if(loc == null)
                return; 

            //Adjust downsample so the location fits nicely in the view
            double downsample = (loc.MosaicShape.Envelope().Width / Viking.UI.State.ViewerForm.Width) * Global.DefaultLocationJumpDownsample; 

            //SectionViewerForm.Show(section);
            Viking.UI.State.ViewerForm.GoToLocation(new Microsoft.Xna.Framework.Vector2((float)loc.Position.X, (float)loc.Position.Y), (int)loc.Z, true, downsample);

            //BUG: This is because the way I handle commands changed dramatically between Plantmap and Viking.  I need to
            //set selected object to null to keep the UI from doing strange things
            Viking.UI.State.SelectedObject = null; 
        }

        public int CurrentSectionNumber
        {
            get
            { 
                return _Parent.Section.Number; 
            }
        }

        /// <summary>
        /// Returns annotations for section if they exist or null if they do not
        /// </summary>
        /// <param name="SectionNumber"></param>
        public static SectionAnnotationsView GetAnnotationsForSection(int SectionNumber)
        {
            return cacheSectionAnnotations.Fetch(SectionNumber); 

            /*
            if (dictSectionAnnotations.ContainsKey(SectionNumber))
            {
                return dictSectionAnnotations[SectionNumber];
            }

            return null; 
            */
        }
        
        /// <summary>
        /// Returns annotations for section if they exist or creates new SectionLocationsViewModel if they do not
        /// </summary>
        /// <param name="SectionNumber"></param>
        public static SectionAnnotationsView GetOrCreateAnnotationsForSection(int SectionNumber, 
                                                                                 Viking.UI.Controls.SectionViewerControl parent)
        {
            if (parent.Section.VolumeViewModel.SectionViewModels.ContainsKey(SectionNumber))
            {
                SectionAnnotationsView SectionAnnotations = cacheSectionAnnotations.Fetch(SectionNumber);
                if (SectionAnnotations != null)
                    return SectionAnnotations; 
                                
                SectionAnnotations = new SectionAnnotationsView(parent.Section.VolumeViewModel.SectionViewModels[SectionNumber]);

                SectionAnnotationsView retVal = cacheSectionAnnotations.GetOrAdd(SectionNumber, SectionAnnotations);

                //If we did add a new view model to the cache, then subscribe to events and reduce cache footprint if needed
                if (object.ReferenceEquals(retVal, SectionAnnotations))
                {
                    cacheSectionAnnotations.ReduceCacheFootprint(null);
                }
                else
                {
                    //Otherwise make the duplicate SectionLocationsViewModel go away 
                    SectionAnnotations = null;
                }

                return retVal;                 
            }

            return null; 
        }
        
        

        public SectionAnnotationsView CurrentSectionAnnotations
        {
            get
            {
                return GetOrCreateAnnotationsForSection(_Parent.Section.Number, Parent);
            }
        }

        /// <summary>
        /// Returns the location nearest to the mouse, prefers the locations on the current section
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public LocationCanvasView GetNearestLocation(GridVector2 position, out double BestDistance)
        {
            BestDistance = double.MaxValue;
            SectionAnnotationsView locView = GetAnnotationsForSection(CurrentSectionNumber);
            if (locView == null)
                return null;

            LocationCanvasView bestObj;
            bestObj = locView.GetNearestLocation(position, out BestDistance);

            LocationCanvasView adjacentObj;
            double distance;

            if (_Parent.Section.ReferenceSectionAbove != null)
            {
                locView = GetAnnotationsForSection(_Parent.Section.ReferenceSectionAbove.Number);
                if (locView != null)
                {
                    adjacentObj = locView.GetNearestLocation(position, out distance);
                    if (adjacentObj != null)
                    {
                        if (distance < BestDistance)
                        {
                            bestObj = adjacentObj;
                            BestDistance = distance;
                        }
                    }
                }
            }

            if (_Parent.Section.ReferenceSectionBelow != null)
            {
                locView = GetAnnotationsForSection(_Parent.Section.ReferenceSectionBelow.Number);
                if (locView != null)
                {
                    adjacentObj = locView.GetNearestLocation(position, out distance);
                    if (adjacentObj != null)
                    {
                        if (distance < BestDistance)
                        {
                           bestObj = adjacentObj;
                           BestDistance = distance;
                        }
                    }
                }
            }

            return bestObj; 
        }

        /// <summary>
        /// Find the location nearest the provided point on the section, using annotation locations on the screen, not anatomical positions
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public IUIObjectBasic ObjectAtPosition(GridVector2 position, out double distance)
        {
            double MaxScreenDimension = Math.Max(Parent.Scene.VisibleWorldBounds.Width, Parent.Scene.VisibleWorldBounds.Height);
            distance = double.MaxValue;
            SectionAnnotationsView locView = GetAnnotationsForSection(CurrentSectionNumber);
            if (locView == null)
                return null; 

            double BestDistance = double.MaxValue;
            ICanvasView bestObj = null; 
           
            bestObj = locView.GetAnnotationAtPosition(position, out BestDistance);

            if (bestObj != null)
            {
                distance = BestDistance;
                LocationCanvasView loc = bestObj as LocationCanvasView;
                if (loc == null)
                    return bestObj as IUIObjectBasic;
    
                //if (loc.OverlappingLinks.Count == 0)
                //    return bestObj;

                ICanvasViewContainer container = loc as ICanvasViewContainer;
                if (container != null)
                {
                    bestObj = container.GetAnnotationAtPosition(position, out distance);
                    return bestObj as IUIObjectBasic;
                }
            }

            /*
            if (bestObj == null)
            {
                double bestRatio = double.MaxValue;
                double distanceRatio = double.MaxValue;
                List<LocationCanvasView> adjacentObjs = new List<LocationCanvasView>();
                if (_Parent.Section.ReferenceSectionAbove != null)
                {
                    locView = GetAnnotationsForSection(_Parent.Section.ReferenceSectionAbove.Number);
                    if (locView != null)
                    {
                        adjacentObjs.AddRange(locView.GetAdjacentLocations(position).Where(l => l.IsVisible(Parent.Scene)));
                    }
                }


                if (_Parent.Section.ReferenceSectionBelow != null)
                {
                    locView = GetAnnotationsForSection(_Parent.Section.ReferenceSectionBelow.Number);
                    if (locView != null)
                    {
                        adjacentObjs.AddRange(locView.GetAdjacentLocations(position).Where(l => l.IsVisible(Parent.Scene)));
                    }
                }

                //locView.GetLocations(Store.Locations.GetObjectsByIDs(adjacentObjs.SelectMany(a => a.Links));


                
                IEnumerable<LocationCanvasView> intersecting_candidates = FindNonOverlappedAdjacentLocations(locView.GetLocations().ToList(), adjacentObjs, this.CurrentSectionNumber);
                intersecting_candidates = adjacentObjs.Where(l => l.Intersects(position) && l.IsTerminal == false);
                LocationCanvasView nearest = intersecting_candidates.OrderBy(c => c.DistanceFromCenterNormalized(position)).FirstOrDefault();
                bestObj = nearest;
                if (bestObj != null)
                {
                    distance = GridVector2.Distance(position, nearest.VolumePosition);
                }
            }

            //Only check for links if there are no annotations in range
            if (bestObj == null)
            {
                IUIObjectBasic bestLink;
                bestLink = linksView.GetNearestLink(CurrentSectionNumber, position, out distance);
                if (bestLink != null)
                {
                    BestDistance = distance;
                    return bestLink;
                }
            }

            distance = BestDistance; 
            */
            return bestObj as IUIObjectBasic;
        }

        #region ISectionOverlayExtension Members

        public void SetParent(Viking.UI.Controls.SectionViewerControl parent)
        {
            //I'm only expecting this to be set once
            Debug.Assert(_Parent == null, "Not expecting parent to be set twice, OK to ignore, but annotation display may be incorrect"); 
            this._Parent = parent;
              
            //Load the locations for the current sections
            this._Parent.OnSectionChanged += new SectionChangedEventHandler(this.OnSectionChanged);
            Viking.UI.State.volume.TransformChanged  += new TransformChangedEventHandler(this.OnVolumeTransformChanged); 
            this._Parent.OnReferenceSectionChanged += new EventHandler(this.OnReferenceSectionChanged);

            this._Parent.MouseDown += new MouseEventHandler(this.OnMouseDown);
            this._Parent.MouseMove += new MouseEventHandler(this.OnMouseMove);
            this._Parent.MouseUp += new MouseEventHandler(this.OnMouseUp); 
            this._Parent.KeyDown += new KeyEventHandler(this.OnKeyDown);
            this._Parent.KeyUp += new KeyEventHandler(this.OnKeyUp);

           
            //linksView = new LocationLinksViewModel(parent); 

            LoadSectionAnnotations();
        }

        protected void UpdateMouseCursor()
        {
            LocationCanvasView loc = LastMouseOverObject as LocationCanvasView; // GetNearestLocation(WorldPosition, out distance);
            if (loc != null)
            {
                LocationAction action;
                GridVector2 WorldPosition = this.LastMouseMoveVolumeCoords;
                if (Control.ModifierKeys == Keys.Shift)
                    action = loc.GetMouseShiftClickActionForPositionOnAnnotation(WorldPosition, this.CurrentSectionNumber);
                else if (Control.ModifierKeys == Keys.Control)
                    action = loc.GetMouseControlClickActionForPositionOnAnnotation(WorldPosition, this.CurrentSectionNumber);
                else
                    action = loc.GetMouseClickActionForPositionOnAnnotation(WorldPosition, this.CurrentSectionNumber);

                _Parent.Cursor = action.GetCursor();
            }
            else
            {
                _Parent.Cursor = Cursors.Default;
            }
        }
                 
        protected void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_Parent.CurrentCommand == null)
                return; 

            //Check if there is a non-default command. we don't want to mess with another active command
            if (_Parent.CurrentCommand.GetType() != typeof(Viking.UI.Commands.DefaultCommand) ||
             Viking.UI.Commands.Command.QueueDepth > 0)
                return;

            //Buttons being pushed means we are in the middle of a default command, probably scrolling, which won't affect the selection
            if (e.Button != MouseButtons.None)
                return; 

            //If locations aren't visible they can't be selected
            if (!_Parent.ShowOverlays)
            {
                _Parent.Cursor = Cursors.Default;
                return;
            }

            double distance;
            GridVector2 WorldPosition = _Parent.ScreenToWorld(e.X, e.Y);
            this.LastMouseMoveVolumeCoords = WorldPosition;

            ICanvasView NextMouseOverObject = ObjectAtPosition(WorldPosition, out distance) as ICanvasView;
            if (NextMouseOverObject != LastMouseOverObject)
            {
                mouseOverEffect.viewObj = NextMouseOverObject;
                
                Parent.Invalidate();
            }   

            LastMouseOverObject = NextMouseOverObject;

            UpdateMouseCursor();
        }

        protected void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (_Parent.CurrentCommand == null)
                return;

            //Check if there is a non-default command. we don't want to mess with another active command
            if (_Parent.CurrentCommand.GetType() != typeof(Viking.UI.Commands.DefaultCommand) ||
                Viking.UI.Commands.Command.QueueDepth > 0)
                return; 
            
            //If locations aren't visible they can't be selected
            if (!_Parent.ShowOverlays)
                return;
            
            GridVector2 WorldPosition = _Parent.ScreenToWorld(e.X, e.Y);
            LastMouseDownCoords = WorldPosition; 

            //Left mouse button selects objects
            if (e.Button == MouseButtons.Left)
            { 
                double distance;
                IUIObjectBasic obj = ObjectAtPosition(WorldPosition, out distance);
                //Figure out if it is resizing a location circle
                //If the loc is on this section we check if we are close to the edge and we are resizing.  Everyone else gets standard location command
                Viking.UI.State.SelectedObject = obj as IUIObjectBasic;

                /*If we select a link, find the location off the section and assume we have selected that*/
                LocationCanvasView loc = obj as LocationCanvasView;

                if (loc != null)
                {
                    LocationAction action;
                    if (Control.ModifierKeys == Keys.Shift)
                        action = loc.GetMouseShiftClickActionForPositionOnAnnotation(WorldPosition, this.CurrentSectionNumber);
                    else if (Control.ModifierKeys == Keys.Control)
                        action = loc.GetMouseControlClickActionForPositionOnAnnotation(WorldPosition, this.CurrentSectionNumber);
                    else
                        action = loc.GetMouseClickActionForPositionOnAnnotation(WorldPosition, this.CurrentSectionNumber);

                    Viking.UI.Commands.Command command = action.CreateCommand(Parent, Store.Locations.GetObjectByID(loc.ID), WorldPosition);
                    if(command != null)
                    {
                        _Parent.CurrentCommand = command;
                    }
                }
                else
                {
                    //Check if we can continue another annotation
                    OnContinueLastTrace(LastMouseDownCoords);
                }
            }
        }
        
        protected void OnMouseUp(object sender, MouseEventArgs e)
        {
        }

        protected void OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                //Refresh the annotations on F5
                case Keys.F5:
                    LoadSectionAnnotations();
                    return;
                case Keys.F3:
                    OnContinueLastTrace();
                    return;
                case Keys.Enter:
                    OnContinueLastTrace();
                    return; 
                case Keys.Back:
                    if (CreateNewLinkedLocationCommand.LastEditedLocation != null)
                    {
                        Parent.GoToLocation(new Microsoft.Xna.Framework.Vector2((float)CreateNewLinkedLocationCommand.LastEditedLocation.Position.X,
                                                                                (float)CreateNewLinkedLocationCommand.LastEditedLocation.Position.Y),
                                            (int)CreateNewLinkedLocationCommand.LastEditedLocation.Z,
                                            true,
                                            (double)((CreateNewLinkedLocationCommand.LastEditedLocation.Radius * 2) / Parent.Width) * 2); 

                    }
                    return;

                case Keys.I:
                    Viking.UI.Commands.Command.EnqueueCommand(typeof(PlacePolylineCommand), new object[] { Parent, new Microsoft.Xna.Framework.Color(1.0f,0f,0f,0.5f), this.LastMouseDownCoords, 16, null});
                    break;
                case Keys.J:
                    OnCreateStructure(34, new string[0], LocationType.OPENCURVE);
                    break;
                case Keys.O:
                    OnCreateStructure(34, new string[0], LocationType.CLOSEDCURVE);
                    break;
                case Keys.ShiftKey:
                    UpdateMouseCursor();
                    break;
                case Keys.ControlKey:
                    UpdateMouseCursor();
                    break;
            }

            try
            {
                //Search the list of hotkeys for a match
                if (Global.UserSettings != null)
                {
                    IEnumerable<Hotkey> matchingKeys = Global.UserSettings.Shortcuts.Hotkey.Where(h => h.KeyCode == e.KeyCode &&
                                                                                                       h.Shift == e.Shift && 
                                                                                                       h.Ctrl == e.Control &&
                                                                                                       h.Alt == e.Alt);
                    foreach (Hotkey h in matchingKeys)
                    {
                        //OK, we have a match, invoke the command
                        //Check if there is a non-default command. we don't want to mess with another active command
                        if (_Parent.CurrentCommand.GetType() != typeof(Viking.UI.Commands.DefaultCommand) ||
                            Viking.UI.Commands.Command.QueueDepth > 0)
                            return;

                        connectomes.utah.edu.XSD.WebAnnotationUserSettings.xsd.Action a = Global.UserSettings.Actions.Action.Where(action => action.Name == h.Action).SingleOrDefault();
                        if (a != null)
                        {
                            System.Type commandType;
                            object[] parameters;

                            a.ExecuteAction(out commandType, out parameters);
                            if (commandType != null)
                            {
                                Viking.UI.Commands.Command.EnqueueCommand(commandType, parameters);
                            }

                            return;
                        }

                        CreateStructureCommandAction comAction = Global.UserSettings.Actions.CreateStructureCommandAction.Where(action => action.Name == h.Action).SingleOrDefault();
                        if (comAction != null)
                        {
                            OnCreateStructure(System.Convert.ToInt64(comAction.TypeID), comAction.AttributeList, comAction.GetLocationType());

                            return;
                        }

                        ToggleStructureTagCommandAction tagStructureAction = Global.UserSettings.Actions.ToggleStructureTagCommandAction.Where(action => action.Name == h.Action).SingleOrDefault();
                        if (tagStructureAction != null)
                        {
                            OnToggleStructureTag(tagStructureAction.Tag);

                            return;
                        }

                        ToggleLocationTagCommandAction tagLocationAction = Global.UserSettings.Actions.ToggleLocationTagCommandAction.Where(action => action.Name == h.Action).SingleOrDefault();
                        if (tagLocationAction != null)
                        {
                            OnToggleLocationTag(tagLocationAction.Tag);

                            return;
                        }

                        ToggleLocationTerminalCommandAction tagToggleTerminalAction = Global.UserSettings.Actions.ToggleLocationTerminalCommandAction.Where(action => action.Name == h.Action).SingleOrDefault();
                        if (tagToggleTerminalAction != null)
                        {
                            OnToggleLocationTerminalTag();

                            return;
                        }

                        ChangeLocationAnnotationTypeAction tagChangeLocationAnnotationTypeAction = Global.UserSettings.Actions.ChangeLocationAnnotationTypeAction.Where(action => action.Name == h.Action).SingleOrDefault();
                        if (tagChangeLocationAnnotationTypeAction != null)
                        {
                            OnChangeLocationAnnotationType(tagChangeLocationAnnotationTypeAction.GetLocationType());

                            return;
                        }

                        /*
                        System.Type t = System.Type.GetType(Param.Type);
                        if(t == null)
                        {
                            Trace.WriteLine("Could not get type of hotkey command: " + h.Type);
                            return; 
                        }

                        if(t.IsValueType)
                        {
                            //paramaters[iParam] = System.Convert.ChangeType(Param.Value, t);
                        }
                        else
                        {
                           // paramaters[iParam] = Activator.CreateInstance(t, 
                        }
                    

                    //Viking.UI.Commands.Command.EnqueueCommand();
                     */

                    }
                }
            }
            catch(Exception except)
            {
                Trace.WriteLine("Error with hotkeys: " + except.ToString()); 
            }
             
        }

        protected void OnKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Space:

                    //Only load the annotations for any section once so we can't fire multiple requests by pounding the spacebar
                    LoadSectionAnnotations();
                    this._Parent.Invalidate();

                    break;
                case Keys.ControlKey:
                    UpdateMouseCursor();
                    break;
                case Keys.ShiftKey:
                    UpdateMouseCursor();
                    break;
            }
        }

       

        protected void OnCreateStructure(long TypeID, IEnumerable<string> attributes, LocationType AnnotationType)
        {
            StructureTypeObj typeObj = Store.StructureTypes.GetObjectByID(TypeID);
            if (typeObj != null)
            {
                StructureType type = new StructureType(typeObj);
                bool StructureNeedsParent = type.ParentID.HasValue;
                System.Drawing.Point ClientPoint = _Parent.PointToClient(System.Windows.Forms.Control.MousePosition);
                GridVector2 WorldPos = _Parent.ScreenToWorld(ClientPoint.X, ClientPoint.Y);
                GridVector2 SectionPos;
                bool success = Parent.Section.ActiveSectionToVolumeTransform.TryVolumeToSection(WorldPos, out SectionPos);
                Debug.Assert(success);
                if (!success)
                    return;

                
                StructureObj newStruct = new StructureObj(type.modelObj);
                LocationObj newLocation = new LocationObj(newStruct,
                                                Parent.Section.Number,
                                                AnnotationType);
                 
                
                if (attributes != null)
                {
                    foreach (string attrib in attributes)
                    {
                        newStruct.ToggleAttribute(attrib);
                    }
                }

                switch (AnnotationType)
                {
                    case LocationType.CIRCLE:
                        QueuePlacementCommandForCircleStructure(this.Parent,newLocation, WorldPos, SectionPos, type.Color, false);
                        break;
                    case LocationType.OPENCURVE:
                        newLocation.Radius = 8.0;
                        QueuePlacementCommandForOpenCurveStructure(this.Parent, newLocation, WorldPos, type.Color, false);
                        break;
                    case LocationType.CLOSEDCURVE:
                        newLocation.Radius = 8.0;
                        QueuePlacementCommandForClosedCurveStructure(this.Parent, newLocation, WorldPos, type.Color, false);
                        break;
                    default:
                        Trace.WriteLine("Could not find commands for annotation type: " + AnnotationType.ToString());
                        return;
                }

                if (StructureNeedsParent)
                {
                    //Enqueue extra command to select a parent
                    Viking.UI.Commands.Command.EnqueueCommand(typeof(LinkStructureToParentCommand), new object[] { Parent, newStruct, newLocation });
                }

                Viking.UI.Commands.Command.EnqueueCommand(typeof(CreateNewStructureCommand), new object[] { Parent, newStruct, newLocation });
            }
            else
                Trace.WriteLine("Could not find hotkey ID for type: " + TypeID.ToString()); 
        }
        
        public static void QueuePlacementCommandForCircleStructure(Viking.UI.Controls.SectionViewerControl Parent, LocationObj newLocation, GridVector2 worldPos, GridVector2 sectionPos, System.Drawing.Color typecolor, bool SaveToStore)
        {
            Viking.UI.Commands.Command.EnqueueCommand(typeof(ResizeCircleCommand), new object[] { Parent,
                    typecolor,
                    worldPos,
                    new ResizeCircleCommand.OnCommandSuccess((double radius) => {
                                    
                                    newLocation.TypeCode = LocationType.CIRCLE;
                                    newLocation.MosaicShape = SqlGeometryUtils.GeometryExtensions.ToCircle(sectionPos.X,
                                       sectionPos.Y,
                                       newLocation.Section,
                                       radius);
                                    newLocation.VolumeShape = SqlGeometryUtils.GeometryExtensions.ToCircle(worldPos.X,
                                        worldPos.Y,
                                        newLocation.Section,
                                        radius);
                                    newLocation.Radius = radius;

                                    if(SaveToStore)
                                        Store.Locations.Save();
                                     })});
        }

        public static void QueuePlacementCommandForOpenCurveStructure(Viking.UI.Controls.SectionViewerControl Parent, LocationObj newLocation, GridVector2 origin, System.Drawing.Color typecolor, bool SaveToStore)
        {
            /*
            public PlaceOpenCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
                                        */
            double LineWidth = 16.0;
            Viking.UI.Commands.Command.EnqueueCommand(typeof(PlaceCurveCommand), new object[] { Parent, typecolor, origin,  LineWidth, false,
                                                            new PlaceCurveCommand.OnCommandSuccess((GridVector2[] points) => {
                                                                    newLocation.TypeCode = LocationType.OPENCURVE;
                                                                    newLocation.Radius = LineWidth / 2.0;
                                                                    SetLocationShapeFromPointsInVolume(Parent.Section, newLocation, points);
                                                                    if(SaveToStore)
                                                                        Store.Locations.Save();
                                                            }) });
        }

        public static void QueuePlacementCommandForClosedCurveStructure(Viking.UI.Controls.SectionViewerControl Parent, LocationObj newLocation, GridVector2 origin, System.Drawing.Color typecolor, bool SaveToStore)
        {
            /*
            public PlaceOpenCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
                                        */
            double LineWidth = 16.0;
            Viking.UI.Commands.Command.EnqueueCommand(typeof(PlaceCurveCommand), new object[] { Parent, typecolor, origin, LineWidth, true,
                                                            new PlaceCurveCommand.OnCommandSuccess((GridVector2[] points) => {
                                                                    newLocation.TypeCode = LocationType.CLOSEDCURVE;
                                                                    newLocation.Radius = LineWidth / 2.0;
                                                                    SetLocationShapeFromPointsInVolume(Parent.Section, newLocation, points);
                                                                    if(SaveToStore)
                                                                        Store.Locations.Save();
                                                            }) });
        }

        public static void SetLocationShapeFromPointsInVolume(SectionViewModel Section, LocationObj location, GridVector2[] points)
        {
            GridVector2[] mosaic_points = Section.ActiveSectionToVolumeTransform.VolumeToSection(points);

            switch (location.TypeCode)
            {
                case LocationType.OPENCURVE:
                    location.MosaicShape = mosaic_points.ToPolyLine();
                    location.VolumeShape = points.ToPolyLine();
                    break;
                case LocationType.POLYLINE:
                    location.MosaicShape = mosaic_points.ToPolyLine();
                    location.VolumeShape = points.ToPolyLine();
                    break;
                case LocationType.POLYGON:
                    location.MosaicShape = mosaic_points.ToPolygon();
                    location.VolumeShape = points.ToPolygon();
                    break;
                case LocationType.CLOSEDCURVE:
                    location.MosaicShape = mosaic_points.ToPolygon();
                    location.VolumeShape = points.ToPolygon();
                    break;
            }
        }

        protected void OnToggleStructureTag(string tag)
        {
            if(LastMouseOverObject == null)
            {
                Trace.WriteLine("No mouse over object to toggle tag");
                return;
            }

            LocationCanvasView loc = LastMouseOverObject as LocationCanvasView; // GetNearestLocation(WorldPosition, out distance);
            if(loc == null)
            {
                Trace.WriteLine("No mouse over location to toggle tag");
                return;
            }
               
            Viking.UI.Commands.Command.EnqueueCommand(typeof(ToggleStructureTag), new object[] { this.Parent, Store.Structures[loc.ParentID.Value], tag});

            return; 
        }

        protected void OnToggleLocationTag(string tag)
        {
            if (LastMouseOverObject == null)
            {
                Trace.WriteLine("No mouse over object to toggle tag");
                return;
            }

            LocationCanvasView loc = LastMouseOverObject as LocationCanvasView; // GetNearestLocation(WorldPosition, out distance);
            if (loc == null)
            {
                Trace.WriteLine("No mouse over location to toggle tag");
                return;
            }

            Viking.UI.Commands.Command.EnqueueCommand(typeof(ToggleLocationTag), new object[] { this.Parent, Store.Locations[loc.ID], tag });

            return;
        }

        protected void OnToggleLocationTerminalTag()
        {
            if (LastMouseOverObject == null)
            {
                Trace.WriteLine("No mouse over object to toggle terminal");
                return;
            }

            LocationCanvasView loc = LastMouseOverObject as LocationCanvasView; // GetNearestLocation(WorldPosition, out distance);
            if (loc == null)
            {
                Trace.WriteLine("No mouse over location to toggle terminal");
                return;
            }

            //ToggleLocationIsTerminalCommand command = new ToggleLocationIsTerminalCommand(this.Parent, loc.modelObj);

            Viking.UI.Commands.Command.EnqueueCommand(typeof(ToggleLocationIsTerminalCommand), new object[] { this.Parent, Store.Locations[loc.ID] });

            return;
        }

        protected void OnChangeLocationAnnotationType(LocationType newLocType)
        {
            if (LastMouseOverObject == null)
            {
                Trace.WriteLine("No mouse over object to change location annotation type");
                return;
            }

            LocationCanvasView loc = LastMouseOverObject as LocationCanvasView; // GetNearestLocation(WorldPosition, out distance);
            if (loc == null)
            {
                Trace.WriteLine("No mouse over object to change location annotation type");
                return;
            }

            GridVector2 SectionPos;
            bool success = Parent.Section.ActiveSectionToVolumeTransform.TryVolumeToSection(LastMouseMoveVolumeCoords, out SectionPos);
            Debug.Assert(success);
            if (!success)
                return;

            switch (newLocType)
            {
                case LocationType.CIRCLE:
                    QueuePlacementCommandForCircleStructure(Parent, Store.Locations[loc.ID], LastMouseMoveVolumeCoords, SectionPos, loc.Parent.Type.Color, true);
                    break;
                case LocationType.OPENCURVE: 
                    QueuePlacementCommandForOpenCurveStructure(Parent, Store.Locations[loc.ID], LastMouseMoveVolumeCoords, loc.Parent.Type.Color, true);
                    break;
                case LocationType.CLOSEDCURVE: 
                    QueuePlacementCommandForClosedCurveStructure(Parent, Store.Locations[loc.ID], LastMouseMoveVolumeCoords, loc.Parent.Type.Color, true);
                    break; 
            }
        }

        protected void OnContinueLastTrace()
        {
            System.Drawing.Point ClientPoint = _Parent.PointToClient(System.Windows.Forms.Control.MousePosition);
            GridVector2 WorldPos = _Parent.ScreenToWorld(ClientPoint.X, ClientPoint.Y);
            OnContinueLastTrace(WorldPos); 
        }

        protected void OnContinueLastTrace(GridVector2 WorldPos)
        {
            if (CreateNewLinkedLocationCommand.LastEditedLocation != null)
            {
                if (CreateNewLinkedLocationCommand.LastEditedLocation.Z != this.CurrentSectionNumber)
                {
                    Viking.UI.Commands.Command command = LocationAction.CREATELINKEDLOCATION.CreateCommand(this.Parent, CreateNewLinkedLocationCommand.LastEditedLocation, WorldPos);
                    if (command != null)
                    {
                        _Parent.CurrentCommand = command;
                    }
                }
                /*
                LocationObj template = CreateNewLinkedLocationCommand.LastEditedLocation;
                if (template.Z != this.Parent.Section.Number)
                {
                    GridVector2 SectionPos;
                    bool success = Parent.Section.ActiveSectionToVolumeTransform.TryVolumeToSection(WorldPos, out SectionPos);
                    Debug.Assert(success);
                    if (!success)
                        return;

                    LocationObj newLoc = new LocationObj(CreateNewLinkedLocationCommand.LastEditedLocation.Parent,
                                        Parent.Section.Number,
                                        template.TypeCode);

                    if (template.TypeCode == LocationType.CIRCLE)
                    {
                        LocationCircleView newLocView = new LocationCircleView(newLoc, Parent.Section.ActiveSectionToVolumeTransform);

                        Viking.UI.Commands.Command.EnqueueCommand(typeof(ResizeCircleCommand), new object[] { Parent, template.Parent.Type.Color, WorldPos, new ResizeCircleCommand.OnCommandSuccess((double radius) => { newLoc.Radius = radius; }) });
                        Viking.UI.Commands.Command.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), new object[] { Parent, template, newLocView });
                    }

                    Viking.UI.State.SelectedObject = null;
                    CreateNewLinkedLocationCommand.LastEditedLocation = null; 
                }
                */
            }
        }

        /// <summary>
        /// This occurs when a new section is loaded and we need to fetch all locations from scratch
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void OnSectionChanged(object sender, SectionChangedEventArgs e)
        {
            e.OldSection.TransformChanged -= this.OnSectionTransformChanged;
            e.NewSection.TransformChanged += this.OnSectionTransformChanged;
            
            //Don't load annotations when flipping sections if the user is holding down space bar to hide them
            if (_Parent.ShowOverlays)
            { 
                LoadSectionAnnotations();
                Task.Factory.StartNew(() => Store.Locations.FreeExcessSections(Global.NumSectionsInMemory, Global.NumSectionsLoading));
            }
        }

        protected void OnAnnotationChanged(object sender, EventArgs e)
        {
            //Trigger redraw of screen
            _Parent.Invalidate(); 
        }

        private static SortedSet<int> ChangedSectionsInLocationCollection(NotifyCollectionChangedEventArgs e)
        {
            SortedSet<int> changedSections = new SortedSet<int>();

            if (e.NewItems != null)
            {
                changedSections = GetDistinctLocationSections(e.NewItems);
            }

            if (e.OldItems != null)
            {
                if (changedSections == null)
                    changedSections = GetDistinctLocationSections(e.OldItems);
                else
                    changedSections = new SortedSet<int>(changedSections.Union(GetDistinctLocationSections(e.OldItems)));
            }

            return changedSections;
        }

        /// <summary>
        /// Return the distinct set of sections the locationObjs exist on
        /// </summary>
        /// <returns></returns>
        private static SortedSet<int> GetDistinctLocationSections(System.Collections.IList listLocations)
        {
            SortedSet<int> changedSections = new SortedSet<int>();

            if (listLocations != null)
            {
                for (int iObj = 0; iObj < listLocations.Count; iObj++)
                {
                    LocationObj locNewObj = listLocations[iObj] as LocationObj;
                    if (!changedSections.Contains(locNewObj.Section))
                        changedSections.Add(locNewObj.Section);
                }
            }

            return changedSections;
        }

        private static SortedSet<int> ChangedSectionsInLocationLinkCollection(NotifyCollectionChangedEventArgs e)
        {
            SortedSet<int> changedSections = new SortedSet<int>();

            if (e.NewItems != null)
            {
                changedSections = GetDistinctLocationLinkSections(e.NewItems);
            }

            if (e.OldItems != null)
            {
                if (changedSections == null)
                    changedSections = GetDistinctLocationLinkSections(e.OldItems);
                else
                    changedSections = new SortedSet<int>(changedSections.Union(GetDistinctLocationLinkSections(e.OldItems)));
            }

            return changedSections;
        }

        /// <summary>
        /// Return the distinct set of sections the locationObjs exist on
        /// </summary>
        /// <returns></returns>
        private static SortedSet<int> GetDistinctLocationLinkSections(System.Collections.IList listObjs)
        {
            SortedSet<int> changedSections = new SortedSet<int>();

            if (listObjs != null)
            {
                for (int iObj = 0; iObj < listObjs.Count; iObj++)
                {
                    LocationLinkObj locLink = listObjs[iObj] as LocationLinkObj;
                    LocationObj locA = Store.Locations.GetObjectByID(locLink.A, false);
                    LocationObj locB = Store.Locations.GetObjectByID(locLink.B, false);

                    if (locA != null && !changedSections.Contains(locA.Section))
                        changedSections.Add(locA.Section);

                    if (locB != null && !changedSections.Contains(locB.Section))
                        changedSections.Add(locB.Section);
                }
            }

            return changedSections;
        }

        /// <summary>
        /// Organize the changes so we only call the SectionAnnotationViewModel objects that we have to.
        /// Can be called from any thread
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void OnLocationCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            SortedSet<int> changedSections = ChangedSectionsInLocationCollection(e);

            SortedSet<int> AdjacentSections = new SortedSet<int>();
            foreach (SectionViewModel svm in Viking.UI.State.volume.SectionViewModels.Values)
            {
                if(svm.ReferenceSectionAbove != null)
                {
                    if(changedSections.Contains(svm.ReferenceSectionAbove.Number))
                        AdjacentSections.Add(svm.Number);
                }

                if (svm.ReferenceSectionBelow != null)
                {
                    if(changedSections.Contains(svm.ReferenceSectionBelow.Number))
                        AdjacentSections.Add(svm.Number);
                }
            }
            
            foreach (int section in changedSections)
            {
                SectionAnnotationsView SLVModel = GetOrCreateAnnotationsForSection(section, this.Parent);
                if (SLVModel != null)
                {
                    SLVModel.OnLocationsStoreChanged(sender, e);
                }
            }

            foreach (int section in AdjacentSections)
            {
                if (!changedSections.Contains(section))
                {
                    SectionAnnotationsView SLVModel = GetOrCreateAnnotationsForSection(section, this.Parent);
                    if (SLVModel != null)
                    {
                        SLVModel.OnLocationsStoreChanged(sender, e);
                    }
                }
            }
            
            //Invalidate can always be called from any thread
            Parent.BeginInvoke(new System.Action( () => Parent.Invalidate()));
        }

        /// <summary>
        /// Organize the changes so we only call the SectionAnnotationViewModel objects that we have to.
        /// Can be called from any thread
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void OnLocationLinksCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            SortedSet<int> changedSections = ChangedSectionsInLocationLinkCollection(e);

            foreach (int section in changedSections)
            {
                SectionAnnotationsView SLVModel = GetOrCreateAnnotationsForSection(section, this.Parent);
                if (SLVModel != null)
                {
                    SLVModel.OnLocationLinksStoreChanged(sender, e);
                }
            }
        }

        /// <summary>
        /// When this occurs we need to update the reference locations
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnReferenceSectionChanged(object sender, EventArgs e)
        {
            ///This could be optimized, but it should be a rare event
            LoadSectionAnnotations();
        }

        /// <summary>
        /// When this occurs we should update the positions we draw the locations at. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnSectionTransformChanged(object sender, TransformChangedEventArgs e)
        {
            //AnnotationCache.TransformLocationsToVolume();
            //AnnotationCache.PopulateLocationLinks(); 
            LoadSectionAnnotations();
        }

        /// <summary>
        /// When this occurs we should update the positions we draw the locations at. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnVolumeTransformChanged(object sender, TransformChangedEventArgs e)
        {
            //AnnotationCache.TransformLocationsToVolume();
            //AnnotationCache.PopulateLocationLinks(); 
            LoadSectionAnnotations();
        }

        private GridRectangle LastVisibleWorldBounds;
        private double LastCameraDownsample;

        /// <summary>
        /// Return true if we should reload our annotations for a scene movement
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        private bool ShouldLoadAnnotationsForSceneMovement(VikingXNA.Scene scene)
        {
            return (LastVisibleWorldBounds != scene.VisibleWorldBounds);// &&
                   //(LastCameraDownsample == scene.Camera.Downsample);
        }

        /// <summary>
        /// Record the last scene we rendered so we know if we've moved the camera and need to load new annotations
        /// </summary>
        private void UpdateSceneHistory(VikingXNA.Scene scene)
        {
            LastCameraDownsample = scene.Camera.Downsample;
            LastVisibleWorldBounds = scene.VisibleWorldBounds;
        }

        protected void LoadSectionAnnotations()
        {
            if (Parent.Scene == null)
                return;

            int StartingSectionNumber = _Parent.Section.Number;
            SectionAnnotationsView SectionAnnotations;

            SectionAnnotations = GetOrCreateAnnotationsForSection(_Parent.Section.Number, _Parent);
            SectionAnnotations.LoadAnnotationsInRegion(Parent.Scene);
            //SectionAnnotationsView.LoadSectionAnnotations(SectionAnnotations, false);
        }

        /*
        protected void LoadSectionAnnotations()
        {
            if (Parent.Scene == null)
               return;
            
            int StartingSectionNumber = _Parent.Section.Number;
            SectionAnnotationsView SectionAnnotations;
            SectionAnnotationsView SectionAnnotationsAbove;
            SectionAnnotationsView SectionAnnotationsBelow;
            SectionAnnotations = GetOrCreateAnnotationsForSection(_Parent.Section.Number, _Parent);
            SectionAnnotations.LoadSectionAnnotationsInRegion(Parent.Scene);
            //Task.Factory.StartNew(() => SectionAnnotations.LoadSectionAnnotationsInRegion(Parent.Scene));

            int refSectionNumberAbove=0;
            int refSectionNumberBelow=-1;
            if (_Parent.Section.ReferenceSectionAbove != null)
            {
                refSectionNumberAbove = _Parent.Section.ReferenceSectionAbove.Number;
                SectionAnnotationsAbove = GetOrCreateAnnotationsForSection(refSectionNumberAbove, _Parent);
                SectionAnnotationsAbove.LoadSectionAnnotationsInRegion(Parent.Scene);
            //    Task.Factory.StartNew(() => SectionAnnotationsAbove.LoadSectionAnnotations(false));
                //Task.Factory.StartNew(() => SectionAnnotationsAbove.LoadSectionAnnotationsInRegion(Parent.Scene));
            }

            if (_Parent.Section.ReferenceSectionBelow != null)
            {
                refSectionNumberBelow = _Parent.Section.ReferenceSectionBelow.Number;
                SectionAnnotationsBelow = GetOrCreateAnnotationsForSection(refSectionNumberBelow, _Parent);
                SectionAnnotationsBelow.LoadSectionAnnotationsInRegion(Parent.Scene);
                //Task.Factory.StartNew(() => SectionAnnotationsBelow.LoadSectionAnnotations(false));
                //Task.Factory.StartNew(() => SectionAnnotationsBelow.LoadSectionAnnotationsInRegion(Parent.Scene));
            }

            int EndingSectionNumber = _Parent.Section.Number; 
            Debug.Assert(refSectionNumberAbove != refSectionNumberBelow);
            Debug.Assert(StartingSectionNumber == EndingSectionNumber);
            Debug.Assert(SectionAnnotations.Section.Number == StartingSectionNumber); 
            
            linksView.LoadSection(_Parent.Section.Number);

            //Task.Factory.StartNew(() => Store.Locations.FreeExcessSections(Global.NumSectionsInMemory, Global.NumSectionsLoading));

            //AnnotationCache.LoadSectionAnnotations(_Parent.Section); 
        }
        */
          
        static private BasicEffect basicEffect = null;
        static private BlendState defaultBlendState = null; 
        
        private static BasicEffect CreateBasicEffect(GraphicsDevice graphicsDevice, VikingXNA.Scene scene)
        {
            basicEffect = new BasicEffect(graphicsDevice);
            basicEffect.Projection = scene.Projection;
            basicEffect.View = scene.Camera.View;
            basicEffect.World = scene.World;
            return basicEffect;
        }

        public void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                         VikingXNA.Scene scene, 
                         Microsoft.Xna.Framework.Graphics.Texture BackgroundLuma,
                         Microsoft.Xna.Framework.Graphics.Texture BackgroundColors,
                        ref int nextStencilValue)
        {
            /// <summary>
            /// Steps:
            ///     Find all the locations for a section.  This could be optimized to return only visible sections immediately with a better data structure
            ///     Filter out invisible locations
            ///     Draw the backgrounds
            ///     Draw the overlapping linked locations over the backgrounds
            ///     Draw the structure links 
            ///     Draw the labels
            /// </summary>
            /// <param name="graphicsDevice"></param>
            /// <param name="scene"></param>
            /// <param name="BackgroundLuma"></param>
            /// <param name="BackgroundColors"></param>
            /// <param name="nextStencilValue"></param>

            if(_Parent.Section == null)
                return;

            if (_Parent.spriteBatch.GraphicsDevice.IsDisposed)
                return;


            if (ShouldLoadAnnotationsForSceneMovement(scene))
                System.Threading.Tasks.Task.Run(() => LoadSectionAnnotations());

            UpdateSceneHistory(Parent.Scene);

            Matrix ViewProjMatrix = scene.Camera.View * scene.Projection;

            GridRectangle Bounds = scene.VisibleWorldBounds;

            nextStencilValue++; 
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, nextStencilValue);

            if (basicEffect == null)
                basicEffect = CreateBasicEffect(graphicsDevice, scene);
            else if (basicEffect.IsDisposed)
                basicEffect = CreateBasicEffect(graphicsDevice, scene);

            basicEffect.Projection = scene.Projection;
            basicEffect.View = scene.Camera.View;
            basicEffect.World = scene.World;

            VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect = Parent.annotationOverlayEffect;

            overlayEffect.LumaTexture = BackgroundLuma;
            Parent.LumaOverlayLineManager.LumaTexture = BackgroundLuma; 

            overlayEffect.RenderTargetSize = graphicsDevice.Viewport;
            Parent.LumaOverlayLineManager.RenderTargetSize = graphicsDevice.Viewport;
            
            basicEffect.Alpha = 1;
            
            RasterizerState OriginalRasterState = graphicsDevice.RasterizerState;
            SectionAnnotationsView currentSectionAnnotations = CurrentSectionAnnotations;
            Debug.Assert(currentSectionAnnotations != null);

            int SectionNumber = _Parent.Section.Number;

            float Time = (float)TimeSpan.FromTicks(DateTime.Now.Ticks - DateTime.Today.Ticks).TotalSeconds;
            //            Debug.WriteLine("Time: " + Time.ToString()); 

            nextStencilValue = DeviceStateManager.GetDepthStencilValue(graphicsDevice) + 1;
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, nextStencilValue);
            
            ICollection<LocationCanvasView> Locations = currentSectionAnnotations.GetLocations(scene.VisibleWorldBounds);
            List<LocationCanvasView> listLocationsToDraw = FindVisibleLocations(Locations, scene);

            //Draw all of the locations on the current section
            WebAnnotation.LocationObjRenderer.DrawBackgrounds(listLocationsToDraw, graphicsDevice, basicEffect, overlayEffect, Parent.LumaOverlayLineManager, Parent.LumaOverlayCurveManager, scene, SectionNumber);

            nextStencilValue = DeviceStateManager.GetDepthStencilValue(graphicsDevice) + 1;
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, nextStencilValue);

            //Get all the lines to draw first so the text and geometric shapes are over top of them
            //IEnumerable<LocationLinkView> VisibleLinks = current.VisibleLocationLinks(_Parent.Section.Number, Bounds);
            LocationLinkView.Draw(graphicsDevice, scene, Parent.LumaOverlayLineManager, basicEffect, overlayEffect, currentSectionAnnotations.NonOverlappedLocationLinks);

            // graphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Black, float.MaxValue, 0);

            nextStencilValue = DeviceStateManager.GetDepthStencilValue(graphicsDevice) + 1;
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, nextStencilValue);

            //Find the locations on the adjacent sections

            ICollection<LocationCanvasView> RefLocations = currentSectionAnnotations.AdjacentLocationsNotOverlappedInRegion(Bounds);
            
            List<LocationCanvasView> listVisibleNonOverlappingLocationsOnAdjacentSections = FindVisibleAdjacentLocations(RefLocations, scene); 
            //listVisibleNonOverlappingLocationsOnAdjacentSections = FindNonOverlappedAdjacentLocations(listLocationsToDraw, listVisibleNonOverlappingLocationsOnAdjacentSections, _Parent.Section.Number);

            nextStencilValue = DeviceStateManager.GetDepthStencilValue(graphicsDevice) + 1;
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, nextStencilValue);

            graphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Black, float.MaxValue, 0);
            
            WebAnnotation.LocationObjRenderer.DrawBackgrounds(listVisibleNonOverlappingLocationsOnAdjacentSections, graphicsDevice, basicEffect, overlayEffect, Parent.LumaOverlayLineManager, Parent.LumaOverlayCurveManager, scene, SectionNumber);

            nextStencilValue = DeviceStateManager.GetDepthStencilValue(graphicsDevice) + 1;
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, nextStencilValue);

            graphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Black, float.MaxValue, 0);
            
            if (defaultBlendState == null || defaultBlendState.IsDisposed)
            {
                defaultBlendState = new BlendState();
                defaultBlendState.AlphaBlendFunction = BlendFunction.Add;
                defaultBlendState.AlphaSourceBlend = Blend.SourceAlpha;
                defaultBlendState.AlphaDestinationBlend = Blend.DestinationAlpha;
                defaultBlendState.ColorSourceBlend = Blend.SourceColor;
                defaultBlendState.ColorDestinationBlend = Blend.DestinationColor;
                defaultBlendState.ColorBlendFunction = BlendFunction.Add;
            }

            graphicsDevice.BlendState = defaultBlendState;

            DeviceStateManager.SetRasterizerStateForShapes(graphicsDevice);
            DeviceStateManager.SetRenderStateForShapes(graphicsDevice);

            //Get all the lines to draw
            List<StructureLinkViewModelBase> VisibleStructureLinks = currentSectionAnnotations.VisibleStructureLinks(scene);
            StructureLinkCirclesView.Draw(graphicsDevice, scene, Parent.LumaOverlayLineManager, VisibleStructureLinks.Where(l => l as StructureLinkCirclesView != null).Cast<StructureLinkCirclesView>().ToArray());
            StructureLinkCurvesView.Draw(graphicsDevice, scene, Parent.LumaOverlayLineManager, VisibleStructureLinks.Where(l => l as StructureLinkCurvesView != null).Cast<StructureLinkCurvesView>().ToArray());
            
            graphicsDevice.BlendState = defaultBlendState;
            
            //Draw text
            DrawLocationLabels(listLocationsToDraw, scene);

            DrawLocationLabels(listVisibleNonOverlappingLocationsOnAdjacentSections, scene); 
             
            graphicsDevice.RasterizerState = OriginalRasterState;
        }

        private void DrawLocationLabels(ICollection<LocationCanvasView> locations, VikingXNA.Scene scene)
        {
            var listLocationsWithVisibleLabels = locations.Where(l => l.IsLabelVisible(scene));
            _Parent.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            
            long section_number = _Parent.Section.Number;
            foreach (LocationCanvasView loc in listLocationsWithVisibleLabels)
            {
                loc.DrawLabel(_Parent.spriteBatch,
                              _Parent.fontArial,
                              scene,
                              (int)(section_number - loc.Z));
            }
            _Parent.spriteBatch.End();

            foreach (LocationOpenCurveView curve in listLocationsWithVisibleLabels.Where(l => l.GetType() == typeof(LocationOpenCurveView)).Cast<LocationOpenCurveView>())
            {
                curve.DrawLabel(Parent.Device, scene, Parent.spriteBatch, Parent.fontArial, Parent.CurveManager, Parent.basicEffect);
            }

        }

        private static List<LocationCanvasView> FindVisibleLocations(IEnumerable<LocationCanvasView> locations, VikingXNA.Scene scene)
        {
            return locations.Where(l => l != null && l.Parent != null && l.Parent.Type != null && l.IsVisible(scene)).ToList();
        }

        private static List<LocationCanvasView> FindVisibleAdjacentLocations(IEnumerable<LocationCanvasView> locations, VikingXNA.Scene scene)
        { 
            return locations.Where(l => l != null && l.Parent != null && l.Parent.Type != null && l.IsVisible(scene)).ToList();
        }
        /*
        /// <summary>
        /// Return all locations which overlap the passed locations
        /// </summary>
        /// <param name="locations"></param>
        /// <returns></returns>
        private static List<LocationCanvasView> FindOverlappedAdjacentLocations(List<LocationCanvasView> locations)
        {
            return locations.SelectMany(l => l.OverlappingLinks).OrderBy(l => l.ID).ToList();
        }

        /// <summary>
        /// Remove all locations from the collection which overlap locations on the specified section
        /// </summary>
        /// <param name="locations">The collection to remove overlapping locations from</param>
        /// <returns>The removed locations which overlap</returns>
        private static List<LocationCanvasView> FindNonOverlappedAdjacentLocations(List<LocationCanvasView> locations, List<LocationCanvasView> adjacentLocations, int section_number)
        {
            List<LocationCanvasView> listOverlappingLocations = FindOverlappedAdjacentLocations(locations);

            adjacentLocations = adjacentLocations.OrderBy(l => l.ID).ToList();

            locations = locations.OrderBy(l => l.ID).ToList();

            int iOverlapping = listOverlappingLocations.Count - 1;
            for (int i = adjacentLocations.Count - 1; i >= 0; i--)
            {
                LocationCanvasView loc = adjacentLocations.ElementAt(i);
                while (iOverlapping >= 0)
                {
                    LocationCanvasView overlappingLoc = listOverlappingLocations[iOverlapping];
                    if (overlappingLoc.ID < loc.ID)
                    {
                        break;
                    }
                    else if (overlappingLoc.ID == loc.ID)
                    {
                        adjacentLocations.RemoveAt(i);
                        break;
                    }
                    else
                    {
                        iOverlapping--;
                    }
                }

                if (iOverlapping < 0)
                    break;
            }

            //List<LocationCanvasView> listOverlappingLocations = new List<LocationCanvasView>(locations.Count);
            //return adjacentLocations.Where(l => !listOverlappingLocations.Contains(l)).ToList();
            
            return adjacentLocations;
        }
        */
        /// <summary>
        /// 
        /// </summary>
        /// <param name="StructureTypeColor"></param>
        /// <param name="section_span_distance">Number of sections the location link crosses</param>
        /// <param name="direction">Direction the link is in from the current section</param>
        /// <returns></returns>
        private Microsoft.Xna.Framework.Color GetLocationLinkColor(System.Drawing.Color structure_type_color, int section_span_distance, double direction, bool IsMouseOver)
        {
            int red = (int)((float)(structure_type_color.R * .5) + (128 * direction));
            red = 255 - (red / section_span_distance);
            red = red > 255 ? 255 : red;
            red = red < 0 ? 0 : red;
            int blue = (int)((float)(structure_type_color.B * .5) + (128 * -direction));
            blue = 255 - (blue / section_span_distance);
            blue = blue > 255 ? 255 : blue;
            blue = blue < 0 ? 0 : blue;
            int green = (int)((float)structure_type_color.G);
            green = 255 - (green / section_span_distance);
            green = green < 0 ? 0 : green;

            int alpha = 64;
            if (IsMouseOver)
            {
                alpha = 128;
            }

            //If you don't cast to byte the wrong constructor is used and the alpha value is wrong
            return new Microsoft.Xna.Framework.Color((byte)(red),
                (byte)(green),
                (byte)(blue),
                (byte)(alpha));
        }

        /*
        private void DrawLocationLink(LocationLinkView link, Matrix ViewProjMatrix)
        {
            LocationObj locA = link.A;
            LocationObj locB = link.B;

            if (!link.IsVisible(Parent.Scene))
                return;

            if (!locA.VolumePositionHasBeenCalculated)
                return;
            if (!locB.VolumePositionHasBeenCalculated)
                return;

            //Don't draw links for line style locations.
            if (!(locA.TypeCode == LocationType.CIRCLE && locB.TypeCode == LocationType.CIRCLE))
                return; 

            //Don't draw if the link falls within the radius of the location we are drawing
            if (link.LinksOverlap(Parent.Section.Number))
                return;

            if (locA.Parent == null)
                return;

            StructureType type = new StructureType(locA.Parent.Type);
            if (type == null)
                return;

            int distanceFactor = link.maxSection - link.minSection;
            if (distanceFactor == 0)
                distanceFactor = 1;

            //Give the colors a nudge towards red or blue depending on the direction to the link
            double directionFactor = 1;
            directionFactor = link.maxSection == _Parent.Section.Number ? 1 : -1;

            Microsoft.Xna.Framework.Color color = GetLocationLinkColor(type.Color, distanceFactor, directionFactor, LastMouseOverObject == link);
              
            _Parent.LumaOverlayLineManager.Draw(link.lineGraphic, (float)link.LineWidth, color.ConvertToHSL(),
                                         ViewProjMatrix, 0, null);
        }*/

        /*
        private void DrawStructureLink(StructureLinkViewModelBase link, Matrix ViewProjMatrix, float time_offset)
        {
            int alpha = 128;
            if (LastMouseOverObject == link)
            {
                alpha = 192;
            }

            //If you don't cast to byte the wrong constructor is used and the alpha value is wrong
            Microsoft.Xna.Framework.Color color = new Microsoft.Xna.Framework.Color((byte)(255),
                (byte)(255),
                (byte)(255),
                (byte)(alpha));

            if (link.Bidirectional)
            {
                _Parent.LineManager.Draw(link.lineGraphic, (float)link.Radius, color,
                                         ViewProjMatrix, time_offset, "AnimatedBidirectional");
            }
            else
            {
                _Parent.LineManager.Draw(link.lineGraphic, (float)link.Radius, color,
                                         ViewProjMatrix, time_offset, "AnimatedLinear");
            }
        }*/

        #endregion
            
    }
}
