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
using WebAnnotationModel;
using connectomes.utah.edu.XSD.WebAnnotationUserSettings.xsd;
using System.Threading.Tasks;

namespace WebAnnotation
{
    [Viking.Common.SectionOverlay("Annotation")]
    class AnnotationOverlay : Viking.Common.ISectionOverlayExtension
    {
        static public float LocationTextScaleFactor = 5;
        static public float ReferenceLocationTextScaleFactor = 2.5f;
        static public float RadiusToResizeCircle = 7.0f / 8.0f;
        static public float RadiusToLinkCircle = 1.0f / 4.0f;

        Viking.UI.Controls.SectionViewerControl _Parent;
        public Viking.UI.Controls.SectionViewerControl Parent { get { return _Parent; } }

        protected TransformChangedEventHandler SectionChangedEventHandler;
        private EventHandler AnnotationChangedEventHandler;
        //private NotifyCollectionChangedEventHandler LocationsChangedEventHandler;
        //private NotifyCollectionChangedEventHandler StructuresChangedEventHandler; 

        //private static SortedDictionary<int, SectionLocationsViewModel> dictSectionAnnotations = new SortedDictionary<int, SectionLocationsViewModel>();
        private static SectionLocationViewModelCache cacheSectionAnnotations = new SectionLocationViewModelCache(); 
        private static LocationLinksViewModel linksView;

        private static AnnotationOverlay _CurrentOverlay = null; 
        public static AnnotationOverlay CurrentOverlay { get { return _CurrentOverlay;}}

        GridVector2 LastMouseDownCoords;

        /// <summary>
        /// The last object the mouse was over, if any
        /// </summary>
        internal static IUIObjectBasic LastMouseOverObject = null;

        static AnnotationOverlay()
        {
            cacheSectionAnnotations.MaxCacheSize = Global.NumSectionsInMemory; 
        }

        public AnnotationOverlay()
        {
            SectionChangedEventHandler = new TransformChangedEventHandler(this.OnSectionTransformChanged);

            AnnotationChangedEventHandler = new EventHandler(OnAnnotationChanged);

            Store.Locations.OnCollectionChanged += new NotifyCollectionChangedEventHandler(OnLocationCollectionChanged);

            //    AnnotationCache.AnnotationChanged += AnnotationChangedEventHandler;      
            _CurrentOverlay = this;
        }

        string Viking.Common.ISectionOverlayExtension.Name()
        {
            return Global.EndpointName;
        }

        static public void  GoToLocation(LocationObj loc)
        {
            if(loc == null)
                return; 

            //Adjust downsample so the location fits nicely in the view
            double downsample = ((loc.Radius * 2) / Viking.UI.State.ViewerForm.Width) * Global.DefaultLocationJumpDownsample; 

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
        public static SectionLocationsViewModel GetAnnotationsForSection(int SectionNumber)
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
        public static SectionLocationsViewModel GetOrCreateAnnotationsForSection(int SectionNumber, 
                                                                                 Viking.UI.Controls.SectionViewerControl parent,
                                                                                 EventHandler AnnotationChangedEventHandler)
        {
            if (parent.Section.VolumeViewModel.SectionViewModels.ContainsKey(SectionNumber))
            {
                SectionLocationsViewModel SectionAnnotations = cacheSectionAnnotations.Fetch(SectionNumber);
                if (SectionAnnotations != null)
                    return SectionAnnotations; 
                                
                SectionAnnotations = new SectionLocationsViewModel(parent.Section.VolumeViewModel.SectionViewModels[SectionNumber],
                                                                                        parent);

                SectionLocationsViewModel retVal = cacheSectionAnnotations.GetOrAdd(SectionNumber, SectionAnnotations);

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
        
        

        public SectionLocationsViewModel CurrentSectionAnnotations
        {
            get
            {
                return GetOrCreateAnnotationsForSection(_Parent.Section.Number, Parent, AnnotationChangedEventHandler);
            }
        }

        /// <summary>
        /// Returns the location nearest to the mouse, prefers the locations on the current section
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public Location_CanvasViewModel GetNearestLocation(GridVector2 position, out double BestDistance)
        {
            BestDistance = double.MaxValue;
            SectionLocationsViewModel locView = GetAnnotationsForSection(CurrentSectionNumber);
            if (locView == null)
                return null;

            Location_CanvasViewModel bestObj;
            bestObj = locView.GetNearestLocation(position, out BestDistance);

            Location_CanvasViewModel adjacentObj;
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
                            if (distance < adjacentObj.OffSectionRadius)
                            {
                                bestObj = adjacentObj;
                                BestDistance = distance;
                            }
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
                            if (distance < adjacentObj.OffSectionRadius)
                            {
                                bestObj = adjacentObj;
                                BestDistance = distance;
                            }
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
        public IUIObjectBasic GetNearestDrawnAnnotation(GridVector2 position, out double distance)
        {
            distance = double.MaxValue; 
            SectionLocationsViewModel locView = GetAnnotationsForSection(CurrentSectionNumber); 
            double BestDistance = double.MaxValue;
            IUIObjectBasic bestObj = null; 
            if (locView != null)
            {
                bestObj = locView.GetNearestAnnotation(position, out BestDistance);

                if (bestObj != null)
                {
                    distance = BestDistance;
                    Location_CanvasViewModel loc = bestObj as Location_CanvasViewModel;
                    if (loc == null)
                        return bestObj;
    
                    if (loc.OverlappingLinkedLocationCircles.Count == 0)
                        return bestObj; 

                    //Check if an overlapping adjacent location is a better fit
                    foreach (OverlappedLocation linkedLoc in loc.OverlappingLinkedLocationCircles.Keys)
                    {
                        GridCircle linkCircle = loc.OverlappingLinkedLocationCircles[linkedLoc];
                        if (linkCircle.Contains(position))
                        {
                            bestObj = linkedLoc;
                           // bestObj = new LocationLink(loc, linkedLoc); 
                            distance = GridVector2.Distance(position, linkCircle.Center); 
                            return bestObj; 
                        }
                    }
                }
            }

            if (bestObj == null)
            {
                Location_CanvasViewModel adjacentObj;
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
                                if (distance < adjacentObj.OffSectionRadius)
                                {
                                    bestObj = adjacentObj;
                                    BestDistance = distance;
                                }
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
                                if (distance < adjacentObj.OffSectionRadius)
                                {
                                    bestObj = adjacentObj;
                                    BestDistance = distance;
                                }
                            }
                        }
                    }
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

            return bestObj; 
        }

        #region ISectionOverlayExtension Members

        public void SetParent(Viking.UI.Controls.SectionViewerControl parent)
        {
            //I'm only expecting this to be set once
            Debug.Assert(_Parent == null, "Not expecting parent to be set twice, OK to ignore, but annotation display may be incorrect"); 
            this._Parent = parent;
              
            //Load the locations for the current sections
            this._Parent.OnSectionChanged += new SectionChangedEventHandler(this.OnSectionChanged);
            this._Parent.OnSectionTransformChanged += new TransformChangedEventHandler(this.OnSectionTransformChanged);
            this._Parent.OnVolumeTransformChanged += new TransformChangedEventHandler(this.OnVolumeTransformChanged); 
            this._Parent.OnReferenceSectionChanged += new EventHandler(this.OnReferenceSectionChanged);

            this._Parent.MouseDown += new MouseEventHandler(this.OnMouseDown);
            this._Parent.MouseMove += new MouseEventHandler(this.OnMouseMove);
            this._Parent.MouseUp += new MouseEventHandler(this.OnMouseUp); 
            this._Parent.KeyDown += new KeyEventHandler(this.OnKeyDown);
            this._Parent.KeyUp += new KeyEventHandler(this.OnKeyUp);

           // AnnotationCache.parent = parent;
            GlobalPrimitives.CircleTexture = parent.LoadTextureWithAlpha("Circle", "CircleMask"); //parent.Content.Load<Texture2D>("Circle");
            GlobalPrimitives.UpArrowTexture = parent.LoadTextureWithAlpha("UpArrowV2", "UpArrowMask"); //parent.Content.Load<Texture2D>("Circle");

            linksView = new LocationLinksViewModel(parent); 

            LoadSectionAnnotations();
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

            IUIObjectBasic NextMouseOverObject = GetNearestDrawnAnnotation(WorldPosition, out distance);
            if (NextMouseOverObject != LastMouseOverObject)
                Parent.Invalidate();

            LastMouseOverObject = NextMouseOverObject; 
            
            /*Check to see if we are over a location*/
            
            Location_CanvasViewModel loc = LastMouseOverObject as Location_CanvasViewModel; // GetNearestLocation(WorldPosition, out distance);
            if (loc != null)
            {
                //If the loc is on this section we check if we are close to the edge and we are resizing.  Everyone else gets move cursor
                if (loc.TypeCode == LocationType.CIRCLE)
                {
                    OverlappedLocation overlapLoc = LastMouseOverObject as OverlappedLocation;
                    if (overlapLoc != null)
                    {
                        _Parent.Cursor = Cursors.Cross;
                        return;
                    }
                    else
                        _Parent.Cursor = Cursors.Default;

                    GridVector2 locPosition = loc.VolumePosition;

                    distance = GridVector2.Distance(locPosition, WorldPosition);
                    if (loc.Section == _Parent.Section.Number)
                    {
                        if (distance <= loc.Radius)
                        {
                            if (distance >= (loc.Radius * RadiusToResizeCircle))
                            {
                                _Parent.Cursor = Cursors.SizeAll;
                            }
                            else if (distance >= (loc.Radius * RadiusToLinkCircle))
                            {
                                _Parent.Cursor = Cursors.Arrow;
                            }
                            else
                            {
                                _Parent.Cursor = Cursors.Hand;
                            }
                        }
                    }
                    else
                    {
                        if (distance <= loc.OffSectionRadius)
                        {
                            LastMouseOverObject = loc;
                            _Parent.Cursor = Cursors.Cross;
                        }
                    }
                }
            }
            else
            {
                _Parent.Cursor = Cursors.Default;
            }

            
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
                IUIObjectBasic obj = GetNearestDrawnAnnotation(WorldPosition, out distance);
                //Figure out if it is resizing a location circle
                //If the loc is on this section we check if we are close to the edge and we are resizing.  Everyone else gets standard location command
                Viking.UI.State.SelectedObject = obj as IUIObjectBasic;

                /*If we select a link, find the location off the section and assume we have selected that*/
                Location_CanvasViewModel loc = obj as Location_CanvasViewModel;

                if (loc != null)
                {
                    if (loc.TypeCode == LocationType.CIRCLE &&
                        loc.Section == _Parent.Section.Number)
                    {
                        if (distance <= loc.Radius)
                        {
                            if (distance >= (loc.Radius * RadiusToResizeCircle))
                            {
                                Location_CanvasViewModel selected_loc = Viking.UI.State.SelectedObject as Location_CanvasViewModel;
                                SectionLocationsViewModel sectionAnnotations = AnnotationOverlay.GetAnnotationsForSection(Parent.Section.Number);
                                if (sectionAnnotations == null)
                                    return;

                                _Parent.CurrentCommand = new ResizeCircleCommand(Parent,
                                        selected_loc.Parent.Type.Color,
                                        sectionAnnotations.GetPositionForLocation(selected_loc),
                                        (radius) => { selected_loc.Radius = radius; Store.Locations.Save(); });
                            }
                            else if (distance >= (loc.Radius * RadiusToLinkCircle))
                            {
                                _Parent.CurrentCommand = new LinkAnnotationsCommand(Parent, loc);
                            }
                            else
                            {
                                _Parent.Cursor = Cursors.SizeAll;
                            }
                        }
                    }
                }
            }
        }

        protected void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (_Parent.CurrentCommand == null)
                return;

            //Check if there is a non-default command. we don't want to mess with another active command
            if (_Parent.CurrentCommand.GetType() != typeof(Viking.UI.Commands.DefaultCommand) ||
                Viking.UI.Commands.Command.QueueDepth > 0)
                return; 

            if (e.Button == MouseButtons.Left)
            {
                if (LastMouseOverObject == null)
                {
                    OnContinueLastTrace(LastMouseDownCoords); 
                }
            }
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
                        Parent.GoToLocation(new Microsoft.Xna.Framework.Vector2((float)CreateNewLinkedLocationCommand.LastEditedLocation.SectionPosition.X,
                                                                                (float)CreateNewLinkedLocationCommand.LastEditedLocation.SectionPosition.Y),
                                            (int)CreateNewLinkedLocationCommand.LastEditedLocation.Z,
                                            true,
                                            (double)((CreateNewLinkedLocationCommand.LastEditedLocation.Radius * 2) / Parent.Width) * 2); 

                    }
                    return; 
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
                            OnCreateStructure(System.Convert.ToInt64(comAction.TypeID), comAction.AttributeList);

                            return;
                        }

                        ToggleStructureTagCommandAction tagAction = Global.UserSettings.Actions.ToggleStructureTagCommandAction.Where(action => action.Name == h.Action).SingleOrDefault();
                        if (tagAction != null)
                        {
                            OnToggleStructureTag(tagAction.Tag);

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
                    if (CurrentSectionAnnotations.HaveLoadedSectionAnnotations == false)
                    {
                        LoadSectionAnnotations();
                    }

                    this._Parent.Invalidate();

                    break;
            }
        }

        protected void OnCreateStructure(long TypeID, IEnumerable<string> attributes)
        {
            StructureTypeObj typeObj = Store.StructureTypes.GetObjectByID(TypeID);
            if (typeObj != null)
            {
                StructureType type = new StructureType(typeObj);

                System.Drawing.Point ClientPoint = _Parent.PointToClient(System.Windows.Forms.Control.MousePosition);
                GridVector2 WorldPos = _Parent.ScreenToWorld(ClientPoint.X, ClientPoint.Y);
                GridVector2 SectionPos;
                bool success = _Parent.TryVolumeToSection(WorldPos, _Parent.Section, out SectionPos);
                Debug.Assert(success);
                if (!success)
                    return;

                
                StructureObj newStruct = new StructureObj(type.modelObj);
                LocationObj newLocation = new LocationObj(newStruct,
                                                SectionPos,
                                                WorldPos,
                                                Parent.Section.Number);

                Structure newStructView = new Structure(newStruct);
                Location_CanvasViewModel newLocationView = new Location_CanvasViewModel(newLocation);

                if (attributes != null)
                {
                    foreach (string attrib in attributes)
                    {
                        newStructView.ToggleAttribute(attrib);
                    }
                }

                Viking.UI.Commands.Command.EnqueueCommand(typeof(ResizeCircleCommand), new object[] { Parent, type.Color, WorldPos, new ResizeCircleCommand.OnCommandSuccess((double radius) => { newLocationView.Radius = radius; }) });
                if (type.Parent != null)
                {
                    //Enqueue extra command to select a parent
                    Viking.UI.Commands.Command.EnqueueCommand(typeof(LinkStructureToParentCommand), new object[] { Parent, newStructView, newLocationView });
                }

                Viking.UI.Commands.Command.EnqueueCommand(typeof(CreateNewStructureCommand), new object[] { Parent, newStructView, newLocationView});
            }
            else
                Trace.WriteLine("Could not find hotkey ID for type: " + TypeID.ToString()); 
        }

        protected void OnToggleStructureTag(string tag)
        {
            if(LastMouseOverObject == null)
            {
                Trace.WriteLine("No mouse over object to toggle tag");
                return;
            }

            Location_CanvasViewModel loc = LastMouseOverObject as Location_CanvasViewModel; // GetNearestLocation(WorldPosition, out distance);
            if(loc == null)
            {
                Trace.WriteLine("No mouse over location to toggle tag");
                return;
            }

            ToggleTagCommand command = new ToggleTagCommand(this.Parent, loc.Parent, tag);

            Viking.UI.Commands.Command.EnqueueCommand(typeof(ToggleTagCommand), new object[] { this.Parent, loc.Parent, tag});

            return; 
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
                Location_CanvasViewModel template = CreateNewLinkedLocationCommand.LastEditedLocation;
                if (template.Z != this.Parent.Section.Number)
                {
                    GridVector2 SectionPos;
                    bool success = _Parent.TryVolumeToSection(WorldPos, _Parent.Section, out SectionPos);
                    Debug.Assert(success);
                    if (!success)
                        return;

                    LocationObj newLoc = new LocationObj(CreateNewLinkedLocationCommand.LastEditedLocation.Parent.modelObj,
                                        SectionPos,
                                        WorldPos,
                                        Parent.Section.Number);

                    Location_CanvasViewModel newLocView = new Location_CanvasViewModel(newLoc);

                    Viking.UI.Commands.Command.EnqueueCommand(typeof(ResizeCircleCommand), new object[] { Parent, template.Parent.Type.Color, WorldPos, new ResizeCircleCommand.OnCommandSuccess((double radius) => { newLocView.Radius = radius; }) });
                    Viking.UI.Commands.Command.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), new object[] { Parent, template, newLocView });

                    Viking.UI.State.SelectedObject = null;
                    CreateNewLinkedLocationCommand.LastEditedLocation = null; 
                }
            }
        }
         

        /// <summary>
        /// This occurs when a new section is loaded and we need to fetch all locations from scratch
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void OnSectionChanged(object sender, SectionChangedEventArgs e)
        {
            
            //Don't load annotations when flipping sections if the user is holding down space bar to hide them
            if (_Parent.ShowOverlays)
            { 
                LoadSectionAnnotations();
            }
        }

        protected void OnAnnotationChanged(object sender, EventArgs e)
        {
            //Trigger redraw of screen
            _Parent.Invalidate(); 
        }

        /// <summary>
        /// Organize the changes so we only call the SectionAnnotationViewModel objects that we have to.
        /// Can be called from any thread
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void OnLocationCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            SortedSet<int> changedSections = new SortedSet<int>();

  //          if (e.Action == NotifyCollectionChangedAction.Replace)
  //          {
                //SortedList<int, List<LocationObj>> changedSectionsNewObjects = new SortedList<int, List<LocationObj>>(e.NewItems.Count);
//                SortedList<int, List<LocationObj>> changedSectionsOldObjects = new SortedList<int, List<LocationObj>>(e.NewItems.Count);

            if (e.NewItems != null)
            {
                for (int iObj = 0; iObj < e.NewItems.Count; iObj++)
                {
                    LocationObj locNewObj = e.NewItems[iObj] as LocationObj;
                    if(!changedSections.Contains(locNewObj.Section))
                        changedSections.Add(locNewObj.Section);
                }
            }

            if (e.OldItems != null)
            {
                for (int iObj = 0; iObj < e.OldItems.Count; iObj++)
                {
                    LocationObj locOldObj = e.OldItems[iObj] as LocationObj;
                    if (!changedSections.Contains(locOldObj.Section))
                        changedSections.Add(locOldObj.Section); 
                } 
            }

            foreach (int section in changedSections)
            {
                SectionLocationsViewModel SLVModel = cacheSectionAnnotations.Fetch(section);
                if (SLVModel != null)
                {
                    SLVModel.OnLocationsStoreChanged(sender, e);
                }
            }

            //Invalidate can always be called from any thread
            Parent.Invalidate();
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

        protected void LoadSectionAnnotations()
        {
            int StartingSectionNumber = _Parent.Section.Number; 
            SectionLocationsViewModel SectionAnnotations;
            SectionLocationsViewModel SectionAnnotationsAbove;
            SectionLocationsViewModel SectionAnnotationsBelow;
            SectionAnnotations = GetOrCreateAnnotationsForSection(_Parent.Section.Number, _Parent, AnnotationChangedEventHandler);
            //SectionAnnotations.LoadSectionAnnotations();
            Task.Factory.StartNew(() => SectionAnnotations.LoadSectionAnnotations());

            int refSectionNumberAbove=0;
            int refSectionNumberBelow=-1;
            if (_Parent.Section.ReferenceSectionAbove != null)
            {
                refSectionNumberAbove = _Parent.Section.ReferenceSectionAbove.Number;
                SectionAnnotationsAbove = GetOrCreateAnnotationsForSection(refSectionNumberAbove, _Parent, AnnotationChangedEventHandler);
                //SectionAnnotationsAbove.LoadSectionAnnotations();
                Task.Factory.StartNew(() => SectionAnnotationsAbove.LoadSectionAnnotations());
            }

            if (_Parent.Section.ReferenceSectionBelow != null)
            {
                refSectionNumberBelow = _Parent.Section.ReferenceSectionBelow.Number;
                SectionAnnotationsBelow = GetOrCreateAnnotationsForSection(refSectionNumberBelow, _Parent, AnnotationChangedEventHandler);
                //SectionAnnotationsBelow.LoadSectionAnnotations();
                Task.Factory.StartNew(() => SectionAnnotationsBelow.LoadSectionAnnotations());
            }

            int EndingSectionNumber = _Parent.Section.Number; 
            Debug.Assert(refSectionNumberAbove != refSectionNumberBelow);
            Debug.Assert(StartingSectionNumber == EndingSectionNumber);
            Debug.Assert(SectionAnnotations.Section.Number == StartingSectionNumber); 
            

            linksView.LoadSection(_Parent.Section.Number); 
            
            #if DEBUG
                //            Store.Structures.FreeExcessSections(40, 5);
                Task.Factory.StartNew(() => Store.Locations.FreeExcessSections(4, 3));
                //Task.Factory.StartNew(() => Store.Locations.FreeExcessSections(Global.NumSectionsInMemory, 5));
            #else
                Task.Factory.StartNew(() => Store.Locations.FreeExcessSections(Global.NumSectionsInMemory, 5));
            #endif
            //AnnotationCache.LoadSectionAnnotations(_Parent.Section); 
        }

        public IUIObjectBasic NearestObject(GridVector2 WorldPosition, out double distance)
        {
            distance = 0;
            IUIObjectBasic obj = GetNearestDrawnAnnotation(WorldPosition, out distance); 
            //IUIObjectBasic obj = CurrentSectionAnnotations.GetNearestAnnotation(WorldPosition, out distance);

            
            //IUIObjectBasic obj = AnnotationCache.GetNearestAnnotation(WorldPosition, out distance);

            return obj; 
        }

        DepthStencilState depthstencilState;

        public void IncrementDepthStencilValue(GraphicsDevice graphicsDevice, ref int NextStencilValue)
        {
            if (depthstencilState != null)
            {
                depthstencilState.Dispose();
                depthstencilState = null;
            }

            if (depthstencilState == null || depthstencilState.IsDisposed)
            {
                depthstencilState = new DepthStencilState();
                depthstencilState.DepthBufferEnable = true;
                depthstencilState.DepthBufferWriteEnable = true;
                depthstencilState.DepthBufferFunction = CompareFunction.LessEqual;

                depthstencilState.StencilEnable = true;
                depthstencilState.StencilFunction = CompareFunction.GreaterEqual;
                NextStencilValue++;
                depthstencilState.ReferenceStencil = NextStencilValue;
                depthstencilState.StencilPass = StencilOperation.Replace;

                graphicsDevice.DepthStencilState = depthstencilState;
            }
        }

        static private BasicEffect basicEffect = null;
        static private BlendState defaultBlendState = null; 

        
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

            Matrix ViewProjMatrix = scene.Camera.View * scene.Projection;

            GridRectangle Bounds = scene.VisibleWorldBounds;
            
            nextStencilValue++;

            if (basicEffect == null)
                basicEffect = new BasicEffect(graphicsDevice);
            else if(basicEffect.IsDisposed)
                basicEffect = new BasicEffect(graphicsDevice);

            VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect = Parent.annotationOverlayEffect;

            overlayEffect.LumaTexture = BackgroundLuma;

            overlayEffect.RenderTargetSize = graphicsDevice.Viewport;
            
            basicEffect.Alpha = 1;
            
            RasterizerState OriginalRasterState = graphicsDevice.RasterizerState;
            SectionLocationsViewModel currentSectionAnnotations = CurrentSectionAnnotations;
            Debug.Assert(currentSectionAnnotations != null);

            int SectionNumber = _Parent.Section.Number;

            float Time = (float)TimeSpan.FromTicks(DateTime.Now.Ticks - DateTime.Today.Ticks).TotalSeconds;
//            Debug.WriteLine("Time: " + Time.ToString()); 

            IncrementDepthStencilValue(graphicsDevice, ref nextStencilValue);

            //Get all the lines to draw first so the text and geometric shapes are over top of them
            IEnumerable<LocationLink> VisibleLinks = linksView.VisibleLocationLinks(_Parent.Section.Number, Bounds);
            foreach (LocationLink link in VisibleLinks)
            {
                DrawLocationLink(link, ViewProjMatrix);
            }
            graphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Black, float.MaxValue, 0);             

            IncrementDepthStencilValue(graphicsDevice, ref nextStencilValue);

            //graphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Black, float.MaxValue, 0); 
            //IncrementDepthStencilValue(graphicsDevice, ref nextStencilValue);

            ICollection<Location_CanvasViewModel> Locations = currentSectionAnnotations.GetLocations(Bounds);
            List<Location_CanvasViewModel> listLocationsToDraw = FindVisibleLocations(Locations, Bounds);
            
            //Find a circle that encloses the visible bounds
            GridCircle VisibleCircle = new GridCircle(Bounds.Center, GridVector2.Distance(Bounds.Center, new GridVector2(Bounds.Left, Bounds.Top)));
            
            List<Location_CanvasViewModel> RefLocations = new List<Location_CanvasViewModel>();
            if(_Parent.Section.ReferenceSectionBelow != null)
            {
                SectionLocationsViewModel sectionLocations = GetAnnotationsForSection(_Parent.Section.ReferenceSectionBelow.Number);
                if (sectionLocations != null)
                    RefLocations.AddRange(sectionLocations.GetLocations(Bounds).Where(l => l.modelObj.Terminal==false));//(Bounds)); 
            }

            if(_Parent.Section.ReferenceSectionAbove != null)
            {
                SectionLocationsViewModel sectionLocations = GetAnnotationsForSection(_Parent.Section.ReferenceSectionAbove.Number);
                if (sectionLocations != null)
                {
                    RefLocations.AddRange(sectionLocations.GetLocations(Bounds).Where(l => l.modelObj.Terminal==false));
                }
            }
            
            //Draw text for locations on the reference sections
            List<Location_CanvasViewModel> listVisibleNonOverlappingLocationsOnAdjacentSections = FindVisibleLocations(RefLocations, Bounds); 
            List<Location_CanvasViewModel> listVisibleOverlappingLocationsOnAdjacentSections = RemoveOverlappingLocations(listVisibleNonOverlappingLocationsOnAdjacentSections, _Parent.Section.Number); 

            //Draw all of the locations on the current section
            WebAnnotation.LocationObjRenderer.DrawBackgrounds(listLocationsToDraw, graphicsDevice, basicEffect, overlayEffect, scene, SectionNumber);

            IncrementDepthStencilValue(graphicsDevice, ref nextStencilValue);
            graphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Black, float.MaxValue, 0);

            WebAnnotation.LocationObjRenderer.DrawBackgrounds(listVisibleNonOverlappingLocationsOnAdjacentSections, graphicsDevice, basicEffect, overlayEffect, scene, SectionNumber);

            IncrementDepthStencilValue(graphicsDevice, ref nextStencilValue);
            graphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Black, float.MaxValue, 0);

            WebAnnotation.LocationObjRenderer.DrawOverlappedAdjacentLinkedLocations(listLocationsToDraw, scene, graphicsDevice, basicEffect, overlayEffect, SectionNumber);

            TryDrawLineFromOverlappingLocation(AnnotationOverlay.LastMouseOverObject as OverlappedLocation, _Parent.LineManager, _Parent.Section.Number, Time);
               
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

            //Get all the lines to draw
            List<StructureLink> VisibleStructureLinks = currentSectionAnnotations.VisibleStructureLinks(Bounds); 
            foreach (StructureLink link in VisibleStructureLinks)
            {
                DrawStructureLink(link, ViewProjMatrix, Time);
            }
            
            graphicsDevice.BlendState = defaultBlendState;
            
            //Draw text
            DrawLocationLabels(listLocationsToDraw);

            DrawLocationLabels(listVisibleNonOverlappingLocationsOnAdjacentSections); 
             
            graphicsDevice.RasterizerState = OriginalRasterState;
        }

        private void DrawLocationLabels(ICollection<Location_CanvasViewModel> locations)
        {
            _Parent.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            
            long section_number = _Parent.Section.Number;
            foreach (Location_CanvasViewModel loc in locations)
            {
                GridVector2 WorldPosition = loc.VolumePosition;

                GridVector2 DrawPosition = _Parent.WorldToScreen(WorldPosition.X, WorldPosition.Y);

                loc.DrawLabel(_Parent.spriteBatch,
                              _Parent.fontArial,
                              new Vector2((float)DrawPosition.X, (float)DrawPosition.Y),
                              (float)(1.0 / _Parent.StatusMagnification),
                              (int)(section_number - loc.Section));
            }

            _Parent.spriteBatch.End();
        }

        private static bool TryDrawLineFromOverlappingLocation(OverlappedLocation OverlappingLocation, RoundLineCode.RoundLineManager lineManager, int section_number, float time_offset)
        { 
            if (OverlappingLocation != null)
            {
                LocationLink SelectedLink = OverlappingLocation.link;
                //Give the colors a nudge towards red or blue depending on the direction to the link
                double directionFactor = 1;
                StructureType type = new StructureType(SelectedLink.A.Parent.Type);
                int distanceFactor = SelectedLink.maxSection - SelectedLink.minSection;
                if (distanceFactor == 0)
                    distanceFactor = 1;

                directionFactor = SelectedLink.maxSection == section_number ? 1 : -1;

                int red = (int)((float)(type.Color.R * .5) + (128 * directionFactor));
                red = 255 - (red / distanceFactor);
                red = red > 255 ? 255 : red;
                int blue = (int)((float)(type.Color.B * .5) + (128 * (1 - directionFactor)));
                blue = 255 - (blue / distanceFactor);
                blue = blue > 255 ? 255 : blue;
                int green = (int)((float)type.Color.G);
                green = 255 - (green / distanceFactor);

                int alpha = 85;
                if (LastMouseOverObject == SelectedLink)
                {
                    alpha = 192;
                }

                //If you don't cast to byte the wrong constructor is used and the alpha value is wrong
                Microsoft.Xna.Framework.Color color = new Microsoft.Xna.Framework.Color((byte)(red),
                    (byte)(green),
                    (byte)(blue),
                    (byte)(alpha));

                lineManager.Draw(SelectedLink.lineGraphic, (float)SelectedLink.Radius, color,
                                             basicEffect.View * basicEffect.Projection, time_offset, null);

                return true; 
            }

            return false; 
        }

        private static List<Location_CanvasViewModel> FindVisibleLocations(IEnumerable<Location_CanvasViewModel> locations,  GridRectangle VisibleBounds)
        {
            return locations.Where(l => l != null && l.VolumePositionHasBeenCalculated && l.Parent != null && l.Parent.Type != null).ToList();
        }


        /// <summary>
        /// Remove all locations from the collection which overlap locations on the specified section
        /// </summary>
        /// <param name="locations">The collection to remove overlapping locations from</param>
        /// <returns>The removed locations which overlap</returns>
        private static List<Location_CanvasViewModel> RemoveOverlappingLocations(List<Location_CanvasViewModel> locations, int section_number)
        {
            List<Location_CanvasViewModel> listOverlappingLocations = new List<Location_CanvasViewModel>(locations.Count);
            for (int i = locations.Count - 1; i >= 0; i--)
            {
                Location_CanvasViewModel loc = locations.ElementAt(i);
                if (loc.TypeCode == LocationType.CIRCLE)
                {
                    GridCircle locCircle = new GridCircle(loc.VolumePosition, loc.OffSectionRadius);
                    foreach (long linked in loc.Links)
                    {
                        LocationObj LinkedObj = Store.Locations.GetObjectByID(linked, false);
                        if (LinkedObj == null)
                            continue;

                        if (LinkedObj.Section == section_number)
                        {
                            if (locCircle.Intersects(LinkedObj.VolumePosition, LinkedObj.Radius))
                            {
                                listOverlappingLocations.Add(loc);
                                locations.RemoveAt(i);
                                break;
                            }
                        }
                    }
                }
            }

            return listOverlappingLocations;
        }

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
            int blue = (int)((float)(structure_type_color.B * .5) + (128 * (1 - direction)));
            blue = 255 - (blue / section_span_distance);
            blue = blue > 255 ? 255 : blue;
            int green = (int)((float)structure_type_color.G);
            green = 255 - (green / section_span_distance);

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

        private void DrawLocationLink(LocationLink link, Matrix ViewProjMatrix)
        {
            LocationObj locA = link.A;
            LocationObj locB = link.B;

            if (!link.LinksVisible(_Parent.Downsample))
                return;

            if (!locA.VolumePositionHasBeenCalculated)
                return;
            if (!locB.VolumePositionHasBeenCalculated)
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
              
            _Parent.LineManager.Draw(link.lineGraphic, (float)link.Radius, color,
                                         ViewProjMatrix, 0, null);
        }

        private void DrawStructureLink(StructureLink link, Matrix ViewProjMatrix, float time_offset)
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
        }

        #endregion

        /// <summary>
        /// Allocates a new quad tree based on the current section parameters
        /// </summary>
        public static GridRectangle SectionBounds(Viking.UI.Controls.SectionViewerControl Parent, int SectionNumber)
        {
            GridRectangle bounds = new GridRectangle(); 
            
            //Figure out the new boundaries for our quad-tree
            if(!Parent.Section.VolumeViewModel.SectionViewModels.ContainsKey(SectionNumber))
                return new GridRectangle();

            SectionViewModel SectionView = Parent.Section.VolumeViewModel.SectionViewModels[SectionNumber];
            bounds = Parent.SectionBounds(SectionView.section);
            if (SectionView.ReferenceSectionAbove != null)
            {
                bounds = GridRectangle.Union(bounds, Parent.SectionBounds(SectionView.ReferenceSectionAbove));
            }
            if (SectionView.ReferenceSectionBelow != null)
            {
                bounds = GridRectangle.Union(bounds, Parent.SectionBounds(SectionView.ReferenceSectionBelow));
            }
            
            return bounds;
        }
    }
}
