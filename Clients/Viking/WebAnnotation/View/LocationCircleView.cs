using Geometry;
using Viking.Input;
using Rectangle = Geometry.Rectangle;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Viking.VolumeModel;
using VikingXNA;
using VikingXNAGraphics;
using WebAnnotation.UI;
using WebAnnotation.UI.Actions;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.View
{
    internal abstract class LocationCircleViewBase(LocationObj obj) : LocationCanvasView(obj), ILabelView
    {
        public virtual double Radius => MosaicCircle.Radius;

        private SqlGeometry? _VolumeShape = null;
        public override SqlGeometry VolumeShapeAsRendered
        {
            get
            {
                _VolumeShape ??= VolumeCircle.ToSqlGeometry(Z);
                return _VolumeShape;
            }
        }

        public abstract Circle MosaicCircle { get; }

        public abstract Circle VolumeCircle { get; }

        public override Rectangle BoundingBox => VolumeCircle.BoundingBox;

        /// <summary>
        /// True if the point is on or inside the circle
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override bool Contains(Geometry.Vector2 Position) => VolumeCircle.Covers(Position);

        /// <summary>
        /// True if the point is on or inside the circle
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override bool Intersects(LineSegment line) => VolumeCircle.Intersects(line);

        public override bool Intersects(SqlGeometry shape)
        {
            ///If it is a circle, use the fast comparison
            switch (shape.GeometryType())
            {
                case SupportedGeometryType.CURVEPOLYGON:
                    Circle circle = shape.ToCircle();
                    return VolumeCircle.Intersects(circle);
                case SupportedGeometryType.POINT:
                    Geometry.Vector2 point = new(shape.STX.Value, shape.STY.Value);
                    return VolumeCircle.Covers(point);
                default:
                    return VolumeShapeAsRendered.STIntersects(shape).IsTrue;
            }
        }

        /// <summary>
        /// Distance to the nearest point on circle if outside, otherwise zero
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override double Distance(Geometry.Vector2 Position) => VolumeCircle.Distance(Position);

        public override double DistanceFromCenterNormalized(Geometry.Vector2 Position) => Geometry.Vector2.Distance(Position, VolumeCircle.Center) / Radius;

        public double DistanceToCenter(Geometry.Vector2 Position) => Geometry.Vector2.Distance(Position, VolumeCircle.Center);


        public abstract void DrawLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              Scene scene);

        public abstract bool IsLabelVisible(Scene scene);
    }

    /// <summary>Adjacent-section circle or inscribed-circle proxy. Click is CREATELINKEDLOCATION, not the on-section concentric zones.</summary>
    internal class AdjacentLocationCircleView : LocationCircleViewBase, IColorView
    {
        public TextureCircleView upCircleView;
        public TextureCircleView downCircleView;
        public StructureCircleLabels structureLabels;

        protected readonly Circle _VolumeCircle;
        protected readonly Circle _MosaicCircle;

        public override Circle MosaicCircle => _MosaicCircle;

        public override Circle VolumeCircle => _VolumeCircle;

        private readonly ICollection<long> _OverlappedLinks;
        public override ICollection<long> OverlappedLinks
        {
            protected get => _OverlappedLinks;

            set => throw new NotImplementedException();
        }

        public AdjacentLocationCircleView(LocationObj obj, IVolumeToSectionTransform mapper, double Radius) : base(obj)
        {
            _MosaicCircle = new Circle(obj.Position, Radius);
            _VolumeCircle = new Circle(mapper.SectionToVolume(_MosaicCircle.Center), _MosaicCircle.Radius);

            CreateViewObjects(MosaicCircle, mapper);
            CreateLabelObjects();
        }

        public AdjacentLocationCircleView(LocationObj obj, IVolumeToSectionTransform mapper) : base(obj)
        {
            _MosaicCircle = new Circle(obj.Position, obj.Radius * Global.AdjacentLocationRadiusScalar);
            _VolumeCircle = new Circle(mapper.SectionToVolume(_MosaicCircle.Center), _MosaicCircle.Radius);

            CreateViewObjects(MosaicCircle, mapper);
            CreateLabelObjects();
        }

        public AdjacentLocationCircleView(LocationObj obj, Circle mosaicCircle, IVolumeToSectionTransform mapper) : base(obj)
        {
            _MosaicCircle = mosaicCircle;
            _VolumeCircle = new Circle(mapper.SectionToVolume(_MosaicCircle.Center), _MosaicCircle.Radius);

            CreateViewObjects(MosaicCircle, mapper);
            CreateLabelObjects();
        }

        /// <summary>
        /// We scale down the radius when the location is on an adjacent section
        /// </summary>
        public override double Radius => VolumeCircle.Radius;

        public Color Color
        {
            get => upCircleView.Color;

            set
            {
                upCircleView.Color = value;
                downCircleView.Color = value;
            }
        }

        public float Alpha
        {
            get => upCircleView.Alpha;

            set
            {
                upCircleView.Alpha = value;
                downCircleView.Alpha = value;
            }
        }

        private void CreateViewObjects(Circle MosaicCircle, IVolumeToSectionTransform mapper)
        {
            var color = (modelObj.Parent?.Type?.Color ?? 0x808080u).ToXNAColor(0.5f);
            upCircleView = TextureCircleView.CreateUpArrow(_VolumeCircle, color);
            downCircleView = TextureCircleView.CreateDownArrow(_VolumeCircle, color);
        }

        private void CreateLabelObjects() => structureLabels = new StructureCircleLabels(modelObj, VolumeCircle, false);

        #region overrides

        public override bool IsVisible(VikingXNA.Scene scene) => !modelObj.IsVerifiedTerminal && upCircleView.IsVisible(scene);

        public override bool IsLabelVisible(VikingXNA.Scene scene) => structureLabels.IsLabelVisible(scene);

        public override LocationAction GetPenContactActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            LocationID = ID;
            return LocationAction.CREATELINKEDLOCATION;
        }


        public override LocationAction GetMouseClickActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            LocationID = ID;

            if (modifierKeys.ShiftOrCtrlPressed())
            {
                return LocationAction.NONE;
            }

            double distance = DistanceToCenter(WorldPosition);
            if (distance > Radius)
            {
                return LocationAction.NONE;
            }

            return LocationAction.CREATELINKEDLOCATION;
        }

        public override List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber)
        {
            List<IAction> list = [];

            if ((path.HasSelfIntersection && TypeCode.AllowsClosed2DShape()) ||
               (path.HasSelfIntersection == false && TypeCode.AllowsOpen2DShape()))
            {
                //Both are closed shapes, so allow continuing a linked annotation
                IVolumeToSectionTransform Transform = WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform;

                //TODO: Check our location links to make sure the shape does not intersect an existing annotation of the same structure on this section
                IShape2D mosaic_shape;
                IShape2D volume_shape;

                if (path.HasSelfIntersection)
                {
                    Polygon poly = new(path.SimplifiedFirstLoop);
                    volume_shape = poly;
                    mosaic_shape = Transform.TryMapShapeVolumeToSection(poly);
                }
                else
                {
                    Polyline line = new(path.SimplifiedPath, false);
                    volume_shape = line;
                    mosaic_shape = Transform.TryMapShapeVolumeToSection(line);
                }

                CreateNewLinkedLocationAction NewLinkedLocationAction = new(ID, mosaic_shape, volume_shape, VisibleSectionNumber, Transform);
                list.Add(NewLinkedLocationAction);
            }

            return list;
        }

        public override string[] HelpStrings => [
                    "Hold left click + drag on inscribed arrow: Create additional annotation for this structure linked to the annotation on the adjacent section."
                ];

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          BasicEffect basicEffect,
                          OverlayShaderEffect overlayEffect,
                          AdjacentLocationCircleView[] listToDraw,
                          int VisibleSectionNumber)
        {
            TextureCircleView[] backgroundCircles = [.. listToDraw.Select(l => l.modelObj.Z < VisibleSectionNumber ? l.downCircleView : l.upCircleView)];
            TextureCircleView.Draw(device, scene, OverlayStyle.Luma, backgroundCircles);
        }

        public override void DrawLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              VikingXNA.Scene scene) => structureLabels.DrawLabel(spriteBatch, font, scene);/*
            if (font is null)
                throw new ArgumentNullException("font");

            if (spriteBatch is null)
                throw new ArgumentNullException("spriteBatch");

            float MagnificationFactor = (float)(1.0 / scene.Camera.Downsample);
            double DesiredRowsOfText = 6.0;
            double DefaultFontSize = (this.Radius * 2) / DesiredRowsOfText;
            StructureIDLabelView.FontSize = DefaultFontSize;
            StructureIDLabelView.Position = modelObj.VolumePosition - new Geometry.Vector2(0.0, this.Radius / 3.0f);
            StructureIDLabelView.Draw(spriteBatch, font, scene);

            return; 
            */

        #endregion 
    }

    /// <summary>On-section circle. OverlappedLinkCircleView children win hit-test over this parent.</summary>
    internal class LocationCircleView : LocationCircleViewBase, ICanvasViewContainer, ISelectable, IColorView, ILabelView
    {
        protected readonly Circle _VolumeCircle;
        protected readonly Circle _MosaicCircle;

        public override Circle MosaicCircle => _MosaicCircle;

        public override Circle VolumeCircle => _VolumeCircle;

        public Color Color
        {
            get => circleView.Color;

            set
            {
                circleView.Color = value;
                if (OverlappedLinkView != null)
                    OverlappedLinkView.Color = value;
            }
        }

        public float Alpha
        {
            get => circleView.Alpha;

            set
            {
                if (circleView.Alpha != value)
                {
                    circleView.Alpha = value;
                    if (OverlappedLinkView != null)
                        OverlappedLinkView.Alpha = value;
                }
            }
        }

        public CircleView circleView;

        public OverlappedLinkCircleView OverlappedLinkView;
        public StructureCircleLabels structureLabels;
        private static readonly float RadiusToResizeCircle = 7.0f / 8.0f;
        private static readonly float RadiusToPenResizeCircle = 1.0f / 8.0f;
        private static readonly float RadiusToLinkCircle = 1.75f / 4.0f;
        private static readonly double BeginFadeCutoff = 0.1;
        private static readonly double InvisibleCutoff = 1f;

        public LocationCircleView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj)
        {
            _MosaicCircle = new Circle(obj.Position, obj.Radius);
            _VolumeCircle = new Circle(mapper.SectionToVolume(_MosaicCircle.Center), _MosaicCircle.Radius);

            //RegisterForLocationEvents();
            //RegisterForStructureChangeEvents();
            CreateViewObjects(_MosaicCircle, mapper);
            CreateLabelObjects();
        }

        private void CreateViewObjects(Circle MosaicCircle, IVolumeToSectionTransform mapper)
        {
            Geometry.Vector2 VolumePosition = mapper.SectionToVolume(MosaicCircle.Center);
            bool hasParent = modelObj.Parent?.ParentID.HasValue ?? false;
            float opacity = Global.AnnotationSettings.GetOpacityForAnnotationType(modelObj.TypeCode, hasParent);
            Color color = modelObj.Parent is null
                ? Color.Gray.SetAlpha(opacity)
                : modelObj.Parent.Type.Color.ToXNAColor(opacity);
            circleView = new CircleView(new Circle(VolumePosition, modelObj.Radius), color);
        }

        private void CreateLabelObjects() => structureLabels = new StructureCircleLabels(modelObj, VolumeCircle);


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
                }

                OverlappedLinkView = new OverlappedLinkCircleView(circleView.Circle, ID, (int)Z, value)
                {
                    Color = Color
                };

                CreateLabelObjects();
            }
        }


        #region overrides

        public override bool IsVisible(VikingXNA.Scene scene) => circleView.IsVisible(scene) && GetAlphaFadeScalarForScene(scene) > 0;

        public override bool IsLabelVisible(VikingXNA.Scene scene) => structureLabels.IsLabelVisible(scene);

        public override LocationAction GetPenContactActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            LocationID = ID;

            if (modifierKeys.ShiftOrCtrlPressed())
            {
                return LocationAction.NONE;
            }

            if (VisibleSectionNumber == (int)modelObj.Z)
            {
                double distance = DistanceToCenter(WorldPosition);
                if (distance <= (Radius * RadiusToPenResizeCircle))
                {
                    return LocationAction.SCALETRANSLATE;
                }
            }

            return LocationAction.NONE;
        }


        public override List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber)
        {
            List<IAction> listActions = [];
            if (path.HasSelfIntersection)
            {
                if (Z == VisibleSectionNumber)
                {
                    Polygon closedpath = new(path.SimplifiedFirstLoop);
                    ChangeToPolygonAction action = new(modelObj, closedpath);
                    listActions.Add(action);

                    if (VolumeCircle.Covers(closedpath))
                    {
                        CutHoleAction cutHoleAction = new(modelObj, closedpath);
                        listActions.Add(cutHoleAction);
                    }
                }
            }
            else
            {
                if (Z == VisibleSectionNumber)
                {
                    Polyline line = new(path.SimplifiedPath);
                    ChangeToPolylineAction action = new(modelObj, line);
                    listActions.Add(action);

                    /*SortedDictionary<double, PointIndex> intersectedSegments = this.VolumeShapeAsRendered.IntersectingSegments(path.ToLineSegments());

                    if (intersectedSegments.Count >= 2)
                    {

                    }*/
                }


            }

            //Check for links to create
            listActions.AddRange(interaction_log.IdentifyPossibleLinkActions(modelObj.ID));
            return listActions;
        }

        /// <summary>
        /// Concentric zones on this section: overlap child, then SCALE (outer), CREATELINK, TRANSLATE (center).
        /// Throws if VisibleSectionNumber is not this location's section — adjacent circles use AdjacentLocationCircleView.
        /// </summary>
        public override LocationAction GetMouseClickActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            LocationID = ID;

            if (modifierKeys.ShiftOrCtrlPressed())
            {
                return LocationAction.NONE;
            }

            double distance = DistanceToCenter(WorldPosition);

            if (OverlappedLinkView != null)
            {
                if (OverlappedLinkView.Contains(WorldPosition))
                {
                    return LocationAction.CREATELINKEDLOCATION;
                }
            }

            if (VisibleSectionNumber == (int)modelObj.Z)
            {
                if (distance > Radius)
                {
                    return LocationAction.NONE;
                }
                else if (distance >= (Radius * RadiusToResizeCircle))
                {
                    return LocationAction.SCALE;
                }
                else if (distance >= (Radius * RadiusToLinkCircle))
                {
                    return LocationAction.CREATELINK;
                }
                else
                {
                    return LocationAction.TRANSLATE;
                }
            }

            throw new ArgumentException("Wrong section for location");
        }

        public override string[] HelpStrings => [
                    "Hold left click on circle edge: Resize",
                    "Hold left click + drag on inscribed arrow: Create additional annotation for this structure linked to the annotation on the adjacent section.",
                    "Hold left click on circle center: Move annotation"
                ];


        #endregion

        public override double Radius => VolumeCircle.Radius;

        private bool _Selected = false;
        public bool Selected
        {
            get => _Selected;

            set
            {
                circleView.Alpha = value ? 0.25f : 0.5f;

                _Selected = value;
            }
        }

        #region Linked Locations


        public ICanvasView GetAnnotationAtPosition(Geometry.Vector2 position)
        {
            ICanvasView annotation = null;

            if (Contains(position))
            {
                if (OverlappedLinkView != null)
                {
                    annotation = OverlappedLinkView.GetAnnotationAtPosition(position);
                    if (annotation != null)
                    {
                        return annotation;
                    }
                }

                return this;
            }

            return null;
        }

        #endregion

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          BasicEffect basicEffect,
                          OverlayShaderEffect overlayEffect,
                          LocationCircleView[] listToDraw)
        {
            int stencilValue = DeviceStateManager.GetDepthStencilValue(device);
            DeviceStateManager.SetDepthStencilValue(device, stencilValue + 1);

            float[] originalAlpha = [.. listToDraw.Select(loc => loc.Alpha)];
            float[] fadeFactor = [.. listToDraw.Select(loc => loc.GetAlphaFadeScalarForScene(scene))];

            listToDraw.ForEach((view, i) =>
                {
                    if (fadeFactor[i] < 1.0f)
                    {
                        view.Alpha = originalAlpha[i] * fadeFactor[i];
                    }
                });

            OverlappedLinkCircleView[] overlappedLocations = [.. listToDraw.Select(l => l.OverlappedLinkView).Where(l => l != null && l.IsVisible(scene))];
            OverlappedLinkCircleView.Draw(device, scene, basicEffect, overlayEffect, overlappedLocations);

            DeviceStateManager.SetDepthStencilValue(device, stencilValue);

            CircleView[] backgroundCircles = [.. listToDraw.Select(l => l.circleView)];
            overlayEffect.InputLumaAlphaValue = 0.5f;
            CircleView.Draw(device, scene, OverlayStyle.Luma, backgroundCircles);

            listToDraw.ForEach((view, i) => view.Alpha = originalAlpha[i]);
        }

        /// <summary>
        /// Draw the text for the location at the specified screen coordinates
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="font"></param>
        /// <param name="ScreenDrawPosition">Center of the annotation in screen space, which is the coordinate system used for text</param>
        /// <param name="MagnificationFactor"></param>
        /// <param name="DirectionToVisiblePlane">The Z distance of the location to the plane viewed by user.</param>
        public override void DrawLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              VikingXNA.Scene scene)
        {
            structureLabels.DrawLabel(spriteBatch, font, scene);

            OverlappedLinkView?.DrawLabel(spriteBatch, font, scene);

            return;
        }

        /// <summary>
        /// Returns an alpha value that fades if the circle fills the screen.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name=""></param>
        /// <returns></returns>
        private float GetAlphaFadeScalarForScene(VikingXNA.Scene scene)
        {
            double ScreenFraction = (Radius * 2.0) / scene.MinVisibleWorldBorderLength;
            double MinScreenFraction = 0.25;
            double MaxScreenFraction = 1.5;
            if (ScreenFraction < MinScreenFraction)
            {
                return 1.0f;
            }
            else if (ScreenFraction > MaxScreenFraction)
            {
                return 0.25f;
            }
            else
            {
                Geometry.Range screenFractionRange = new(MinScreenFraction, MaxScreenFraction);
                double scalar = screenFractionRange.Normalize(ScreenFraction, clip: true);
                //double scalar = (ScreenFraction - MaxScreenFraction) / (MinScreenFraction - MaxScreenFraction);
                scalar = 1f - scalar;
                if (scalar < 0.25f)
                    scalar = 0.25;

                return (float)scalar;
            }
        }

        private float GetAlphaForScale(float scale, float ViewingDistanceAlpha) => GetAlphaForScale(scale, ViewingDistanceAlpha, 1f, 0f, 0.05f, 2f, 0.6f);

        private static float GetAlphaForScale(float scale, float OptimalViewingAlpha, float MaxAlpha, float MinAlpha, float opaqueBelowScaleCutoff, float InvisibleAboveScaleCutoff, float OptimalViewingScale)
        {
            //adjust alpha depending on zoom factor
            float scaledAlpha = OptimalViewingAlpha;
            if (scale < opaqueBelowScaleCutoff)
            {
                scaledAlpha = 1;
            }
            else if (scale > InvisibleAboveScaleCutoff)
            {
                scaledAlpha = MinAlpha;
            }
            else
            {
                if (scale == OptimalViewingScale)
                {
                    scaledAlpha = OptimalViewingAlpha;
                }
                else if (scale < OptimalViewingScale)
                {
                    float AvailableRange = 1 - OptimalViewingScale;
                    scaledAlpha = ((AvailableRange) * ((scale - opaqueBelowScaleCutoff) / (OptimalViewingScale - opaqueBelowScaleCutoff))) + OptimalViewingScale;
                }

                else
                {
                    scaledAlpha = (scaledAlpha - ((scale - OptimalViewingScale) * (scaledAlpha / InvisibleAboveScaleCutoff)));
                }
            }

            return scaledAlpha;
        }

        /*

        private float BaseFontSizeForLocationType(LocationType typecode, int DirectionToVisiblePlane, float MagnificationFactor, Microsoft.Xna.Framework.Graphics.SpriteFont font)
        {
            switch (typecode)
            {
                case LocationType.POINT: // a point
                    if (DirectionToVisiblePlane == 0)
                        return MagnificationFactor * AnnotationOverlay.LocationTextScaleFactor;
                    else
                        return MagnificationFactor * AnnotationOverlay.ReferenceLocationTextScaleFactor;
                case LocationType.CIRCLE: // a circle
                    if (DirectionToVisiblePlane == 0)
                    {
                        return (((float)Radius / (float)font.LineSpacing) * MagnificationFactor) / 2;
                    }
                    else
                    {
                        float maxLines = (float)this.OffSectionRadius / (float)font.LineSpacing;

                        return (maxLines * MagnificationFactor) / 2;
                    }
                default:
                    return MagnificationFactor * AnnotationOverlay.LocationTextScaleFactor;
            }
        }

        

        
        */
        //#endregion

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
            //ClearOverlappingLinkedLocationCache();

            //CreateViewObjects();
            if (IsLocationPropertyAffectingLabels(args.PropertyName))
            {
                CreateLabelObjects();
            }
        }


        /*
        protected override void OnLinkedObjectPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            if(LocationObj.IsGeometryProperty(args.PropertyName))
            {
                this.ClearOverlappingLinkedLocationCache();
            }

            base.OnLinkedObjectPropertyChanged(o, args);
        }

        protected override void OnLinksChanged(object o, NotifyCollectionChangedEventArgs args)
        {
            DeregisterForLinkedLocationChangeEvents();
            ClearOverlappingLinkedLocationCache();
        }
        */
    }
}
