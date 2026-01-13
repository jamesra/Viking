using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.VolumeModel;
using VikingXNA;
using VikingXNAGraphics;
using WebAnnotation.UI;
using WebAnnotation.UI.Actions;
using WebAnnotationModel;

namespace WebAnnotation.View
{
    internal class LocationPolygonView : LocationCanvasView, ILabelView, ICanvasViewContainer, Viking.Common.IHelpStrings, IColorView
    {
        private StructureCircleLabels curveLabels;
        private OverlappedLinkCircleView OverlappedLinkView;
        private LocationInteriorHoleView[] InteriorHoleViews;
        private SolidPolygonView polygonMesh;
        private readonly GridPolygon VolumePolygon;
        private GridPolygon SmoothedVolumePolygon;
        private PointSetView ControlPointView;

        public override string[] HelpStrings
        {
            get
            {
                List<string> listStrings = new List<string>();
                if (Global.PenMode)
                {
                    listStrings.Add("Hold Left Click + SHIFT drag the interior: Move shape");
                    listStrings.Add("Hold Left Click + SHIFT drag near edge: Create link");
                    listStrings.Add("Draw path across shape: Replace annotation boundary");
                }
                else
                {
                    listStrings.Add("SHIFT + Hold Left Button near the interior: Move shape");
                    listStrings.Add("SHIFT + Hold Left Button near edge: Create link");
                    listStrings.Add("SHIFT + Left Click and drag: Move shape");
                    listStrings.Add("CTRL + Left click off control point: Add a control point");
                    listStrings.Add("CTRL + Left click on control point: Remove control point");
                }

                return listStrings.ToArray();
            }
        }

        private Color _Color;

        public Microsoft.Xna.Framework.Color Color
        {
            get => _Color;
            set
            {
                _Color = value;
                if (polygonMesh != null)
                {
                    polygonMesh.Color = value.ConvertToHCL();
                    if (ControlPointView != null)
                    {
                        ControlPointView.Color = GetControlPointColor();
                        ControlPointView.UpdateViews();
                    }
                }
            }
        }

        public Microsoft.Xna.Framework.Color HSLColor => _Color.ConvertToHCL();

                /// <summary>
        /// Calculates a control point color that maintains the same hue as the polygon
        /// but inverts the luma (brightness) for better visibility and contrast.
        /// Uses perceptual luma (0.3R + 0.59G + 0.11B) to match human vision.
        /// Uses more aggressive contrast to ensure points are clearly visible.
        /// </summary>
        private Microsoft.Xna.Framework.Color GetControlPointColor()
        {
            // Calculate perceptual luma of the polygon color
            float r = (float)_Color.R / 255f;
            float g = (float)_Color.G / 255f;
            float b = (float)_Color.B / 255f;
            float currentLuma = 0.3f * r + 0.59f * g + 0.11f * b;

            // More aggressive contrast: push to extremes (0.1 for dark, 0.9 for light)
            float targetLuma = currentLuma > 0.5f ? 0.1f : 0.9f;

            // Handle edge cases with still-good contrast
            if (currentLuma < 0.05f)
                targetLuma = 0.85f; // Very dark polygon -> very light points
            else if (currentLuma > 0.95f)
                targetLuma = 0.15f; // Very light polygon -> very dark points

            // Calculate the difference needed to reach target luma
            float lumaDifference = targetLuma - currentLuma;

            // To preserve hue, add/subtract the same value from all RGB components
            // This maintains the relative ratios between R, G, B (which defines hue)
            // Clamp to [0,1] to stay within valid RGB range
            float newR = Math.Max(0.0f, Math.Min(1.0f, r + lumaDifference));
            float newG = Math.Max(0.0f, Math.Min(1.0f, g + lumaDifference));
            float newB = Math.Max(0.0f, Math.Min(1.0f, b + lumaDifference));

            // If we hit the caps, boost saturation for better visibility
            // while maintaining approximate hue
            float maxComponent = Math.Max(newR, Math.Max(newG, newB));
            float minComponent = Math.Min(newR, Math.Min(newG, newB));
            float chroma = maxComponent - minComponent;

            // If there's color (chroma > 0), boost saturation for visibility
            if (chroma > 0.01f)
            {
                // Boost saturation by reducing the minimum component
                // This makes colors more vibrant while preserving hue
                float saturationBoost = 0.25f; // 25% saturation boost
                float boostAmount = minComponent * saturationBoost;
                
                // Reduce the minimum component to increase saturation
                if (Math.Abs(newR - minComponent) < 0.001f) 
                    newR = Math.Max(0.0f, newR - boostAmount);
                else if (Math.Abs(newG - minComponent) < 0.001f) 
                    newG = Math.Max(0.0f, newG - boostAmount);
                else if (Math.Abs(newB - minComponent) < 0.001f) 
                    newB = Math.Max(0.0f, newB - boostAmount);
            }

            return new Microsoft.Xna.Framework.Color(
                (byte)(newR * 255f),
                (byte)(newG * 255f),
                (byte)(newB * 255f),
                _Color.A
            );
        }

        public float Alpha
        {
            get => polygonMesh.Alpha;
            set
            {
                polygonMesh.Alpha = value;
                if (ControlPointView != null)
                {
                    ControlPointView.Alpha = value;
                    ControlPointView.UpdateViews();
                }
            }
        }

        private double _ControlPointRadius;

        public double ControlPointRadius
        {
            get => _ControlPointRadius;
            set
            {
                if (Math.Abs(_ControlPointRadius - value) > 0.01)
                {
                    _ControlPointRadius = value;
                    if (Initialized && ControlPointView != null)
                    {
                        ControlPointView.PointRadius = value;
                        ControlPointView.UpdateViews();
                    }
                }
            }
        }


        public double lineWidth = 32;

        public static uint NumInterpolationPoints = Global.NumClosedCurveInterpolationPoints;
        public LocationPolygonView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj)
        {
            _ControlPointRadius = Global.AnnotationSettings.PolygonPointRadius;
            VolumePolygon = mapper.TryMapShapeSectionToVolume(obj.MosaicShape)?.ToPolygon();
            //_ControlPointRadius = GetRadiusFromPolygonArea(VolumePolygon, 0.01);
            SmoothedVolumePolygon = VolumePolygon;//VolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
            if (obj.Parent == null)
            {
                Color = Color.Gray.SetAlpha(Global.AnnotationSettings.PolygonOpacityParentless);
            }
            else if (obj.Parent.TypeID == 1) //Cells get a random color for polygons to help Becca see Glia
            {
                Color = obj.Parent.Color.ToXNAColor(Global.AnnotationSettings.PolygonOpacityWithParent);
            }
            else
            {
                Color = obj.Parent.Type.Color.ToXNAColor(Global.AnnotationSettings.PolygonOpacityWithParent);
            }

            ControlPointView = new PointSetView(GetControlPointColor(), Global.AnnotationSettings.PolygonPointRadius)
            {
                Points = GetAllPolygonVertices(VolumePolygon)
            };
            ControlPointView.UpdateViews();

            //polygonMesh = TriangleNetExtensions.CreateMeshForPolygon2D(SmoothedVolumePolygon, this.HSLColor);
            //polygonMesh = SmoothedVolumePolygon.CreateMeshForPolygon2D(this.HSLColor);
            //polygonMesh = new SolidPolygonView(SmoothedVolumePolygon, this.HSLColor);


            /*InteriorHoleViews = new LocationInteriorHoleView[VolumePolygon.InteriorPolygons.Count];
            for (int iInner = 0; iInner < VolumePolygon.InteriorPolygons.Count; iInner++)
            {
                InteriorHoleViews[iInner] = new LocationInteriorHoleView(obj.ID, iInner,
                    VolumePolygon.InteriorPolygons[iInner],
                    SmoothedVolumePolygon.InteriorPolygons[iInner]);
            }
            */
        }

        private int _Initializing = 0;
        private int _Initialized = 0;
        private bool Initialized => _Initialized > 0;
        public Task Initialize()
        {
            //If initialized move on
            if (Interlocked.CompareExchange(ref _Initialized, _Initialized, 1) > 0)
            {
                return Task.CompletedTask;
            }

            //If another thread is initializing, move on
            if (Interlocked.CompareExchange(ref _Initializing, 1, 0) > 0)
            {
                return Task.CompletedTask;
            }

            ControlPointView.Points = GetAllPolygonVertices(VolumePolygon);
            ControlPointView.PointRadius = Global.AnnotationSettings.PolygonPointRadius;
            ControlPointView.UpdateViews();

            try
            {
                SmoothedVolumePolygon = VolumePolygon.Smooth(Global.NumClosedCurveInterpolationPointsForDisplay);
            }
            catch (ArgumentException)
            {
                Trace.WriteLine($"Unable to smooth volume polygon: {ID}");
                SmoothedVolumePolygon = VolumePolygon;
            }

            polygonMesh = new SolidPolygonView(SmoothedVolumePolygon, HSLColor);
            CreateLabelObjects();

            InteriorHoleViews = new LocationInteriorHoleView[VolumePolygon.InteriorPolygons.Count];
            for (int iInner = 0; iInner < VolumePolygon.InteriorPolygons.Count; iInner++)
            {
                InteriorHoleViews[iInner] = new LocationInteriorHoleView(modelObj.ID, iInner,
                    VolumePolygon.InteriorPolygons[iInner],
                    SmoothedVolumePolygon.InteriorPolygons[iInner]);
            }


            Interlocked.Exchange(ref _Initialized, 1);
            Interlocked.Exchange(ref _Initializing, 0);

            return Task.CompletedTask;
        }

        public static double GetRadiusFromPolygonArea(GridPolygon poly, double percentage)
        {
            double circleArea = poly.Area * percentage;
            double radius = Math.Sqrt(circleArea / Math.PI);
            return radius;
        }

        private GridCircle? _InscribedCircle;
        protected GridCircle InscribedCircle
        {
            get
            {
                if (!_InscribedCircle.HasValue)
                {
                    _InscribedCircle = SmoothedVolumePolygon.InscribedCircle();
                }

                return _InscribedCircle.Value;
            }
        }

        public void CreateLabelObjects()
        {
            curveLabels = new StructureCircleLabels(modelObj, InscribedCircle);
        }

        /// <summary>
        /// Return a collection of GridVector2s containing the location of every vertex
        /// </summary>
        /// <param name="polygon"></param>
        /// <returns></returns>
        private ICollection<GridVector2> GetAllPolygonVertices(GridPolygon polygon)
        {
            List<GridVector2> vertices = new List<GridVector2>();
            
            // Add exterior ring vertices (excluding last duplicate point)
            if (polygon.ExteriorRing.Length > 0)
            {
                int count = polygon.ExteriorRing.Length;
                // Exclude last point if it's duplicate of first
                if (count > 1 && polygon.ExteriorRing[0] == polygon.ExteriorRing[count - 1])
                {
                    count--;
                }
                for (int i = 0; i < count; i++)
                {
                    vertices.Add(polygon.ExteriorRing[i]);
                }
            }

            // Add interior polygon vertices recursively
            foreach (GridPolygon innerPoly in polygon.InteriorPolygons)
            {
                ICollection<GridVector2> innerVertices = GetAllPolygonVertices(innerPoly);
                vertices.AddRange(innerVertices);
            }

            return vertices;
        }

        private SqlGeometry _RenderedVolumeShape;
        public override SqlGeometry VolumeShapeAsRendered => _RenderedVolumeShape ?? (_RenderedVolumeShape = VolumePolygon.ToSqlGeometry());

        /// <summary>
        /// We have this because with the current renderings the control points are circles that fall outside the polygon we use to render the closed curves
        /// </summary> 
        public override GridRectangle BoundingBox => GridRectangle.Pad(SmoothedVolumePolygon.BoundingBox, ControlPointRadius);

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundCurve.CurveManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          OverlayShaderEffect overlayEffect,
                          LocationPolygonView[] listToDraw)
        {

            listToDraw = listToDraw.Where(l => l.Initialized).ToArray();
            OverlappedLinkCircleView[] overlappedLocations = listToDraw.Select(l => l.OverlappedLinkView).Where(l => l != null && l.IsVisible(scene)).ToArray();
            OverlappedLinkCircleView.Draw(device, scene, basicEffect, overlayEffect, overlappedLocations);

            double radius_scalar = Math.Sqrt((double)scene.Camera.Downsample);
            double expected_radius = Global.AnnotationSettings.PolygonPointRadius * radius_scalar;
             
            //Todo: Check if control points will be visible.
#if DEBUG
            foreach (var lpv in listToDraw.Where(lpv => lpv.ControlPointView != null))
            {
                if(Math.Abs(lpv.ControlPointRadius - expected_radius) > 0.001)
                    lpv.ControlPointRadius = expected_radius;

                lpv.ControlPointView.Draw(device, scene, OverlayStyle.Alpha);
            }
#else
            if(!Global.PenMode)
            {
                foreach (var lpv in listToDraw.Where(lpv => lpv.ControlPointView != null))
                {
                    if(lpv.ControlPointRadius != Global.AnnotationSettings.PolygonPointRadius)
                        lpv.ControlPointRadius = Global.AnnotationSettings.PolygonPointRadius;

                    lpv.ControlPointView.Draw(device, scene, OverlayStyle.Luma);
                }
            }
#endif
            //CurveView.Draw(device, scene, lineManager, basicEffect, overlayEffect, 0, listToDraw.Select(l => l.curveView).ToArray());

            //MeshView<VertexPositionColor>.Draw(device, scene, DeviceEffectsStore<PolygonOverlayEffect>.TryGet(device), meshmodels: listToDraw.Select(l => l.polygonMesh));
            SolidPolygonView.Draw(device, scene, OverlayStyle.Luma, listToDraw.Select(l => l.polygonMesh));
            //FilledClosedCurvePolygonView.Draw(device, scene, listToDraw.Select(l => l.polyView));
        }

        public override bool Contains(GridVector2 Position)
        {
            if (!BoundingBox.Contains(Position))
            {
                return false;
            }

            //Test if we are over a control point
            if (Global.PenMode == false)
            {
                if (SmoothedVolumePolygon.ExteriorRing.Any(p => new GridCircle(p, lineWidth / 2.0).Contains(Position)))
                {
                    return true;
                }
            }

            if (OverlappedLinkView != null && OverlappedLinkView.Contains(Position))
            {
                return true;
            }

            if (SmoothedVolumePolygon.Contains(Position))
            {
                return true;
            }

            //If the UI doesn't detect a hole as part of the annotation then it becomes impossible to close holes in the UI.  
            //On the other hand, a location link inside the hole is unselectable. 
            //The workaround was to assign a distance > 1 when the point falls outside the polygon.
            if (SmoothedVolumePolygon.InteriorPolygonContains(Position))
            {
                return true;
            }

            return false;
        }

        public override bool Intersects(GridLineSegment line)
        {
            if (!BoundingBox.Intersects(line.BoundingBox))
            {
                return false;
            }

            /*
            //Test if we are over a control point
            if (Global.PenMode == false)
            {
                if (this.SmoothedVolumePolygon.ExteriorRing.Any(p => new GridCircle(p, lineWidth / 2.0).Intersects(line)))
                    return true;
            }*/

            if (OverlappedLinkView != null && OverlappedLinkView.Intersects(line))
            {
                return true;
            }

            if (SmoothedVolumePolygon.Intersects(line))
            {
                return true;
            }

            return false;
        }

        public void DrawLabel(SpriteBatch spriteBatch, SpriteFont font, Scene scene)
        {
            if (OverlappedLinkView != null)
            {
                OverlappedLinkView.DrawLabel(spriteBatch, font, scene);
            }
            curveLabels.DrawLabel(spriteBatch, font, scene);
        }

        public ICanvasView GetAnnotationAtPosition(GridVector2 position)
        {
            if (Initialized == false)
            {
                return null;
            }

            if (OverlappedLinkView != null)
            {
                ICanvasView containedAnnotation = OverlappedLinkView.GetAnnotationAtPosition(position);
                if (containedAnnotation != null)
                {
                    return containedAnnotation;
                }
            }

            if (InteriorHoleViews != null)
            {
                foreach (LocationInteriorHoleView interiorHole in InteriorHoleViews)
                {
                    if (interiorHole.Contains(position))
                    {
                        return interiorHole;
                    }
                }
            }

            if (Contains(position))
            {
                return this;
            }

            return null;
        }

        public override ICollection<long> OverlappedLinks
        {
            protected get
            {
                if (OverlappedLinkView == null)
                {
                    return new long[0];
                }

                return OverlappedLinkView.OverlappedLinks;
            }

            set
            {
                if (value == null || value.Count == 0)
                {
                    OverlappedLinkView = null;
                }

                OverlappedLinkView = new OverlappedLinkCircleView(InscribedCircle, ID, (int)Z, value)
                {
                    Color = Color
                };

                CreateLabelObjects();
            }
        }


        public LocationAction GetMouseClickActionForPositionOnAnnotationWithPen(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {
            LocationID = ID;

            if (ModifierKeys.ShiftPressed())
            {
                if (VisibleSectionNumber == (int)modelObj.Z)
                {
                    if (SmoothedVolumePolygon.Contains(WorldPosition))
                    {
                        GridCircle TranslateTargetCircle = new GridCircle(InscribedCircle.Center, InscribedCircle.Radius / 2.0);
                        if (TranslateTargetCircle.Contains(WorldPosition))
                        {
                            LocationID = ID;
                            return LocationAction.TRANSLATE;
                        }

                        return LocationAction.CREATELINK;
                    }
                }
            }
            else if (ModifierKeys.CtrlPressed())
            {
                //Check to see if we are on a line segment to add/remove control points.  Otherwise cut a hole
                if (SmoothedVolumePolygon.Contains(WorldPosition))
                {
                    LocationID = ID;
                    return LocationAction.CUTHOLE;
                }
                else if (SmoothedVolumePolygon.InteriorPolygonContains(WorldPosition))
                {
                    LocationID = ID;
                    return LocationAction.REMOVEHOLE;
                }
            }
            else if (!ModifierKeys.ShiftOrCtrlPressed())
            {
                return LocationAction.CHANGEBOUNDARY;
            }

            return LocationAction.NONE;
        }

        public LocationAction GetMouseClickActionForPositionOnAnnotationWithoutPen(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {

            LocationID = ID;
            GridPolygon intersectingPoly; //Could be our polygon or an interior polygon

            if (ModifierKeys.ShiftPressed())
            {
                if (SmoothedVolumePolygon.Contains(WorldPosition))
                {
                    return LocationAction.TRANSLATE;
                }
            }
            else if (ModifierKeys.CtrlPressed())
            {
                //Check to see if we are on a line segment to add/remove control points.  Otherwise cut a hole
                if (SmoothedVolumePolygon.PointIntersectsAnyPolygonSegment(WorldPosition, ControlPointRadius, out intersectingPoly))
                {
                    if (VolumePolygon.PointIntersectsAnyPolygonVertex(WorldPosition, ControlPointRadius, out intersectingPoly))
                    {
                        //Cannot have a polygon with fewer than 4 verticies, We check for 4 because first and last vertex are the same.
                        if (intersectingPoly.ExteriorRing.Length > 4)
                        {
                            return LocationAction.REMOVECONTROLPOINT;
                        }
                        else
                        {
                            return LocationAction.NONE;
                        }
                    }
                    else
                    {
                        return LocationAction.ADDCONTROLPOINT;
                    }
                }
                else if (SmoothedVolumePolygon.Contains(WorldPosition))
                {
                    LocationID = ID;
                    return LocationAction.CUTHOLE;
                }
                else if (SmoothedVolumePolygon.InteriorPolygonContains(WorldPosition))
                {
                    LocationID = ID;
                    return LocationAction.REMOVEHOLE;
                }
            }
            else if (!ModifierKeys.ShiftOrCtrlPressed())
            {
                if (VisibleSectionNumber == (int)modelObj.Z)
                {
                    if (!Global.PenMode && VolumePolygon.PointIntersectsAnyPolygonVertex(WorldPosition, ControlPointRadius, out intersectingPoly))
                    {
                        return LocationAction.ADJUST;
                    }
                    else if (SmoothedVolumePolygon.Contains(WorldPosition))
                    {
                        GridCircle TranslateTargetCircle = new GridCircle(InscribedCircle.Center, InscribedCircle.Radius / 2.0);
                        if (TranslateTargetCircle.Contains(WorldPosition))
                        {
                            LocationID = ID;
                            return LocationAction.TRANSLATE;
                        }

                        return LocationAction.CREATELINK;
                    }
                    else if (Global.PenMode && SmoothedVolumePolygon.InteriorPolygonContains(WorldPosition))
                    {
                        return LocationAction.CHANGEBOUNDARY;
                    }
                    else
                    {
                        return LocationAction.CREATELINKEDLOCATION;
                    }
                }
            }

            return LocationAction.NONE;
        }

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {

            if (Global.PenMode)
            {
                return GetMouseClickActionForPositionOnAnnotationWithPen(WorldPosition, VisibleSectionNumber, ModifierKeys, out LocationID);
            }
            else
            {
                return GetMouseClickActionForPositionOnAnnotationWithoutPen(WorldPosition, VisibleSectionNumber, ModifierKeys, out LocationID);
            }
        }

        public override LocationAction GetPenContactActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {
            return GetMouseClickActionForPositionOnAnnotationWithPen(WorldPosition, VisibleSectionNumber, ModifierKeys, out LocationID);
        }

        internal override void OnParentPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == "Label" || args.PropertyName == "Attributes")
            {
                CreateLabelObjects();
            }

            base.OnParentPropertyChanged(o, args);
        }

        internal override void OnObjPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            //ClearOverlappingLinkedLocationCache();A

            //CreateViewObjects();
            if (IsLocationPropertyAffectingLabels(args.PropertyName))
            {
                CreateLabelObjects();
            }
        }

        public bool IsLabelVisible(Scene scene)
        {
            if (Initialized == false)
            {
                return false;
            }

            return curveLabels.IsLabelVisible(scene);
        }

        public override bool IsVisible(Scene scene)
        {
            if (Initialized == false)
            {
                return false;
            }

            return LocationCanvasView.IsPolygonVisible(BoundingBox, scene);
        }

        public override double DistanceFromCenterNormalized(GridVector2 Position)
        {
            if (SmoothedVolumePolygon.Contains(Position))
            {
                return 0.5;
            }
            else
            {
                return 1.01; //This is done so we can fill interior polygons without overlapping annotations inside the polygon hole.
            }
        }

        public override List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber)
        {
            if (Initialized == false)
            {
                return new List<IAction>();
            }

            List<IAction> listActions = new List<IAction>();
            if (path.HasSelfIntersection)
            {
                //This could be a reshape or linking to an adjacent annotation
                if (Z == VisibleSectionNumber)
                {
                    listActions.AddRange(Shared2DShapeActionsForPath.IdentifyPossibleInteriorActions(ID, VolumePolygon, SmoothedVolumePolygon, path));
                    listActions.AddRange(Shared2DShapeActionsForPath.GetPenActionsForShapeAnnotation(this, SmoothedVolumePolygon, path, interaction_log, VisibleSectionNumber));
                }
            }
            else
            {
                if (Z == VisibleSectionNumber)
                {
                    //Ask if they want to convert to a polyline
                    GridPolyline line = new GridPolyline(path.SimplifiedPath);
                    ChangeToPolylineAction action = new ChangeToPolylineAction(modelObj, line);
                    listActions.Add(action);

                    //Check if they cross the shape at two points and want to adjust the shape
                    listActions.AddRange(Shared2DShapeActionsForPath.GetPenActionsForShapeAnnotation(this, SmoothedVolumePolygon, path, interaction_log, VisibleSectionNumber));
                }
            }

            //Check for links to create
            listActions.AddRange(interaction_log.IdentifyPossibleLinkActions(modelObj.ID));
            return listActions;
        }
    }
}
