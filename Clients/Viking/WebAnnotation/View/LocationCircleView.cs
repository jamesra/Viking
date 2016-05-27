using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Geometry;
using WebAnnotationModel;
using WebAnnotation;
using VikingXNAGraphics;
using WebAnnotation.ViewModel;
using WebAnnotation.UI.Commands;
using Microsoft.SqlServer.Types;
using VikingXNA;
using Viking.VolumeModel;
using SqlGeometryUtils;

namespace WebAnnotation.View
{
    abstract class LocationCircleViewBase : LocationCanvasView, ILabelView
    {
        public LocationCircleViewBase(LocationObj obj) : base(obj)
        {
        }

        public virtual double Radius
        {
            get { return MosaicCircle.Radius; }
        }
        
        private SqlGeometry _VolumeShape = null;
        public override SqlGeometry VolumeShapeAsRendered
        {
            get
            {
                if(_VolumeShape == null)
                {
                    _VolumeShape = VolumeCircle.ToSqlGeometry(this.Z);
                }
                return _VolumeShape;
            }
        }

        public abstract GridCircle MosaicCircle { get; }
        
        public abstract GridCircle VolumeCircle { get;}

        public override GridRectangle BoundingBox
        {
            get
            {
                return VolumeCircle.BoundingBox;
            }
        }

        /// <summary>
        /// True if the point is on or inside the circle
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override bool Intersects(GridVector2 Position)
        {
            return VolumeCircle.Contains(Position);
        }

        public override bool Intersects(SqlGeometry shape)
        {
            return this.modelObj.VolumeShape.STIntersects(shape).IsTrue;
        }
        
        /// <summary>
        /// Distance to the nearest point on circle if outside, otherwise zero
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override double Distance(GridVector2 Position)
        {
            double Distance = GridVector2.Distance(Position, this.VolumeCircle.Center) - Radius;
            Distance = Distance < 0 ? 0 : Distance;
            return Distance;
        }

        public override double DistanceFromCenterNormalized(GridVector2 Position)
        {
            return GridVector2.Distance(Position, this.VolumeCircle.Center) / this.Radius;
        }

        public double DistanceToCenter(GridVector2 Position)
        {
            return GridVector2.Distance(Position, this.VolumeCircle.Center);
        }


        public abstract void DrawLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              Scene scene);

        public abstract bool IsLabelVisible(Scene scene);
    }

    class AdjacentLocationCircleView : LocationCircleViewBase, IColorView
    {
        public TextureCircleView upCircleView;
        public TextureCircleView downCircleView;
        public StructureCircleLabels structureLabels;

        protected readonly GridCircle _VolumeCircle;
        protected readonly GridCircle _MosaicCircle;

        public override GridCircle MosaicCircle
        {
            get
            {
                return _MosaicCircle;
            }
        }

        public override GridCircle VolumeCircle
        {
            get
            {
                return _VolumeCircle;
            }
        }

        private ICollection<long> _OverlappedLinks;
        public override ICollection<long> OverlappedLinks
        {
            protected get
            {
                return _OverlappedLinks;
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public AdjacentLocationCircleView(LocationObj obj, IVolumeToSectionTransform mapper, double Radius) : base(obj)
        {
            _MosaicCircle = new GridCircle(obj.Position, Radius);
            _VolumeCircle = new GridCircle(mapper.SectionToVolume(_MosaicCircle.Center), _MosaicCircle.Radius);

            CreateViewObjects(this.MosaicCircle, mapper);
            CreateLabelObjects();
        }

        public AdjacentLocationCircleView(LocationObj obj, IVolumeToSectionTransform mapper) : base(obj)
        {
            _MosaicCircle = new GridCircle(obj.Position, obj.Radius * Global.AdjacentLocationRadiusScalar);
            _VolumeCircle = new GridCircle(mapper.SectionToVolume(_MosaicCircle.Center), _MosaicCircle.Radius);

            CreateViewObjects(this.MosaicCircle, mapper);
            CreateLabelObjects();  
        }

        public AdjacentLocationCircleView(LocationObj obj, GridCircle mosaicCircle, IVolumeToSectionTransform mapper) : base(obj)
        {
            _MosaicCircle = mosaicCircle;
            _VolumeCircle = new GridCircle(mapper.SectionToVolume(_MosaicCircle.Center), _MosaicCircle.Radius);

            CreateViewObjects(this.MosaicCircle, mapper);
            CreateLabelObjects();
        }

        /// <summary>
        /// We scale down the radius when the location is on an adjacent section
        /// </summary>
        public override double Radius
        {
            get { return this.VolumeCircle.Radius; }
        }

        public Color Color
        {
            get
            {
                return this.upCircleView.Color;
            }

            set
            {
                this.upCircleView.Color = value;
                this.downCircleView.Color = value;
            }
        }

        public float Alpha
        {
            get
            {
                return this.upCircleView.Alpha;
            }

            set
            {
                this.upCircleView.Alpha = value;
                this.downCircleView.Alpha = value;
            }
        }

        private void CreateViewObjects(GridCircle MosaicCircle, IVolumeToSectionTransform mapper)
        { 
            upCircleView = TextureCircleView.CreateUpArrow(_VolumeCircle, modelObj.Parent.Type.Color.ToXNAColor(0.5f));
            downCircleView = TextureCircleView.CreateDownArrow(_VolumeCircle, modelObj.Parent.Type.Color.ToXNAColor(0.5f));
        }

        private void CreateLabelObjects()
        {
            this.structureLabels = new StructureCircleLabels(this.modelObj, this.VolumeCircle, false); 
        }

        #region overrides

        public override bool IsVisible(VikingXNA.Scene scene)
        {
            return !this.modelObj.IsVerifiedTerminal && upCircleView.IsVisible(scene);
        }

        public override bool IsLabelVisible(VikingXNA.Scene scene)
        {
            return structureLabels.IsLabelVisible(scene);
        }
        
        public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {
            LocationID = this.ID;

            if (ModifierKeys.ShiftOrCtrlPressed())
            {    
                return LocationAction.NONE;
            }

            double distance = this.DistanceToCenter(WorldPosition);
            if (distance > this.Radius)
                return LocationAction.NONE;

            return LocationAction.CREATELINKEDLOCATION;
        }

        public override string[] HelpStrings
        {
            get
            {
                return new string[] {
                    "Hold left click + drag on inscribed arrow: Create additional annotation for this structure linked to the annotation on the adjacent section."
                };
            }
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          BasicEffect basicEffect,
                          VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect,
                          AdjacentLocationCircleView[] listToDraw,
                          int VisibleSectionNumber)
        {
            TextureCircleView[] backgroundCircles = listToDraw.Select(l => l.modelObj.Z < VisibleSectionNumber ? l.downCircleView : l.upCircleView).ToArray();
            TextureCircleView.Draw(device, scene, basicEffect, overlayEffect, backgroundCircles);
        }

        public override void DrawLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              VikingXNA.Scene scene)
        {

            structureLabels.DrawLabel(spriteBatch, font, scene);
            /*
            if (font == null)
                throw new ArgumentNullException("font");

            if (spriteBatch == null)
                throw new ArgumentNullException("spriteBatch");

            float MagnificationFactor = (float)(1.0 / scene.Camera.Downsample);
            double DesiredRowsOfText = 6.0;
            double DefaultFontSize = (this.Radius * 2) / DesiredRowsOfText;
            StructureIDLabelView.FontSize = DefaultFontSize;
            StructureIDLabelView.Position = modelObj.VolumePosition - new GridVector2(0.0, this.Radius / 3.0f);
            StructureIDLabelView.Draw(spriteBatch, font, scene);

            return; 
            */
        }

        #endregion

    }


    class LocationCircleView : LocationCircleViewBase, ICanvasViewContainer, ISelectable, IColorView, ILabelView
    {
        protected readonly GridCircle _VolumeCircle;
        protected readonly GridCircle _MosaicCircle;

        public override GridCircle MosaicCircle
        {
            get
            {
                return _MosaicCircle;
            }
        }

        public override GridCircle VolumeCircle
        {
            get
            {
                return _VolumeCircle;
            }
        }

        public Color Color
        {
            get
            {
                return circleView.Color;
            }

            set
            {
                circleView.Color = value;
                if (OverlappedLinkView != null)
                    OverlappedLinkView.Color = value;
            }
        }

        public float Alpha
        {
            get
            {
                return circleView.Alpha;
            }

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

        static float RadiusToResizeCircle = 7.0f / 8.0f;
        static float RadiusToLinkCircle = 1.0f / 4.0f;
        static double BeginFadeCutoff = 0.1;
        static double InvisibleCutoff = 1f;

        public LocationCircleView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj)
        {
            _MosaicCircle = new GridCircle(obj.Position, obj.Radius);
            _VolumeCircle = new GridCircle(mapper.SectionToVolume(_MosaicCircle.Center), _MosaicCircle.Radius);

            //RegisterForLocationEvents();
            //RegisterForStructureChangeEvents();
            CreateViewObjects(_MosaicCircle, mapper);
            CreateLabelObjects();
        }

        private void CreateViewObjects(GridCircle MosaicCircle, IVolumeToSectionTransform mapper)
        {
            GridVector2 VolumePosition = mapper.SectionToVolume(MosaicCircle.Center);
            circleView = new CircleView(new GridCircle(VolumePosition, modelObj.Radius), modelObj.Parent.Type.Color.ToXNAColor(1f));
        }

        private void CreateLabelObjects()
        {
            this.structureLabels = new StructureCircleLabels(this.modelObj, this.VolumeCircle);
        }


        public override ICollection<long> OverlappedLinks
        {
            protected get
            {
                if (this.OverlappedLinkView == null)
                    return new long[0];

                return this.OverlappedLinkView.OverlappedLinks;
            }

            set
            {
                if (value == null || value.Count == 0)
                {
                    this.OverlappedLinkView = null;
                }

                this.OverlappedLinkView = new OverlappedLinkCircleView(this.circleView.Circle, this.ID, (int)this.Z, value);
                this.OverlappedLinkView.Color = this.Color;

                this.CreateLabelObjects();
            }
        }


        #region overrides

        public override bool IsVisible(VikingXNA.Scene scene)
        {
            return circleView.IsVisible(scene) && GetAlphaFadeScalarForScene(scene) > 0;
        }

        public override bool IsLabelVisible(VikingXNA.Scene scene)
        {
            return structureLabels.IsLabelVisible(scene);
        }

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {
            LocationID = this.ID;

            if (ModifierKeys.ShiftOrCtrlPressed())
                return LocationAction.NONE;

            double distance = this.DistanceToCenter(WorldPosition);

            if (OverlappedLinkView != null)
            {
                if (OverlappedLinkView.Intersects(WorldPosition))
                {
                    return LocationAction.CREATELINKEDLOCATION;
                }
            }

            if (VisibleSectionNumber == (int)this.modelObj.Z)
            {
                if (distance > this.Radius)
                    return LocationAction.NONE;
                else if (distance >= (this.Radius * RadiusToResizeCircle))
                    return LocationAction.SCALE;
                else if (distance >= (this.Radius * RadiusToLinkCircle))
                    return LocationAction.CREATELINK;
                else
                    return LocationAction.TRANSLATE;
            }

            throw new ArgumentException("Wrong section for location");
        }
         
        public override string[] HelpStrings
        {
            get
            {
                return new string[] {
                    "Hold left click on circle edge: Resize",
                    "Hold left click + drag on inscribed arrow: Create additional annotation for this structure linked to the annotation on the adjacent section.",
                    "Hold left click on circle center: Move annotation"
                };
            }
        }


        #endregion

        public override double Radius
        {
            get
            {
                return VolumeCircle.Radius;
            }
        }

        private bool _Selected = false;
        public bool Selected
        {
            get
            {
                return _Selected;
            }

            set
            {
                if (value)
                {
                    this.circleView.Alpha = 0.25f;
                }
                else
                {
                    this.circleView.Alpha = 0.5f;
                }

                _Selected = value;
            }
        }

        #region Linked Locations


        public ICanvasView GetAnnotationAtPosition(GridVector2 position)
        {
            ICanvasView annotation = null;

            if (this.Intersects(position))
            {
                if (this.OverlappedLinkView != null)
                {
                    annotation = this.OverlappedLinkView.GetAnnotationAtPosition(position);
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
                          VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect,
                          LocationCircleView[] listToDraw)
        {
            int stencilValue = DeviceStateManager.GetDepthStencilValue(device);
            DeviceStateManager.SetDepthStencilValue(device, stencilValue + 1);

            float[] originalAlpha = listToDraw.Select(loc => loc.Alpha).ToArray();
            float[] fadeFactor = listToDraw.Select(loc => loc.GetAlphaFadeScalarForScene(scene)).ToArray();
             
            listToDraw.ForEach((view, i) =>
                {
                    if (fadeFactor[i] < 1.0f)
                    {
                        view.Alpha = originalAlpha[i] * fadeFactor[i];
                    }
                });

            OverlappedLinkCircleView[] overlappedLocations = listToDraw.Select(l => l.OverlappedLinkView).Where(l => l != null && l.IsVisible(scene)).ToArray();
            OverlappedLinkCircleView.Draw(device, scene, basicEffect, overlayEffect, overlappedLocations);
             
            DeviceStateManager.SetDepthStencilValue(device, stencilValue);
            
            CircleView[] backgroundCircles = listToDraw.Select(l => l.circleView).ToArray();
            
            CircleView.Draw(device, scene, basicEffect, overlayEffect, backgroundCircles);

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

            if (this.OverlappedLinkView != null)
                this.OverlappedLinkView.DrawLabel(spriteBatch, font, scene);
        
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
            double ScreenFraction = (this.Radius * 2.0) / scene.MinVisibleWorldBorderLength;
            double MinScreenFraction = 0.25;
            double MaxScreenFraction = 1.5;
            if (ScreenFraction < MinScreenFraction)
            {
                return 1.0f;
            }
            else if (ScreenFraction > MaxScreenFraction)
            {
                return 0f;
            }
            else
            {
                double scalar = (ScreenFraction - MaxScreenFraction) / (MinScreenFraction - MaxScreenFraction);
                return (float)scalar.Interpolate(0, 1);
            }
        }
            
        private float GetAlphaForScale(float scale, float ViewingDistanceAlpha)
        {
            return GetAlphaForScale(scale, ViewingDistanceAlpha, 1f, 0f, 0.05f, 2f, 0.6f);
        }

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
                    scaledAlpha = OptimalViewingAlpha;
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
            if(IsLocationPropertyAffectingLabels(args.PropertyName))
                CreateLabelObjects();
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
