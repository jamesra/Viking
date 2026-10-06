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
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.View
{
    internal class LocationPolygonView : LocationCanvasView, ILabelView, ICanvasViewContainer, Viking.Common.IHelpStrings, IColorView
    {
        private StructureCircleLabels curveLabels;
        private OverlappedLinkCircleView OverlappedLinkView;
        private LocationInteriorHoleView[] InteriorHoleViews;
        private SolidPolygonView polygonMesh;
        private readonly Polygon VolumePolygon;
        private Polygon SmoothedVolumePolygon;
        private readonly PointSetView ControlPointView;

        public override string[] HelpStrings
        {
            get
            {
                List<string> listStrings = [];
                if (Global.PenMode)
                {
                    listStrings.Add("Pen: Draw across the shape to replace the boundary");
                    listStrings.Add("Shift: Move the shape or create a link");
                    listStrings.Add("Ctrl: Cut a hole or remove a hole");
                }
                else
                {
                    listStrings.Add("SHIFT + Hold Left Button near the interior: Move shape");
                    listStrings.Add("SHIFT + Hold Left Button near edge: Create link");
                    listStrings.Add("CTRL + Left click off control point: Add a control point");
                    listStrings.Add("CTRL + Left click on control point: Remove control point");
                }

                return [.. listStrings];
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
        /// <summary>
        /// Non-null only when this view was built because a pen command changed the boundary of an existing
        /// polygon; collects the timings written by <see cref="PolygonViewTimingLog"/>.
        /// </summary>
        private readonly PolygonViewTiming timing;

        public LocationPolygonView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj)
        {
            long constructionStart = Stopwatch.GetTimestamp();
            timing = PolygonViewTimingLog.TryBegin(obj.ID);
            _ControlPointRadius = Global.AnnotationSettings.PolygonPointRadius;
            var mappedShape = mapper.TryMapShapeSectionToVolume(obj.MosaicShape);
            if (mappedShape is null)
            {
                throw new ArgumentException($"Could not map location {obj.ID} to volume");
            }

            VolumePolygon = mappedShape.ToPolygon();
            //_ControlPointRadius = GetRadiusFromPolygonArea(VolumePolygon, 0.01);
            SmoothedVolumePolygon = VolumePolygon;//VolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
            bool hasParent = obj.Parent?.ParentID.HasValue ?? false;
            float opacity = Global.AnnotationSettings.GetOpacityForAnnotationType(obj.TypeCode, hasParent);
            if (obj.Parent is null)
            {
                Color = Color.Gray.SetAlpha(opacity);
            }
            else
            {
                Color = ColorForStructure(obj.Parent, opacity);
            }

            ControlPointView = new PointSetView(GetControlPointColor(), Global.AnnotationSettings.PolygonPointRadius)
            {
                Points = GetAllPolygonVertices(VolumePolygon)
            };
            ControlPointView.UpdateViews();

            timing?.RecordConstruction(
                Stopwatch.GetTimestamp() - constructionStart,
                VolumePolygon.TotalUniqueVertices,
                VolumePolygon.InteriorPolygons.Count);

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

        /// <summary>
        /// Called when the background <see cref="Initialize"/> task faults. The view stays uninitialized and is skipped by
        /// every draw, so the failure is traced and, for pen-edit views, written to <see cref="PolygonViewTimingLog"/>.
        /// </summary>
        internal void ReportInitializeFailure(Exception ex)
        {
            Trace.WriteLine($"LocationPolygonView.Initialize failed for location {ID}: {ex}");
            timing?.RecordInitializeFailure(ex);
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

            long initializeStart = Stopwatch.GetTimestamp();
            ControlPointView.Points = GetAllPolygonVertices(VolumePolygon);
            ControlPointView.PointRadius = Global.AnnotationSettings.PolygonPointRadius;
            ControlPointView.UpdateViews();

            long smoothStart = Stopwatch.GetTimestamp();
            try
            {
                SmoothedVolumePolygon = VolumePolygon.Smooth(Global.NumClosedCurveInterpolationPointsForDisplay);
            }
            catch (ArgumentException)
            {
                Trace.WriteLine($"Unable to smooth volume polygon: {ID}");
                SmoothedVolumePolygon = VolumePolygon;
            }

            long smoothTicks = Stopwatch.GetTimestamp() - smoothStart;

            SolidPolygonView mesh = new(SmoothedVolumePolygon, HSLColor);
            // Must be set before polygonMesh is published: the triangulation starts on the first draw.
            mesh.MeshBuildCompleted = timing is null ? null : timing.RecordTriangulation;
            mesh.MeshBuildStarted = timing is null ? null : timing.RecordTriangulationStarted;
            polygonMesh = mesh;
            // Warm the cut-fill cache while the cell is on screen so a later retrace does not triangulate it on the pen thread.
            WebAnnotation.UI.Commands.PolygonCutFillCache.Begin(SmoothedVolumePolygon);
            CreateLabelObjects();

            InteriorHoleViews = new LocationInteriorHoleView[VolumePolygon.InteriorPolygons.Count];
            for (int iInner = 0; iInner < VolumePolygon.InteriorPolygons.Count; iInner++)
            {
                InteriorHoleViews[iInner] = new LocationInteriorHoleView(modelObj.ID, iInner,
                    VolumePolygon.InteriorPolygons[iInner],
                    SmoothedVolumePolygon.InteriorPolygons[iInner]);
            }

            timing?.RecordInitialization(
                smoothTicks,
                Stopwatch.GetTimestamp() - initializeStart - smoothTicks,
                SmoothedVolumePolygon.TotalUniqueVertices);

            Interlocked.Exchange(ref _Initialized, 1);
            Interlocked.Exchange(ref _Initializing, 0);

            return Task.CompletedTask;
        }

        public static double GetRadiusFromPolygonArea(Polygon poly, double percentage)
        {
            double circleArea = poly.Area * percentage;
            double radius = Math.Sqrt(circleArea / Math.PI);
            return radius;
        }

        private Circle? _InscribedCircle;
        protected Circle InscribedCircle
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

        public void CreateLabelObjects() => curveLabels = new StructureCircleLabels(modelObj, InscribedCircle);

        /// <summary>
        /// Return a collection of Geometry.Vector2s containing the location of every vertex
        /// </summary>
        /// <param name="polygon"></param>
        /// <returns></returns>
        private ICollection<Geometry.Vector2> GetAllPolygonVertices(Polygon polygon)
        {
            if (polygon is null)
            {
                return [];
            }

            List<Geometry.Vector2> vertices = [];

            // Add exterior ring vertices (excluding last duplicate point)
            if (polygon.ExteriorRing is { Length: > 0 })
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
            foreach (Polygon innerPoly in polygon.InteriorPolygons)
            {
                ICollection<Geometry.Vector2> innerVertices = GetAllPolygonVertices(innerPoly);
                vertices.AddRange(innerVertices);
            }

            return vertices;
        }

        private SqlGeometry _RenderedVolumeShape;
        public override SqlGeometry VolumeShapeAsRendered => _RenderedVolumeShape ??= VolumePolygon.ToSqlGeometry();

        /// <summary>
        /// We have this because with the current renderings the control points are circles that fall outside the polygon we use to render the closed curves
        /// </summary> 
        public override Geometry.Rectangle BoundingBox => Geometry.Rectangle.Pad(SmoothedVolumePolygon.BoundingBox, ControlPointRadius);

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundCurve.CurveManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          OverlayShaderEffect overlayEffect,
                          LocationPolygonView[] listToDraw)
        {

            listToDraw = InitializedViews(listToDraw);

            List<OverlappedLinkCircleView>? overlappedList = null;
            for (int i = 0; i < listToDraw.Length; i++)
            {
                OverlappedLinkCircleView overlapped = listToDraw[i].OverlappedLinkView;
                if (overlapped != null && overlapped.IsVisible(scene))
                    (overlappedList ??= []).Add(overlapped);
            }

            OverlappedLinkCircleView[] overlappedLocations = overlappedList is null ? [] : [.. overlappedList];
            OverlappedLinkCircleView.Draw(device, scene, basicEffect, overlayEffect, overlappedLocations);

            double radius_scalar = Math.Sqrt((double)scene.Camera.Downsample);
            double expected_radius = Global.AnnotationSettings.PolygonPointRadius * radius_scalar;

            //Todo: Check if control points will be visible.
#if DEBUG
            for (int i = 0; i < listToDraw.Length; i++)
            {
                LocationPolygonView lpv = listToDraw[i];
                if (lpv.ControlPointView is null)
                    continue;

                if(Math.Abs(lpv.ControlPointRadius - expected_radius) > 0.001)
                    lpv.ControlPointRadius = expected_radius;

                lpv.ControlPointView.Draw(device, scene, OverlayStyle.Alpha);
            }
#else
            if (!Global.PenMode)
            {
                for (int i = 0; i < listToDraw.Length; i++)
                {
                    LocationPolygonView lpv = listToDraw[i];
                    if (lpv.ControlPointView is null)
                        continue;

                    if (lpv.ControlPointRadius != Global.AnnotationSettings.PolygonPointRadius)
                        lpv.ControlPointRadius = Global.AnnotationSettings.PolygonPointRadius;

                    lpv.ControlPointView.Draw(device, scene, OverlayStyle.Luma);
                }
            }
#endif
            //CurveView.Draw(device, scene, lineManager, basicEffect, overlayEffect, 0, listToDraw.Select(l => l.curveView).ToArray());

            //MeshView<VertexPositionColor>.Draw(device, scene, DeviceEffectsStore<PolygonOverlayEffect>.TryGet(device), meshmodels: listToDraw.Select(l => l.polygonMesh));
            SolidPolygonView[] meshes = new SolidPolygonView[listToDraw.Length];
            for (int i = 0; i < meshes.Length; i++)
                meshes[i] = listToDraw[i].polygonMesh;

            SolidPolygonView.Draw(device, scene, OverlayStyle.Luma, meshes);
            //FilledClosedCurvePolygonView.Draw(device, scene, listToDraw.Select(l => l.polyView));
        }

        /// <summary>
        /// The views that have finished initializing, in their original order. Returns the same array when all of them have,
        /// which is the usual case.
        /// </summary>
        /// <remarks>
        /// Views initialize on other threads, so each view's state is read once per pass and the result is never sized from an
        /// earlier read.
        /// </remarks>
        private static LocationPolygonView[] InitializedViews(LocationPolygonView[] views)
        {
            int firstUninitialized = -1;
            for (int i = 0; i < views.Length; i++)
            {
                if (!views[i].Initialized)
                {
                    firstUninitialized = i;
                    break;
                }
            }

            if (firstUninitialized < 0)
                return views;

            List<LocationPolygonView> initialized = new(views.Length);
            for (int i = 0; i < firstUninitialized; i++)
                initialized.Add(views[i]);

            for (int i = firstUninitialized + 1; i < views.Length; i++)
            {
                if (views[i].Initialized)
                    initialized.Add(views[i]);
            }

            return [.. initialized];
        }

        public override bool Contains(Geometry.Vector2 Position)
        {
            if (!BoundingBox.Covers(Position))
            {
                return false;
            }

            //Test if we are over a control point
            if (Global.PenMode == false)
            {
                if (SmoothedVolumePolygon.ExteriorRing.Any(p => new Circle(p, lineWidth / 2.0).Covers(Position)))
                {
                    return true;
                }
            }

            if (OverlappedLinkView != null && OverlappedLinkView.Contains(Position))
            {
                return true;
            }

            if (SmoothedVolumePolygon.Covers(Position))
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

        public override bool Intersects(LineSegment line)
        {
            if (!BoundingBox.Intersects(line.BoundingBox))
            {
                return false;
            }

            /*
            //Test if we are over a control point
            if (Global.PenMode == false)
            {
                if (this.SmoothedVolumePolygon.ExteriorRing.Any(p => new Circle(p, lineWidth / 2.0).Intersects(line)))
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
            OverlappedLinkView?.DrawLabel(spriteBatch, font, scene);
            curveLabels.DrawLabel(spriteBatch, font, scene);
        }

        public ICanvasView GetAnnotationAtPosition(Geometry.Vector2 position)
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
                if (OverlappedLinkView is null)
                {
                    return new long[0];
                }

                return OverlappedLinkView.OverlappedLinks;
            }

            set
            {
                if (value is null || value.Count == 0)
                {
                    OverlappedLinkView = null;
                    return;
                }

                OverlappedLinkView = new OverlappedLinkCircleView(InscribedCircle, ID, (int)Z, value)
                {
                    Color = Color
                };

                CreateLabelObjects();
            }
        }


        public LocationAction GetMouseClickActionForPositionOnAnnotationWithPen(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
            => PolygonPenModeContact.Action(SmoothedVolumePolygon, InscribedCircle, ID, modelObj.Z, WorldPosition, VisibleSectionNumber, ModifierKeys, out LocationID);

        public LocationAction GetMouseClickActionForPositionOnAnnotationWithoutPen(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {

            LocationID = ID;
            Polygon intersectingPoly; //Could be our polygon or an interior polygon

            if (ModifierKeys.ShiftPressed())
            {
                if (SmoothedVolumePolygon.Covers(WorldPosition))
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
                else if (SmoothedVolumePolygon.Covers(WorldPosition))
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
                    else if (SmoothedVolumePolygon.Covers(WorldPosition))
                    {
                        Circle TranslateTargetCircle = new(InscribedCircle.Center, InscribedCircle.Radius / 2.0);
                        if (TranslateTargetCircle.Covers(WorldPosition))
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

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
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

        public override LocationAction GetPenContactActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID) => GetMouseClickActionForPositionOnAnnotationWithPen(WorldPosition, VisibleSectionNumber, ModifierKeys, out LocationID);

        internal override void OnParentPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            if (IsParentPropertyAffectingLabels(args.PropertyName))
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

        public override double DistanceFromCenterNormalized(Geometry.Vector2 Position)
        {
            if (SmoothedVolumePolygon.Covers(Position))
            {
                return 0.5;
            }
            else
            {
                return 1.01; //This is done so we can fill interior polygons without overlapping annotations inside the polygon hole.
            }
        }

        /// <summary>
        /// The cut-hole choice for a closed pen loop drawn entirely inside this polygon's exterior ring
        /// and clear of every existing interior hole. Returns an empty list otherwise, including when the
        /// loop touches a hole (that case is a replace-hole choice, not a new hole).
        /// Called by the overlay after a stroke because the stroke log only names annotations the path crossed,
        /// and a loop wholly inside a polygon crosses nothing.
        /// </summary>
        public IReadOnlyList<CutHoleAction> GetCutHoleActionsForLoop(Path path)
        {
            if (Initialized == false || Z != AnnotationOverlay.CurrentOverlay.CurrentSectionNumber || !TypeCode.AllowsInteriorHoles())
            {
                return [];
            }

            return [.. Shared2DShapeActionsForPath.IdentifyPossibleInteriorActions(ID, VolumePolygon, SmoothedVolumePolygon, path).OfType<CutHoleAction>()];
        }

        public override List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber)
        {
            if (Initialized == false)
            {
                return [];
            }

            List<IAction> listActions = [];
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
                    Polyline line = new(path.SimplifiedPath);
                    ChangeToPolylineAction action = new(modelObj, line);
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

    /// <summary>
    /// Pen Mode mouse and hardware-pen contact for a polygon.
    /// Unmodified hits return <see cref="LocationAction.NONE"/> so the overlay starts free-draw;
    /// retrace is chosen on the confirmation ring after the stroke.
    /// Ctrl still cuts or fills holes. Shift still translates or links.
    /// Kept off <see cref="LocationPolygonView"/> so callers can evaluate a hit without loading that view's type initializer.
    /// </summary>
    internal static class PolygonPenModeContact
    {
        public static LocationAction Action(
            Polygon smoothedVolumePolygon,
            Circle inscribedCircle,
            long locationId,
            double sectionZ,
            Geometry.Vector2 worldPosition,
            int visibleSectionNumber,
            System.Windows.Forms.Keys modifierKeys,
            out long actionLocationId)
        {
            actionLocationId = locationId;

            if (modifierKeys.ShiftPressed())
            {
                if (visibleSectionNumber == (int)sectionZ)
                {
                    if (smoothedVolumePolygon.Covers(worldPosition))
                    {
                        Circle translateTargetCircle = new(inscribedCircle.Center, inscribedCircle.Radius / 2.0);
                        if (translateTargetCircle.Covers(worldPosition))
                        {
                            return LocationAction.TRANSLATE;
                        }

                        return LocationAction.CREATELINK;
                    }
                }
            }
            else if (modifierKeys.CtrlPressed())
            {
                if (smoothedVolumePolygon.Covers(worldPosition))
                {
                    return LocationAction.CUTHOLE;
                }

                if (smoothedVolumePolygon.InteriorPolygonContains(worldPosition))
                {
                    return LocationAction.REMOVEHOLE;
                }
            }

            return LocationAction.NONE;
        }
    }
}
