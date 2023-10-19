﻿using Viking.AnnotationServiceTypes.Interfaces;
using connectomes.utah.edu.XSD.WebAnnotationUserSettings.xsd;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Viking.Common;
using Viking.ViewModels;
using Viking.VolumeModel;
using VikingXNAGraphics;
using VikingXNAWinForms;
using WebAnnotation.Actions;
using WebAnnotation.UI;
using WebAnnotation.UI.Commands;
using WebAnnotation.View;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation
{
    [Viking.Common.SectionOverlay("Annotation")]
    class AnnotationOverlay : Viking.Common.ISectionOverlayExtension, Viking.Common.IHelpStrings, IPenActionSupport, ICanvasViewHitTesting
    {
        public static float LocationTextScaleFactor = 5;
        public static float ReferenceLocationTextScaleFactor = 2.5f;

        Viking.UI.Controls.SectionViewerControl _Parent;
        public Viking.UI.Controls.SectionViewerControl Parent { get { return _Parent; } }

        protected TransformChangedEventHandler SectionChangedEventHandler;
        private EventHandler AnnotationChangedEventHandler;

        //private static SectionLocationViewModelCache cacheSectionAnnotations = new SectionLocationViewModelCache(); 
        private static SectionAnnotationsViewModelCache cacheSectionAnnotations = new SectionAnnotationsViewModelCache();
        //private static LocationLinksViewModel linksView;

        private static AnnotationOverlay _CurrentOverlay = null;
        public static AnnotationOverlay CurrentOverlay { get { return _CurrentOverlay; } }

        protected static WebAnnotation.UI.Forms.GoToActionForm GoToLocationForm;
        protected static WebAnnotation.UI.Forms.GoToActionForm GoToStructureForm;

        GridVector2 LastMouseDownCoords;
        GridVector2 LastMouseMoveVolumeCoords;

        /// <summary>
        /// The last object the mouse was over, if any
        /// </summary>
        internal static ICanvasView LastMouseOverObject = null;

        Viking.UI.PenInputHelper PenPath = null;

        /// <summary>
        /// The last object the mouse was over, if any
        /// </summary>
        internal static ICanvasGeometryView LastIntersectedObject = null;

        private MouseOverLocationCanvasViewEffect mouseOverEffect = new MouseOverLocationCanvasViewEffect();

        /// <summary>
        /// Used to cancel loading section annotations when the desired annotations have changed
        /// </summary>
        private CancellationTokenSource loadSectionAnnotationsCancellationTokenSource;

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

        /// <summary>
        /// Shows a standard message box when changes to the server do not succeed.
        /// </summary>
        /// <param name="e"></param>
        public static void ShowFaultExceptionMsgBox(Exception e)
        {
            MessageBox.Show(string.Format("Server did not send proper response to save request.  The change was probably not saved.\nException:\n{0}", e), "Client <-> Server Error", MessageBoxButtons.OK);
        }

        public static bool SaveLocationsWithMessageBoxOnError()
        {
            try
            {
                Store.Locations.Save();
                return true;
            }
            catch (System.ServiceModel.FaultException e)
            {
                ShowFaultExceptionMsgBox(e);
            }

            return false;
        }

        public void InvalidateParent()
        {
            //Invalidate can always be called from any thread
            if (Parent.IsHandleCreated)
                Parent.BeginInvoke(new System.Action(() => Parent.Invalidate()));
        }

        string Viking.Common.ISectionOverlayExtension.Name()
        {
            return Global.EndpointName;
        }

        int Viking.Common.ISectionOverlayExtension.DrawOrder()
        {
            return 10;
        }

        public static void GoToStructure(long locID)
        {
            StructureObj s = Store.Structures.GetObjectByID(locID);
            if (s is null)
                return;

            GoToStructure(s);
        }

        public static void GoToStructure(StructureObj s)
        {
            if (s is null)
                return;

            ICollection<LocationObj> locations = Store.Locations.GetLocationsForStructure(s.ID);
            if (!locations.Any())
                return;

            {
                //ICanvasView lastObj = 
                GridVector2 lastObjCenter = WebAnnotation.AnnotationOverlay.CurrentOverlay.LastMouseDownCoords;
                GridVector3 origin = new GridVector3(lastObjCenter.X * Global.Scale.X, lastObjCenter.Y * Global.Scale.Y, WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.Section.Number * Global.Scale.Z);

                //Sort locations by distance
                List<LocationObj> nearest = locations.OrderBy(l => l.DistanceToPoint3D(origin)).ToList();

                List<double> Distance = nearest.Select(l => l.DistanceToPoint3D(origin)).ToList();
                List<double> Depth = nearest.Select(l => l.Z).ToList();

                GoToLocation(nearest.First());
            } 
        }

        public static void GoToLocation(long locID)
        {
            LocationObj loc = Store.Locations.GetObjectByID(locID);
            if (loc is null)
                return;

            GoToLocation(loc);
        }

        public static void GoToLocation(LocationObj loc)
        {
            if (loc is null)
                return;

            //Adjust downsample so the location fits nicely in the view
            double downsample = (loc.MosaicShape.BoundingBox().Width / Viking.UI.State.ViewerForm.Width) * Global.DefaultLocationJumpDownsample;

            //SectionViewerForm.Show(section);
            Viking.UI.State.ViewerForm.GoToLocation(new Microsoft.Xna.Framework.Vector2((float)loc.Position.X, (float)loc.Position.Y), (int)loc.Z, true, downsample);

            //BUG: This is because the way I handle commands changed dramatically between Plantmap and Viking.  I need to
            //set selected object to null to keep the UI from doing strange things
            Viking.UI.State.SelectedObject = null;
            Global.LastEditedAnnotationID = null;
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

        private readonly static SemaphoreSlim GetOrCreateAnnotationsForSectionSemaphore = new SemaphoreSlim(1);
        /// <summary>
        /// Returns annotations for section if they exist or creates new SectionLocationsViewModel if they do not
        /// </summary>
        /// <param name="SectionNumber"></param>
        public static SectionAnnotationsView GetOrCreateAnnotationsForSection(int SectionNumber)
        {
            SectionAnnotationsView SectionAnnotations = cacheSectionAnnotations.Fetch(SectionNumber);
            if (SectionAnnotations != null)
                return SectionAnnotations;

            if (Viking.UI.State.volume.SectionViewModels.ContainsKey(SectionNumber))
            {
                try
                {
                    GetOrCreateAnnotationsForSectionSemaphore.Wait();
                     
                    SectionAnnotationsView retVal = cacheSectionAnnotations.GetOrAdd(SectionNumber, (k) => new SectionAnnotationsView(Viking.UI.State.volume.SectionViewModels[SectionNumber]));

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
                finally
                {
                    GetOrCreateAnnotationsForSectionSemaphore.Release();
                }
            }

            return null;
        } 

        public string[] HelpStrings
        {
            get
            {
                List<string> helpstrings = new List<string>();
                if (IsCommandDefault())
                {
                    helpstrings.AddRange(BuildHotkeyHelpStrings());
                }

                helpstrings.AddRange(DefaultKeyHelpStrings());

                return helpstrings.ToArray();
            }
        }
         
        protected string[] LastMouseOverHelpStrings = new string[] { };

        public static void GotoLastModifiedLocation()
        {
            Debug.Print("Open Last Modified Location");

            var task = new System.Threading.Tasks.Task(() =>
            {
                WebAnnotationModel.LocationObj lastLocation = WebAnnotationModel.Store.Locations.GetLastModifiedLocation();
                if (lastLocation != null)
                {
                    Viking.UI.State.MainThreadDispatcher.Invoke(() => AnnotationOverlay.GoToLocation(lastLocation));
                }
            });
            task.Start();
        }

        /// <summary>
        /// Returns the location nearest to the mouse, prefers the locations on the current section
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public static List<HitTestResult> GetAnnotations(int sectionNumber, GridVector2 position)
        {
            SectionAnnotationsView locView = GetAnnotationsForSection(sectionNumber);
            if (locView == null)
                return null;

            //Get the overlapping locations, filter out non-location annotations
            return locView.GetAnnotations(position).Where(hr => hr.obj is LocationCanvasView).ToList();
        }

        /// <summary>
        /// Returns the location nearest to the mouse, prefers the locations on the current section
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public List<HitTestResult> GetAnnotations(GridVector2 position)
        {
            SectionAnnotationsView locView = GetAnnotationsForSection(CurrentSectionNumber);
            if (locView == null)
                return null;

            //Get the overlapping locations, filter out non-location annotations
            return locView.GetAnnotations(position).Where(hr => hr.obj is LocationCanvasView).ToList();
        }

        /// <summary>
        /// Find the annotations intersecting the provided point on the section, using annotation locations on the screen, not anatomical positions
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public object ObjectAtPosition(GridVector2 position, out double distance)
        {
            distance = double.MaxValue;
            SectionAnnotationsView locView = GetAnnotationsForSection(CurrentSectionNumber);
            if (locView == null)
                return null;

            ICanvasGeometryView bestObj = null;

            List<HitTestResult> listObjects = locView.GetAnnotations(position);

            HitTestResult bestHit = listObjects.NearestObjectOnCurrentSectionThenAdjacent(this.CurrentSectionNumber);

            //Use objects on our section, then other sections 
            if (bestHit != null)
            {
                distance = bestHit.Distance;
                bestObj = bestHit.obj as ICanvasGeometryView;

                /*Hit testing for containers should probably be cleaned up and incorporated into the GetAnnotationsAtPositions calls*/
                ICanvasViewContainer container = bestObj as ICanvasViewContainer;
                if (container != null)
                {
                    bestObj = container.GetAnnotationAtPosition(position) as ICanvasGeometryView;
                    if (bestObj != null)
                    {
                        distance = bestObj.DistanceFromCenterNormalized(position);
                        return bestObj;
                    }
                }
            }

            return bestObj;
        }

        /// <summary>
        /// Find the annotations intersecting the provided rectangle on the section, using annotation locations on the screen, not anatomical positions
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public List<HitTestResult> GetAnnotations(GridRectangle rect)
        {
            SectionAnnotationsView locView = GetAnnotationsForSection(CurrentSectionNumber);
            if (locView == null)
                return null;

            List<HitTestResult> listObjects = locView.GetAnnotations(rect);
            return listObjects;
        }

        /// <summary>
        /// Find the annotations intersecting the provided line on viewed section only, using annotation locations on the screen, not anatomical positions
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public static ICanvasView FirstIntersectedObjectOnSection(int CurrentSectionNumber, GridLineSegment line)
        {
            var listObjects = GetAnnotations(line, CurrentSectionNumber);

            HitTestResult bestHit = listObjects.NearestObjectOnCurrentSectionThenAdjacent(CurrentSectionNumber);

            if (bestHit == null)
                return null;
            //Use objects on our section, then other sections

            //This function does not detect contained objects

            return (ICanvasView)bestHit.obj;
        }

        public static List<HitTestResult> GetAnnotations(GridLineSegment line, int CurrentSectionNumber)
        {
            SectionAnnotationsView locView = GetAnnotationsForSection(CurrentSectionNumber);
            if (locView == null)
                return null;

            List<HitTestResult> listObjects = locView.GetLocations(line).Select(o => new HitTestResult(o, (int)o.Z, o.VisualHeight, o.DistanceFromCenterNormalized(line.A))).ToList();
            return listObjects;
        }

        public List<HitTestResult> GetAnnotations(GridLineSegment line)
        {
            return GetAnnotations(line, this.CurrentSectionNumber);
        }

        #region ISectionOverlayExtension Members

        public void SetParent(Viking.UI.Controls.SectionViewerControl parent)
        {
            //I'm only expecting this to be set once
            Debug.Assert(_Parent == null, "Not expecting parent to be set twice, OK to ignore, but annotation display may be incorrect");
            this._Parent = parent;

            //Load the locations for the current sections
            this._Parent.OnSectionChanged += new SectionChangedEventHandler(this.OnSectionChanged);
            Viking.UI.State.volume.TransformChanged += new TransformChangedEventHandler(this.OnVolumeTransformChanged);
            this._Parent.OnReferenceSectionChanged += new ReferenceSectionChangedEventHandler(this.OnReferenceSectionChanged);

            this._Parent.MouseDown += new MouseEventHandler(this.OnMouseDown);
            this._Parent.MouseMove += new MouseEventHandler(this.OnMouseMove);
            this._Parent.MouseUp += new MouseEventHandler(this.OnMouseUp);
            this._Parent.KeyDown += new KeyEventHandler(this.OnKeyDown);
            this._Parent.KeyUp += new KeyEventHandler(this.OnKeyUp);

            this._Parent.OnPenMove += new Viking.UI.PenEventHandler(this.OnPenMove);
            this._Parent.OnPenEnterRange += new Viking.UI.PenEventHandler(this.OnPenEnterRange);
            this._Parent.OnPenContact += new Viking.UI.PenEventHandler(this.OnPenContact);
            this._Parent.OnPenLeaveContact += new Viking.UI.PenEventHandler(this.OnPenLeaveContact);
            this._Parent.OnPenLeaveRange += new Viking.UI.PenEventHandler(this.OnPenLeaveRange);


            this._Parent.Camera.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(this.OnCameraPropertyChanged);
            //linksView = new LocationLinksViewModel(parent); 

            LoadSectionAnnotations(CancellationToken.None);
        }

        private void OnCameraPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            loadSectionAnnotationsCancellationTokenSource?.Cancel();
            loadSectionAnnotationsCancellationTokenSource = new CancellationTokenSource();
            System.Threading.Tasks.Task.Run(() => LoadSectionAnnotations(loadSectionAnnotationsCancellationTokenSource.Token),loadSectionAnnotationsCancellationTokenSource.Token);
        }

        protected void UpdateMouseCursor()
        {
            IMouseActionSupport loc = LastMouseOverObject as IMouseActionSupport; // GetNearestLocation(WorldPosition, out distance);
            if (loc != null)
            {
                long locID;
                GridVector2 WorldPosition = this.LastMouseMoveVolumeCoords;
                LocationAction action = loc.GetMouseClickActionForPositionOnAnnotation(WorldPosition, this.CurrentSectionNumber, Control.ModifierKeys, out locID);
                _Parent.Cursor = action.GetCursor();
            }
            else
            {
                _Parent.Cursor = Cursors.Default;
            }
        }


        protected void UpdatePenCursor()
        {
            IPenActionSupport loc = LastMouseOverObject as IPenActionSupport; // GetNearestLocation(WorldPosition, out distance);
            if (loc != null)
            {
                long locID;
                GridVector2 WorldPosition = this.LastMouseMoveVolumeCoords;
                LocationAction action = loc.GetPenContactActionForPositionOnAnnotation(WorldPosition, this.CurrentSectionNumber, Control.ModifierKeys, out locID);
                _Parent.Cursor = action.GetCursor();
            }
            else
            {
                _Parent.Cursor = Cursors.Default;
            }
        }

        protected bool IsCommandDefault()
        {
            if (_Parent.CurrentCommand == null)
                return true;

            //Check if there is a non-default command. we don't want to mess with another active command
            return _Parent.CurrentCommand.GetType() == typeof(Viking.UI.Commands.DefaultCommand) &&
             this.Parent.CommandQueue.QueueDepth == 0;
        }

        private bool RetraceAndReplaceDisabled = false;
        protected void OnMouseMove(object sender, MouseEventArgs e)
        {
            //Trace.WriteLine("On pen move");

            if (_Parent.CurrentCommand == null)
                return;

            //Check if there is a non-default command. we don't want to mess with another active command
            if (!IsCommandDefault())
                return;

            if (RetraceAndReplaceDisabled)
                RetraceAndReplaceDisabled = e.Button.Left();

            //Buttons being pushed means we are in the middle of a default command, probably scrolling, which won't affect the selection
            if (e.Button != MouseButtons.Left && e.Button != MouseButtons.None)
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

                InvalidateParent();
            }

            LastMouseOverObject = NextMouseOverObject;

            UpdateMouseCursor();
        }

        protected void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (_Parent.CurrentCommand == null)
                return;

            //Check if there is a non-default command. we don't want to mess with another active command
            if (!IsCommandDefault())
                return;

            //If locations aren't visible they can't be selected
            if (!_Parent.ShowOverlays)
                return;

            StopPenPath(); //If we are tracking the pen we should stop

            GridVector2 WorldPosition = _Parent.ScreenToWorld(e.X, e.Y);
            LastMouseDownCoords = WorldPosition;

            //Left mouse button selects objects
            if (e.Button == MouseButtons.Left)
            {
                if (Global.PenMode)
                {
                    //Id we don't have a command to start, begin creating a path
                    StartPenPath(WorldPosition);
                    return;
                }

                if (Viking.UI.State.SelectedObject is StructureType st)
                {
                    var action = LocationAction.CREATESTRUCTURE;
                    OnCreateStructure(st.ID, Array.Empty<string>(), LocationType.OPENCURVE);
                }
                else
                { 
                    double distance;
                    object obj = ObjectAtPosition(WorldPosition, out distance);
                    //Figure out if it is resizing a location circle
                    //If the loc is on this section we check if we are close to the edge and we are resizing.  Everyone else gets standard location command
                    Viking.UI.State.SelectedObject = obj as IUIObjectBasic;

                    /*If we select a link, find the location off the section and assume we have selected that*/
                    IMouseActionSupport actionSupportedObj = obj as IMouseActionSupport;

                    if (actionSupportedObj != null)
                    {
                        long LocationID;
                        LocationAction action =
                            actionSupportedObj.GetMouseClickActionForPositionOnAnnotation(WorldPosition,
                                this.CurrentSectionNumber, Control.ModifierKeys, out LocationID);

                        Viking.UI.Commands.Command command = action.CreateCommand(Parent,
                            Store.Locations.GetObjectByID(LocationID), WorldPosition);
                        if (command != null)
                        {
                            _Parent.CurrentCommand = command;
                        }
                    }
                    else if (CanContinueLastTrace)
                    {
                        //Check if we can continue another annotation, if not, check if we are in pen mode.  If we are start a new place polygon command.
                        OnContinueLastTrace(LastMouseDownCoords);
                    }
                }
            }
        }

        protected void OnPenEnterRange(object sender, Viking.UI.PenEventArgs e)
        {
        }

        protected void OnPenLeaveRange(object sender, Viking.UI.PenEventArgs e)
        {
        }

        protected void OnPenContact(object sender, Viking.UI.PenEventArgs e)
        {
            if (_Parent.CurrentCommand == null)
                return;

            //Check if there is a non-default command. we don't want to mess with another active command
            if (!IsCommandDefault())
                return;

            //If locations aren't visible they can't be selected
            if (!_Parent.ShowOverlays)
                return;

            if (e.Erase)
            {
                return;
            }

            GridVector2 WorldPosition = _Parent.ScreenToWorld(e.X, e.Y);

            if (this.PenPath == null)
            {
                double distance;
                object obj = ObjectAtPosition(WorldPosition, out distance);
                //Figure out if it is resizing a location circle
                //If the loc is on this section we check if we are close to the edge and we are resizing.  Everyone else gets standard location command
                Viking.UI.State.SelectedObject = obj as IUIObjectBasic;

                /*If we select a link, find the location off the section and assume we have selected that*/

                IPenActionSupport actionSupportedObj = obj as IPenActionSupport;

                if (actionSupportedObj != null)
                {
                    long LocationID;
                    LocationAction action = actionSupportedObj.GetPenContactActionForPositionOnAnnotation(WorldPosition,
                        this.CurrentSectionNumber, Control.ModifierKeys, out LocationID);

                    if (actionSupportedObj is LocationCanvasView viewObj)
                    {
                        var command =
                            action.CreateCommand(Parent, Store.Locations.GetObjectByID(LocationID), WorldPosition);
                        if (command != null)
                        {
                            _Parent.CurrentCommand = command;
                            return;
                        }
                    }
                    //var command = new AnnotationPenFreeDrawCommand(Parent, viewObj, Color.Green, Global.DefaultClosedLineWidth * Parent.Downsample, null);
                    //Viking.UI.Commands.Command command = action.CreateCommand(Parent, Store.Locations.GetObjectByID(LocationID), WorldPosition);
                }
            }

            /*
                if (command != null)
                {
                    _Parent.CurrentCommand = command;
                }
            */
            /*else if (Global.PenMode)
            {
                //Id we don't have a command to start, begin creating a path
                StartPenPath(WorldPosition);
            }*/
            /*}
            else
            {*/
            StartPenPath(WorldPosition);
            /*
                }
            }
            */
        }

        protected void OnPenLeaveContact(object sender, Viking.UI.PenEventArgs e)
        {
        }

        protected void OnPenMove(object sender, Viking.UI.PenEventArgs e)
        {
            //Trace.WriteLine("On pen move");

            if (_Parent.CurrentCommand == null)
                return;

            //Check if there is a non-default command. we don't want to mess with another active command
            if (!IsCommandDefault())
                return;


            GridVector2 WorldPosition = _Parent.ScreenToWorld(e.X, e.Y);
            this.LastMouseMoveVolumeCoords = WorldPosition;

            if (e.Erase || e.Inverted)
            {
                mouseOverEffect.viewObj = null;
                LastMouseOverObject = null;

                System.Windows.Forms.Cursor.Current = Cursors.Hand;
                _Parent.Cursor = Cursors.Hand;
            }
            else
            {
                double distance;
                ICanvasView NextMouseOverObject = ObjectAtPosition(WorldPosition, out distance) as ICanvasView;
                if (NextMouseOverObject != LastMouseOverObject)
                {
                    mouseOverEffect.viewObj = NextMouseOverObject;

                    InvalidateParent();
                }

                LastMouseOverObject = NextMouseOverObject;

                UpdatePenCursor();
            }
        }

        /// <summary>
        /// Begin recording the path of the cursor
        /// </summary>
        protected void StartPenPath(GridVector2 origin)
        {
            AnnotationOverlayPenFreeDrawCommandV2 cmd = new AnnotationOverlayPenFreeDrawCommandV2(this.Parent,
                Color.Yellow,
                Global.DefaultClosedLineWidth,
                (object sender, GridVector2[] points) =>
                {
                    AnnotationOverlayPenFreeDrawCommandV2 sender_cmd = (AnnotationOverlayPenFreeDrawCommandV2)sender;

                    OnPenPathCompleted(sender_cmd, points);
                });
            _Parent.CurrentCommand = cmd;
            /*
            System.Diagnostics.Debug.Assert(this.PenPath == null);
            this.PenPath = new Viking.UI.PenInputHelper(this.Parent);
            this.PenPath.Push(origin);
            this.PenPath.OnPathChanged += this.OnPenPathChanged;
            this.PenPath.OnPathCompleted += this.OnPenPathCompleted;*/
        }

        /// <summary>
        /// Stop recording the path of the cursor
        /// </summary>
        protected void StopPenPath()
        {
            if (PenPath != null)
            {
                this.PenPath.UnsubscribeEvents();
                this.PenPath.OnPathChanged -= this.OnPenPathChanged;
                this.PenPath.OnPathCompleted -= this.OnPenPathCompleted;
                this.PenPath = null;
            }
        }

        /// <summary>
        /// This is called when a AnnotationOverlayPenFreeDrawCommand is completed.  The overlay checks all of the shapes 
        /// intersected to determine the possible interactions.  We then present the options to the user to confirm.
        /// </summary>
        /// <param name="path"></param>
        protected void OnPenPathCompleted(AnnotationOverlayPenFreeDrawCommandV2 sender_cmd, GridVector2[] path)
        {
            List<IAction> actions = sender_cmd.PossibleActions == null ? new List<IAction>() : sender_cmd.PossibleActions.ToList();
            if (path.Length < 2)
            {
                //If we have a point and not a line just return
                return;
            }

            //Check if we should add actions to create new structures
            foreach (var favoriteStructureID in Global.UserFavoriteStructureTypes)
            {
                IAction new_action = null;
                if (sender_cmd.Path.HasSelfIntersection)
                {
                    new_action = new WebAnnotation.UI.Actions.Create2DStructureAction(System.Convert.ToInt64(favoriteStructureID), new GridPolygon(sender_cmd.Path.SimplifiedFirstLoop.EnsureClosedRing()), this.CurrentSectionNumber);
                }
                else
                {
                    new_action = new WebAnnotation.UI.Actions.Create1DStructureAction(System.Convert.ToInt64(favoriteStructureID), sender_cmd.Path.SimplifiedPath.ToPolyline(), this.CurrentSectionNumber);
                }

                actions.Add(new_action);
            }

            //ActionConfirmationCommand confirm_command = new ActionConfirmationCommand(this.Parent, actions, path.BoundingBox(), () => { return; });
            var confirm_command = ActionSelectionCanvasControl.CreateViews(this.Parent, actions.ToArray());
            _Parent.CurrentCommand = confirm_command;



            /*
            GridRectangle bounding_rect = path.BoundingBox();

            IShape2D shape = null;
            if(path.IsValidClosedRing())
            {
                shape = new GridPolygon(path);
            }
            else
            {
                shape = new GridPolyline(path, AllowSelfIntersection: true); 
            }

            var intersected = this.ObjectsAtPosition(bounding_rect).Where(i => (i as IPenActionSupport) != null).Select(i => (IPenActionSupport)i).ToArray();

            var intersected_locations = intersected.Select(i => i as IViewLocation).Where(i => i != null).ToArray();

            List<IAction> listActions = new List<IAction>();
            foreach(IPenActionSupport penInteractive in intersected)
            {
                var annotation_actions = penInteractive.GetPenActionsForShapeAnnotation(shape, intersected_locations, this.CurrentSectionNumber);
                if(annotation_actions != null)
                {
                    listActions.AddRange(annotation_actions);
                }
            }

            if (listActions.Count > 0)
            {
                //TODO: Call a command to display all of the actions with buttons and views
                ActionConfirmationCommand confirm_command = new ActionConfirmationCommand(Parent, listActions.ToArray(), null);
                _Parent.CurrentCommand = confirm_command;
            }*/

        }

        protected void OnMouseUp(object sender, MouseEventArgs e)
        {
        }

        protected void OnPenPathChanged(object sender, NotifyCollectionChangedEventArgs e)
        {

        }

        protected void OnPenPathCompleted(object sender, GridVector2[] Path)
        {
            /*
             * TODO: If the path is a valid shape for the last editted annotation we should continue the last trace. 
            if(CanContinueLastTrace)
            {
                LocationObj lastLoc = Store.Locations.GetObjectByID(Global.LastEditedAnnotationID.Value, false);
                switch(lastLoc.TypeCode)
                {
                    case LocationType.CURVEPOLYGON:
                    case LocationType.POLYGON:
                    case LocationType.CLOSEDCURVE:
                        //If we have a closed path type and the user drew a loop then create a new annotation linked to our last editted location.
                        if(PenPath.HasSelfIntersection)
                        {
                            OnContinueLastTrace
                        }
                        break;

                }
            }
            */

            StopPenPath();
        }

        private static string[] DefaultKeyHelpStrings()
        {
            return new string[]
            {
                "F3 or Enter Key: Create new annotation linked to the last placed annotation",
                "F5 Key: Reload section annotations",
                "Back Key: Return to last edited location",
                "F12: Open goto location ID dialog",
                "F11: Open goto structure ID dialog"
            };
        }

        private string[] BuildHotkeyHelpStrings()
        {
            List<string> hotkeyStrings = new List<string>(Global.UserSettings.Shortcuts.Hotkey.Count);
            foreach (Hotkey hkey in Global.UserSettings.Shortcuts.Hotkey)
            {
                string keystr = hkey.BuildModifierString() + hkey.KeyName + ": " + hkey.Action;
                hotkeyStrings.Add(keystr);
            }

            return hotkeyStrings.ToArray();
        }


        public void OpenGotoStructureForm()
        {
            if (GoToStructureForm == null)
            {
                GoToStructureForm = new UI.Forms.GoToActionForm();
                GoToStructureForm.Title = "Enter Structure ID";
                GoToStructureForm.IsValidInput = (ID) => Store.Structures.GetObjectByID(ID, true) != null;
                GoToStructureForm.OnGo = GoToStructure;
                GoToStructureForm.Closed += GoToStructureForm_Closed;
                System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(GoToStructureForm);
                GoToStructureForm.Show();
            }
            else
            {
                GoToStructureForm.Activate();
            }
        }

        public void OpenGotoLocationForm()
        {
            if (GoToLocationForm == null)
            {
                GoToLocationForm = new UI.Forms.GoToActionForm();
                GoToLocationForm.Title = "Enter Location ID";
                GoToLocationForm.IsValidInput = (ID) => Store.Locations.GetObjectByID(ID, true) != null;
                GoToLocationForm.OnGo = GoToLocation;
                GoToLocationForm.Closed += GoToLocationForm_Closed;
                System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(GoToLocationForm);
                GoToLocationForm.Show();
            }
            else
            {
                GoToLocationForm.Activate();
            }
        }

        protected void OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                //Refresh the annotations on F5
                case Keys.F5:
                    ResetAnnotationsAsync(CancellationToken.None);
                    return;
                case Keys.F3:
                    OnContinueLastTrace();
                    return;
                case Keys.F6:
                    var StructureIDChoiceForm = new WebAnnotation.UI.Forms.SelectStructureTypeForm();
                    Annotation.ViewModels.FavoriteStructureIDsViewModel favorite_view_model = new Annotation.ViewModels.FavoriteStructureIDsViewModel(Global.UserFavoriteStructureTypes);
                    StructureIDChoiceForm.DataContext = favorite_view_model;
                    System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(StructureIDChoiceForm);
                    StructureIDChoiceForm.Show();
                    return;
                case Keys.F7:
                    var StructureTypeManagementForm = new WebAnnotation.WPF.Forms.StructureTypeManagementForm();
                    StructureTypeManagementForm.DataContext = Store.StructureTypes;
                    System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(StructureTypeManagementForm);
                    StructureTypeManagementForm.Show();
                    return;
                case Keys.Enter:
                    OnContinueLastTrace();
                    return;
                case Keys.Back:
                    if (Global.LastEditedAnnotationID.HasValue)
                    {
                        LocationObj loc = Store.Locations.GetObjectByID(Global.LastEditedAnnotationID.Value);

                        if (loc != null)
                            Parent.GoToLocation(new Microsoft.Xna.Framework.Vector2((float)loc.Position.X,
                                                                                (float)loc.Position.Y),
                                                                                (int)loc.Z,
                                                                                true,
                                                                                (double)((loc.VolumeShape.BoundingBox().Width) / Parent.Width) * 2);

                    }
                    else
                    {
                        GotoLastModifiedLocation();
                    }
                    return;
                case Keys.ShiftKey:
                case Keys.ControlKey:
                    //TODO: Track if the last input was from pen or mouse and update accordingly
                    UpdateMouseCursor();
                    break;
                case Keys.F12:
                    OpenGotoLocationForm();
                    break;
                case Keys.F11:
                    OpenGotoStructureForm();
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
                        if (!IsCommandDefault())
                            return;

                        connectomes.utah.edu.XSD.WebAnnotationUserSettings.xsd.Action a = Global.UserSettings.Actions.Action.SingleOrDefault(action => action.Name == h.Action);
                        if (a != null)
                        {
                            System.Type commandType;
                            object[] parameters;

                            a.ExecuteAction(out commandType, out parameters);
                            if (commandType != null)
                            {
                                this.Parent.CommandQueue.EnqueueCommand(commandType, parameters);
                            }

                            return;
                        }

                        CreateStructureCommandAction comAction = Global.UserSettings.Actions.CreateStructureCommandAction.SingleOrDefault(action => action.Name == h.Action);
                        if (comAction != null)
                        {
                            OnCreateStructure(System.Convert.ToInt64(comAction.TypeID), comAction.AttributeList, comAction.GetLocationType());

                            return;
                        }

                        ToggleStructureTagCommandAction tagStructureAction = Global.UserSettings.Actions.ToggleStructureTagCommandAction.SingleOrDefault(action => action.Name == h.Action);
                        if (tagStructureAction != null)
                        {
                            OnToggleStructureTag(tagStructureAction.Tag, tagStructureAction.Value);

                            return;
                        }

                        ToggleLocationTagCommandAction tagLocationAction = Global.UserSettings.Actions.ToggleLocationTagCommandAction.SingleOrDefault(action => action.Name == h.Action);
                        if (tagLocationAction != null)
                        {
                            OnToggleLocationTag(tagLocationAction.Tag, tagLocationAction.Value);

                            return;
                        }

                        ToggleLocationTerminalCommandAction tagToggleTerminalAction = Global.UserSettings.Actions.ToggleLocationTerminalCommandAction.SingleOrDefault(action => action.Name == h.Action);
                        if (tagToggleTerminalAction != null)
                        {
                            OnToggleLocationTerminalTag();

                            return;
                        }

                        ChangeLocationAnnotationTypeAction tagChangeLocationAnnotationTypeAction = Global.UserSettings.Actions.ChangeLocationAnnotationTypeAction.SingleOrDefault(action => action.Name == h.Action);
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
            catch (Exception except)
            {
                Trace.WriteLine($"Error with hotkeys: {except}");
                throw;
            }

        }

        private void GoToStructureForm_Closed(object sender, EventArgs e)
        {  
            WebAnnotation.AnnotationOverlay.GoToStructureForm = null;
        }

        private void GoToLocationForm_Closed(object sender, EventArgs e)
        { 
            WebAnnotation.AnnotationOverlay.GoToLocationForm = null;
        }

        protected void OnKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Space:

                    //Only load the annotations for any section once so we can't fire multiple requests by pounding the spacebar
                    LoadSectionAnnotations(CancellationToken.None);
                    InvalidateParent();

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
                        QueuePlacementCommandForCircleStructure(this.Parent, newLocation, WorldPos, SectionPos, type.Color.SetAlpha(0.5f), false);
                        break;
                    case LocationType.OPENCURVE:
                        newLocation.Width = 8.0;
                        QueuePlacementCommandForOpenCurveStructure(this.Parent, newLocation, WorldPos, type.Color.SetAlpha(0.5f), LocationType.OPENCURVE, false);
                        break;
                    case LocationType.CLOSEDCURVE:
                        newLocation.Width = 8.0;
                        QueuePlacementCommandForClosedCurveStructure(this.Parent, newLocation, WorldPos, type.Color.SetAlpha(0.5f), AnnotationType, false);
                        break;
                    case LocationType.CURVEPOLYGON:
                        QueuePlacementCommandForPolygonStructure(this.Parent, newLocation, WorldPos, type.Color.SetAlpha(0.5f), AnnotationType, false);
                        break;
                    default:
                        Trace.WriteLine("Could not find commands for annotation type: " + AnnotationType.ToString());
                        return;
                }

                if (StructureNeedsParent)
                {
                    //Enqueue extra command to select a parent
                    this.Parent.CommandQueue.EnqueueCommand(typeof(LinkStructureToParentCommand), new object[] { Parent, newStruct, newLocation });
                }

                this.Parent.CommandQueue.EnqueueCommand(typeof(CreateNewStructureCommand), new object[] { Parent, newStruct, newLocation });
            }
            else
                Trace.WriteLine("Could not find hotkey ID for type: " + TypeID.ToString());
        }

        public static void QueuePlacementCommandForCircleStructure(Viking.UI.Controls.SectionViewerControl Parent, LocationObj newLocation, GridVector2 worldPos, GridVector2 sectionPos, System.Drawing.Color typecolor, bool SaveToStore)
        {
            Parent.CommandQueue.EnqueueCommand(typeof(ResizeCircleCommand), new object[] { Parent,
                    typecolor,
                    worldPos,
                    new ResizeCircleCommand.OnCommandSuccess((double radius) => {

                                    newLocation.TypeCode = LocationType.CIRCLE;
                                    LocationActions.UpdateCircleLocationCallback(newLocation, worldPos, sectionPos,
                                        radius); 
                                    newLocation.Width = null;

                                    if(SaveToStore)
                                        SaveLocationsWithMessageBoxOnError();
                    })});
        }

        public static void QueuePlacementCommandForOpenCurveStructure(Viking.UI.Controls.SectionViewerControl Parent, LocationObj newLocation, GridVector2 origin, System.Drawing.Color typecolor, LocationType typecode, bool SaveToStore)
        {
            double LineWidth = 16.0;
            if (Global.PenMode)
            {
                Parent.CommandQueue.EnqueueCommand(typeof(PlaceOpenCurveWithPenCommand), new object[] { Parent, typecolor, origin,  LineWidth,
                                                            new ControlPointCommandBase.OnCommandSuccess((object sender, GridVector2[] points) => {
                                                                    PlaceOpenCurveWithPenCommand cmd = (PlaceOpenCurveWithPenCommand)sender;
                                                                    newLocation.TypeCode = typecode;
                                                                    newLocation.Width = LineWidth;
                                                                    newLocation.SetShapeFromPointsInVolume(Parent.Section.ActiveSectionToVolumeTransform, cmd.PenInput.SimplifiedPath, null);
                                                                    if(SaveToStore)
                                                                        SaveLocationsWithMessageBoxOnError();
                                                            }) });
            }
            else
            {
                Parent.CommandQueue.EnqueueCommand(typeof(PlaceOpenCurveCommand), new object[] { Parent, typecolor, origin,  LineWidth,
                                                            new ControlPointCommandBase.OnCommandSuccess((object sender, GridVector2[] points) => {
                                                                    newLocation.TypeCode = typecode;
                                                                    newLocation.Width = LineWidth;
                                                                    newLocation.SetShapeFromPointsInVolume(Parent.Section.ActiveSectionToVolumeTransform, points, null);
                                                                    if(SaveToStore)
                                                                        SaveLocationsWithMessageBoxOnError();
                                                            }) });
            }
        }

        public static void QueuePlacementCommandForClosedCurveStructure(Viking.UI.Controls.SectionViewerControl Parent, LocationObj newLocation, GridVector2 origin, System.Drawing.Color typecolor, LocationType typecode, bool SaveToStore)
        {
            double LineWidth = 16.0;
            if (Global.PenMode)//Parent.FindForm() is WebAnnotation.UI.Forms.PenAnnotationViewForm)
            {
                Parent.CommandQueue.EnqueueCommand(typeof(PlaceClosedCurveWithPenCommand), new object[] { Parent, typecolor, origin, LineWidth,
                                                            new ControlPointCommandBase.OnCommandSuccess((object sender, GridVector2[] points) => {
                                                                    newLocation.TypeCode = typecode;
                                                                    newLocation.Width = LineWidth;
                                                                    newLocation.SetShapeFromPointsInVolume(Parent.Section.ActiveSectionToVolumeTransform, points, null);
                                                                    if(SaveToStore)
                                                                        SaveLocationsWithMessageBoxOnError();
                                                            }) });
            }
            else
            {
                Parent.CommandQueue.EnqueueCommand(typeof(PlaceClosedCurveCommand), new object[] { Parent, typecolor, origin, LineWidth,
                                                            new ControlPointCommandBase.OnCommandSuccess((object sender, GridVector2[] points) => {
                                                                    newLocation.TypeCode = typecode;
                                                                    newLocation.Width = LineWidth;
                                                                    newLocation.SetShapeFromPointsInVolume(Parent.Section.ActiveSectionToVolumeTransform, points, null);
                                                                    if(SaveToStore)
                                                                        SaveLocationsWithMessageBoxOnError();
                                                            }) });
            }

        }

        public static void QueuePlacementCommandForPolygonStructure(Viking.UI.Controls.SectionViewerControl Parent, LocationObj newLocation, GridVector2 origin, System.Drawing.Color typecolor, LocationType typecode, bool SaveToStore)
        {
            double LineWidth = 16.0;
            if (Global.PenMode)//Parent.FindForm() is WebAnnotation.UI.Forms.PenAnnotationViewForm)
            {
                Parent.CommandQueue.EnqueueCommand(typeof(PlaceClosedCurveWithPenCommand), new object[] { Parent, typecolor, origin, LineWidth,
                                                            new ControlPointCommandBase.OnCommandSuccess((object sender, GridVector2[] points) => {
                                                                    PlaceClosedCurveWithPenCommand cmd = sender as PlaceClosedCurveWithPenCommand;
                                                                    newLocation.TypeCode = typecode;
                                                                    newLocation.SetShapeFromPointsInVolume(Parent.Section.ActiveSectionToVolumeTransform, points, null);
                                                                    if(SaveToStore)
                                                                        SaveLocationsWithMessageBoxOnError();
                                                            }) });
            }
            else
            {


                Parent.CommandQueue.EnqueueCommand(typeof(PlaceClosedCurveCommand), new object[] { Parent, typecolor, origin, LineWidth,
                                                            new ControlPointCommandBase.OnCommandSuccess((object sender, GridVector2[] points) => {
                                                                    newLocation.TypeCode = typecode;
                                                                    newLocation.SetShapeFromPointsInVolume(Parent.Section.ActiveSectionToVolumeTransform, points, null);
                                                                    if(SaveToStore)
                                                                        SaveLocationsWithMessageBoxOnError();
                                                            }) });
            }
        }


        protected void OnToggleStructureTag(string tag, string value)
        {
            if (LastMouseOverObject == null)
            {
                Trace.WriteLine("No mouse over object to toggle tag");
                return;
            }

            LocationCanvasView loc = LastMouseOverObject as LocationCanvasView;
            if (loc == null)
            {
                Trace.WriteLine("No mouse over location to toggle tag");
                return;
            }

            //Convert special tag values

            Parent.CommandQueue.EnqueueCommand(typeof(ToggleStructureTag), new object[] { this.Parent, Store.Structures[loc.ParentID.Value], tag, TryConvertTagValue(value, out var _) });
            return;
        }

        protected void OnToggleLocationTag(string tag, string value)
        {
            if (LastMouseOverObject == null)
            {
                Trace.WriteLine("No mouse over object to toggle tag");
                return;
            }

            LocationCanvasView loc = LastMouseOverObject as LocationCanvasView;
            if (loc == null)
            {
                Trace.WriteLine("No mouse over location to toggle tag");
                return;
            }

            Parent.CommandQueue.EnqueueCommand(typeof(ToggleLocationTag), new object[] { this.Parent, Store.Locations[loc.ID], tag, TryConvertTagValue(value, out var _) });
            return;
        }

        protected string TryConvertTagValue(string input, out bool conversionFound)
        {
            conversionFound = false;
            if (string.IsNullOrWhiteSpace(input))
                return input;

            if (input[0] == '@')
            {
                conversionFound = true;
                string specialValueMapKey = input.Substring(1);
                switch (specialValueMapKey.ToLower())
                {
                    case "username":
                        return State.UserCredentials.UserName;
                    default:
                        conversionFound = false;
                        throw new ArgumentException(
                            $"Unknown custom value mapping {input} found in server's webannotationsettings.xml.  Please contact an admin to update the hotkey mapping on the server.");
                }
            }

            return input;
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

            Parent.CommandQueue.EnqueueCommand(typeof(ToggleLocationIsTerminalCommand), new object[] { this.Parent, Store.Locations[loc.ID] });

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

            if (loc.Z != this.CurrentSectionNumber)
            {
                Trace.WriteLine("Mouse over object on incorrect section to convert type");
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
                    QueuePlacementCommandForOpenCurveStructure(Parent, Store.Locations[loc.ID], LastMouseMoveVolumeCoords, loc.Parent.Type.Color, newLocType, true);
                    break;
                case LocationType.CLOSEDCURVE:
                    QueuePlacementCommandForClosedCurveStructure(Parent, Store.Locations[loc.ID], LastMouseMoveVolumeCoords, loc.Parent.Type.Color, LocationType.CLOSEDCURVE, true);
                    break;
                case LocationType.CURVEPOLYGON:
                    //TODO: Update the hotkeys at publish so we don't hardcode CURVEPOLYGON
                    QueuePlacementCommandForPolygonStructure(Parent, Store.Locations[loc.ID], LastMouseMoveVolumeCoords, loc.Parent.Type.Color, LocationType.CURVEPOLYGON, true);
                    break;
            }
        }



        protected bool CanContinueLastTrace
        {
            get
            {
                return Global.CanContinueLastTrace(this.CurrentSectionNumber);
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
            if (!Global.LastEditedAnnotationID.HasValue)
                return;

            LocationObj lastLoc = Store.Locations.GetObjectByID(Global.LastEditedAnnotationID.Value, true);
            {
                //This can occur if we deleted the last location we editted.
                if (lastLoc == null)
                    return;

                if (lastLoc.Z != this.CurrentSectionNumber && IsCommandDefault())
                {
                    Viking.UI.Commands.Command command = LocationAction.CREATELINKEDLOCATION.CreateCommand(this.Parent, lastLoc, WorldPos);
                    if (command != null)
                    {
                        _Parent.CurrentCommand = command;
                    }

                    Viking.UI.State.SelectedObject = null;
                    //LastMouseOverObject = null; 
                    Global.LastEditedAnnotationID = null;
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
            e.OldSection.TransformChanged -= this.OnSectionTransformChanged;
            e.NewSection.TransformChanged += this.OnSectionTransformChanged;

            //Don't load annotations when flipping sections if the user is holding down space bar to hide them
            if (_Parent.ShowOverlays)
            {
                loadSectionAnnotationsCancellationTokenSource?.Cancel();
                loadSectionAnnotationsCancellationTokenSource = new CancellationTokenSource();
                LoadSectionAnnotations(loadSectionAnnotationsCancellationTokenSource.Token);
                Task.Factory.StartNew(() => Store.Locations.FreeExcessSections(Global.NumSectionsInMemory, Global.NumSectionsLoading));
            }
        }

        protected void OnAnnotationChanged(object sender, EventArgs e)
        {
            //Trigger redraw of screen
            InvalidateParent();
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
                if (svm.ReferenceSectionAbove != null)
                {
                    if (changedSections.Contains(svm.ReferenceSectionAbove.Number))
                        AdjacentSections.Add(svm.Number);
                }

                if (svm.ReferenceSectionBelow != null)
                {
                    if (changedSections.Contains(svm.ReferenceSectionBelow.Number))
                        AdjacentSections.Add(svm.Number);
                }
            }

            foreach (int section in changedSections)
            {
                SectionAnnotationsView SLVModel = GetOrCreateAnnotationsForSection(section);
                SLVModel?.OnLocationsStoreChanged(sender, e);
            }

            foreach (int section in AdjacentSections)
            {
                if (!changedSections.Contains(section))
                {
                    SectionAnnotationsView SLVModel = GetOrCreateAnnotationsForSection(section);
                    SLVModel?.OnLocationsStoreChanged(sender, e);
                }
            }

            //Invalidate can always be called from any thread
            InvalidateParent();
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
                SectionAnnotationsView SLVModel = GetOrCreateAnnotationsForSection(section);
                SLVModel?.OnLocationLinksStoreChanged(sender, e);
            }
        }

        /// <summary>
        /// When this occurs we need to update the reference locations
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnReferenceSectionChanged(object sender, ReferenceSectionChangedEventArgs e)
        {
            ///This could be optimized, but it should be a rare event
            cacheSectionAnnotations.RemoveEntry(e.ChangedSection.Number);
            
            if (e.ChangedSection.Number == this.CurrentSectionNumber)
            {
                loadSectionAnnotationsCancellationTokenSource?.Cancel();
                loadSectionAnnotationsCancellationTokenSource = new CancellationTokenSource();
                LoadSectionAnnotations(loadSectionAnnotationsCancellationTokenSource.Token);
            }
        }

            /// <summary>
            /// When this occurs we should update the positions we draw the locations at. 
            /// </summary>
            /// <param name="sender"></param>
            /// <param name="e"></param>
            public void OnSectionTransformChanged(object sender, TransformChangedEventArgs e)
        {
            loadSectionAnnotationsCancellationTokenSource?.Cancel();
            loadSectionAnnotationsCancellationTokenSource = new CancellationTokenSource();
            ResetAnnotationsAsync(loadSectionAnnotationsCancellationTokenSource.Token);
        }

        /// <summary>
        /// When this occurs we should update the positions we draw the locations at. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnVolumeTransformChanged(object sender, TransformChangedEventArgs e)
        {
            loadSectionAnnotationsCancellationTokenSource?.Cancel();
            loadSectionAnnotationsCancellationTokenSource = new CancellationTokenSource();
            ResetAnnotationsAsync(loadSectionAnnotationsCancellationTokenSource.Token);
        }

        private async Task ResetAnnotationsAsync(CancellationToken token)
        {
            cacheSectionAnnotations.Clear();
            await LoadSectionAnnotations(token);
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

        private readonly SemaphoreSlim LoadSectionAnnotationsSemaphore = new SemaphoreSlim(1);
        protected async Task LoadSectionAnnotations(CancellationToken token)
        {
            if (Parent.Scene is null)
                return;

            try
            {
                await LoadSectionAnnotationsSemaphore.WaitAsync(token);
                if (token.IsCancellationRequested)
                    return;

                var sectionAnnotations = GetOrCreateAnnotationsForSection(_Parent.Section.Number);
                sectionAnnotations?.LoadAnnotationsInRegion(Parent.Scene, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            finally
            {
                LoadSectionAnnotationsSemaphore.Release();
            }
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

        private static BasicEffect basicEffect = null;
        private static BlendState defaultBlendState = null;

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
            ///     Draw the location links
            ///     Draw the structure links 
            ///     Draw the labels
            /// </summary>
            /// <param name="graphicsDevice"></param>
            /// <param name="scene"></param>
            /// <param name="BackgroundLuma"></param>
            /// <param name="BackgroundColors"></param>
            /// <param name="nextStencilValue"></param>

            if (_Parent.Section == null)
                return;

            if (_Parent.spriteBatch.GraphicsDevice.IsDisposed)
                return;


            BlendState originalBlendState = graphicsDevice.BlendState;

            UpdateSceneHistory(Parent.Scene);

            Matrix ViewProjMatrix = scene.Camera.View * scene.Projection;

            GridRectangle Bounds = scene.VisibleWorldBounds;

            DeviceStateManager.SetDepthStencilValue(graphicsDevice, nextStencilValue);

            int StartingStencilValue = nextStencilValue;

            if (basicEffect == null || basicEffect.IsDisposed)
            {
                basicEffect = new BasicEffect(graphicsDevice);
            }

            basicEffect.SetScene(scene);

            VikingXNAGraphics.OverlayShaderEffect overlayEffect = Parent.AnnotationOverlayEffect;

            basicEffect.Alpha = 1;

            RasterizerState OriginalRasterState = graphicsDevice.RasterizerState;
            SectionAnnotationsView currentSectionAnnotations = GetOrCreateAnnotationsForSection(_Parent.Section.Number);
            Debug.Assert(currentSectionAnnotations != null);

            int SectionNumber = _Parent.Section.Number;

            float Time = (float)TimeSpan.FromTicks(DateTime.Now.Ticks - DateTime.Today.Ticks).TotalSeconds;
            //            Debug.WriteLine("Time: " + Time.ToString()); 

            DeviceStateManager.SetDepthStencilValue(graphicsDevice, StartingStencilValue);

            ICollection<LocationCanvasView> Locations = currentSectionAnnotations.GetLocations(scene.VisibleWorldBounds);
            List<LocationCanvasView> listLocationsToDraw = FindVisibleLocations(Locations, scene);
            ICollection<LocationCanvasView> RefLocations = currentSectionAnnotations.AdjacentLocationsNotOverlappedInRegion(Bounds);
            List<LocationCanvasView> listVisibleNonOverlappingLocationsOnAdjacentSections = FindVisibleAdjacentLocations(RefLocations, scene); //Find the locations on the adjacent sections

            //Draw all of the locations on the current section
            WebAnnotation.LocationObjRenderer.DrawBackgrounds(listLocationsToDraw, graphicsDevice, basicEffect, overlayEffect, Parent.LumaOverlayLineManager, Parent.LumaOverlayCurveManager, scene, SectionNumber);

            nextStencilValue = DeviceStateManager.GetDepthStencilValue(graphicsDevice) + 1; //Record the next open stencil value for later draw calls that can overwrite the backgrounds
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, nextStencilValue);

            WebAnnotation.LocationObjRenderer.DrawBackgrounds(listVisibleNonOverlappingLocationsOnAdjacentSections, graphicsDevice, basicEffect, overlayEffect, Parent.LumaOverlayLineManager, Parent.LumaOverlayCurveManager, scene, SectionNumber);

            //Draw the LocationLinks, but use the starting stencil value so we don't overwrite any backgrounds
            nextStencilValue = DeviceStateManager.GetDepthStencilValue(graphicsDevice) + 1; //Record the next open stencil value for later draw calls that can overwrite the backgrounds
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, StartingStencilValue);

            //Get all the lines to draw first so the text and geometric shapes are over top of them
            LocationLinkView.Draw(graphicsDevice, scene, Parent.LumaOverlayLineManager, basicEffect, overlayEffect, currentSectionAnnotations.NonOverlappedLocationLinksInRegion(scene.VisibleWorldBounds));

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
            DeviceStateManager.SetRenderStateForShapes(graphicsDevice); //TODO: Why am I clobbering the defaultBlendState I just set...

            //Draw structure links over any annotations
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, nextStencilValue);

            List<StructureLinkViewModelBase> VisibleStructureLinks = currentSectionAnnotations.VisibleStructureLinks(scene);
            StructureLinkCirclesView.Draw(graphicsDevice, scene, Parent.LumaOverlayLineManager, VisibleStructureLinks.Where(l => l as StructureLinkCirclesView != null).Cast<StructureLinkCirclesView>().ToArray());
            StructureLinkCurvesView.Draw(graphicsDevice, scene, Parent.LumaOverlayLineManager, VisibleStructureLinks.Where(l => l as StructureLinkCurvesView != null).Cast<StructureLinkCurvesView>().ToArray());

            graphicsDevice.BlendState = defaultBlendState;

            //Draw text
            DrawLocationLabels(listLocationsToDraw, scene);

            DrawLocationLabels(listVisibleNonOverlappingLocationsOnAdjacentSections, scene);

            if (OriginalRasterState != null && !OriginalRasterState.IsDisposed)
                graphicsDevice.RasterizerState = OriginalRasterState;

            if (originalBlendState != null)
                graphicsDevice.BlendState = originalBlendState;
            //Make sure we update the nextStencilValue for the calling function ref parameter
            nextStencilValue++;// DeviceStateManager.GetDepthStencilValue(graphicsDevice) + 1;
        }

        private void DrawLocationLabels(ICollection<LocationCanvasView> locations, VikingXNA.Scene scene)
        {
            IEnumerable<ILabelView> listLocationsWithVisibleLabels = locations.Where(l =>
            {
                ILabelView iView = l as ILabelView;
                if (iView != null)
                {
                    return iView.IsLabelVisible(scene);
                }
                return false;
            }).Cast<ILabelView>();

            DeviceStateManager.SaveDeviceState(_Parent.Device);
            _Parent.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            foreach (ILabelView loc in listLocationsWithVisibleLabels)
            {
                loc.DrawLabel(_Parent.spriteBatch,
                              _Parent.fontArial,
                              scene);
            }
            _Parent.spriteBatch.End();

            IEnumerable<IRenderedLabelView> listLocationsWithVisibleRenderedLabels = locations.Where(l =>
            {
                IRenderedLabelView iView = l as IRenderedLabelView;
                if (iView != null)
                {
                    return iView.IsLabelVisible(scene);
                }
                return false;
            }).Cast<IRenderedLabelView>();

            foreach (IRenderedLabelView loc in listLocationsWithVisibleRenderedLabels)
            {
                loc.DrawLabel(Parent.Device,
                              _Parent.spriteBatch,
                              _Parent.fontArial,
                              scene);
            }

            DeviceStateManager.RestoreDeviceState(_Parent.Device);
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

        public LocationAction GetPenContactActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, Keys ModifierKeys, out long LocationID)
        {
            throw new NotImplementedException();
        }

        public List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber)
        {
            throw new NotImplementedException();
            //If we didn't overlap an existing annotation then create a new structure
            /*if(interaction_log.All(e => e.Annotation == null))
            {
            }
            */
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
