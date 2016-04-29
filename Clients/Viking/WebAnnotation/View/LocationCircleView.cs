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
    abstract class LocationCircleViewBase : LocationCanvasView
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
    }

    class AdjacentLocationCircleView : LocationCircleViewBase, IColorView
    {
        public TextureCircleView upCircleView;
        public TextureCircleView downCircleView;
        public LabelView StructureIDLabelView;

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

        public AdjacentLocationCircleView(LocationObj obj, IVolumeToSectionTransform mapper) : base(obj)
        {
            _MosaicCircle = new GridCircle(obj.Position, obj.Radius * Global.AdjacentLocationRadiusScalar);
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
            StructureIDLabelView = new LabelView(StructureIDLabelWithTypeCode(), modelObj.VolumePosition - new GridVector2(0, this.Radius));
            StructureIDLabelView.MaxLineWidth = this.Radius * 2;
        }

        #region overrides

        public override bool IsVisible(VikingXNA.Scene scene)
        {
            return upCircleView.IsVisible(scene);
        }

        public override bool IsLabelVisible(VikingXNA.Scene scene)
        {
            return StructureIDLabelView.IsVisible(scene);
        }
        
        public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber)
        {
            double distance = this.DistanceToCenter(WorldPosition);
            if (distance > this.Radius)
                return LocationAction.NONE;

            return LocationAction.CREATELINKEDLOCATION;
        }

        public override LocationAction GetMouseShiftClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber)
        {
            return LocationAction.NONE;
        }

        public override LocationAction GetMouseControlClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber)
        {
            return LocationAction.NONE;
        }

        /*
        internal override void OnObjPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            CreateViewObjects();
            CreateLabelObjects();
        }
        */
        /*
        protected override void OnParentPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == "Label" || args.PropertyName == "Attributes")
            {
                CreateLabelObjects();
            }

            base.OnParentPropertyChanged(o, args);
        }*/

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
                              VikingXNA.Scene scene, 
                              int DirectionToVisiblePlane)
        {
            

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
        }

        #endregion

    }


    class LocationCircleView : LocationCircleViewBase, ICanvasViewContainer, ISelectable, IColorView
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
        public LabelView StructureIDLabelView;
        public LabelView StructureLabelView;
        public LabelView ParentStructureLabelView;
        public OverlappedLinkCircleView OverlappedLinkView;

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
            StructureIDLabelView = new LabelView(StructureIDLabelWithTypeCode(), this.VolumeCircle.Center - new GridVector2(0, this.Radius / 3.0f));
            StructureIDLabelView.MaxLineWidth = this.Radius * 2.0;
            StructureIDLabelView._Color = this.modelObj.IsUnverifiedTerminal ? Color.Yellow : Color.Black;

            StructureLabelView = new LabelView(this.FullLabelText(), this.VolumeCircle.Center + new GridVector2(0, this.Radius / 3.0f));
            StructureLabelView.MaxLineWidth = this.Radius * 2;


            if (this.Parent.ParentID.HasValue)
            {
                ParentStructureLabelView = new LabelView(this.Parent.ParentID.ToString(), this.VolumeCircle.Center + new GridVector2(0, this.Radius / 2.0f));
                ParentStructureLabelView._Color = this.Parent.Parent.Type.Color.ToXNAColor(0.75f);
            }
            else
            {
                ParentStructureLabelView = null;
            }
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

                this.OverlappedLinkView = new OverlappedLinkCircleView(this.circleView.Circle, (int)this.Z, value);
                this.OverlappedLinkView.Color = this.Color;

                this.CreateLabelObjects();
            }
        }


        #region overrides

        public override bool IsVisible(VikingXNA.Scene scene)
        {
            return circleView.IsVisible(scene);
        }

        public override bool IsLabelVisible(VikingXNA.Scene scene)
        {
            return StructureIDLabelView.IsVisible(scene);
        }

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber)
        {
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

        public override LocationAction GetMouseShiftClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber)
        {
            return LocationAction.NONE;
        }

        public override LocationAction GetMouseControlClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber)
        {
            return LocationAction.NONE;
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


        public ICanvasView GetAnnotationAtPosition(GridVector2 position, out double distanceToCenterNormalized)
        {
            ICanvasView annotation = null;
            distanceToCenterNormalized = double.MaxValue;

            if (this.Intersects(position))
            {
                if (this.OverlappedLinkView != null)
                {
                    annotation = this.OverlappedLinkView.GetAnnotationAtPosition(position, out distanceToCenterNormalized);
                    if (annotation != null)
                    {
                        return annotation;
                    }
                }

                distanceToCenterNormalized = this.DistanceFromCenterNormalized(position);
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
                              VikingXNA.Scene scene, 
                              int DirectionToVisiblePlane)
        {
            if (font == null)
                throw new ArgumentNullException("font");

            if (spriteBatch == null)
                throw new ArgumentNullException("spriteBatch");
             
            //Scale the label alpha based on the zoom factor 

            double DesiredRowsOfText = 4.0;
            double DefaultFontSize = (this.Radius * 2.0) / DesiredRowsOfText;
            StructureIDLabelView.FontSize = DefaultFontSize; //We only desire one line of text
            StructureLabelView.FontSize = DefaultFontSize / 3.0f;

            //StructureIDLabelView.Position = modelObj.VolumePosition - new GridVector2(0.0, this.Radius / 3.0f);

            StructureIDLabelView.Draw(spriteBatch, font, scene);
            StructureLabelView.Draw(spriteBatch, font, scene);
            if (ParentStructureLabelView != null)
            {
                ParentStructureLabelView.FontSize = StructureIDLabelView.FontSize / 4.0;
                ParentStructureLabelView.Draw(spriteBatch, font, scene);
            }

            if(this.OverlappedLinkView != null)
                this.OverlappedLinkView.DrawLabel(spriteBatch, font, scene);
        
            return;

            /*

            const byte DefaultAlpha = 192;
            //Labels draw at the top left, so we have to offset the drawstring call so the label is centered on the annotation
            Vector2 offset = GetLabelSize(font);
            offset.X /= 2;
            offset.Y /= 2;
            //Offset.x is now the amount to subtract from the label to center it on the annotation


            bool UsingArtificialRadiusForLowMag = false;

            //Scale is used to adjust for the magnification factor of the viewer.  Otherwise text would remain at constant size regardless of mag factor.
            //offsets must be multiplied by scale before use
            float baseScale = BaseFontSizeForLocationType(modelObj.TypeCode, DirectionToVisiblePlane, MagnificationFactor, font);  //The base scale used for Label text, adjusted for additional info text
            bool LowMagScale = ScaleReducedForLowMag(baseScale);

            //Don't draw labels if no human could read them
            if (LabelIsTooSmallToSee(baseScale, font.LineSpacing))
                return;

            StructureType type = this.Parent.Type;

            Microsoft.Xna.Framework.Color color = new Microsoft.Xna.Framework.Color((byte)(0),
                                                                                    (byte)(0),
                                                                                    (byte)(0),
                                                                                    DefaultAlpha);

            float labelScale = baseScale;
            if (this.modelObj.Equals(CreateNewLinkedLocationCommand.LastEditedLocation))
            {
                double alpha = (DateTime.UtcNow.Millisecond / 1000.0) * Math.PI;
                alpha = Math.Sin(alpha);
                color = new Microsoft.Xna.Framework.Color((byte)(type.Color.R / 4),
                                                          (byte)(type.Color.G / 4),
                                                          (byte)(type.Color.B / 4),
                                                          (byte)(alpha * 255));

                labelScale = baseScale + (baseScale * (float)alpha * .25f);
            }
            else if (modelObj.OffEdge)
            {
                color = new Microsoft.Xna.Framework.Color(255, 255, 255, 128);
            }
            else if (modelObj.IsUnverifiedTerminal)
            {
                color = new Microsoft.Xna.Framework.Color((byte)((255 - type.Color.R) / 1),
                                                          (byte)((255 - type.Color.G) / 1),
                                                          (byte)((255 - type.Color.B) / 1),
                                                          (byte)128);
            }


            byte scaledAlpha = color.A;
            if (!UsingArtificialRadiusForLowMag && !LowMagScale)
            {
                scaledAlpha = (byte)(GetAlphaForScale(baseScale, (float)color.A / 255) * 255.0);
                color.A = scaledAlpha;
            }

            //If we have a parent of our parent (Child of a structure, such as a synapse) then include thier ID in small font
            if (this.Parent.Parent != null)
            {
                StructureType ParentType = this.Parent.Parent.Type;
                Vector2 ParentOffset = this.GetParentLabelSize(font);
                ParentOffset.X /= 2f;
                ParentOffset.Y /= 2f;

                //                string ParentLabel = this.Parent.Parent.ToString();
                float ParentScale = baseScale / 1.75f;

                if (LabelIsTooSmallToSee(ParentScale, font.LineSpacing))
                    return;

                Microsoft.Xna.Framework.Color ParentColor = new Microsoft.Xna.Framework.Color(ParentType.Color.R,
                                                                                              ParentType.Color.G,
                                                                                              ParentType.Color.B,
                                                                                              GetAlphaForScale(ParentScale, 0.5f));

                Microsoft.Xna.Framework.Color LabelColor = new Microsoft.Xna.Framework.Color(0f,
                                                                                              0f,
                                                                                              0f,
                                                                                              GetAlphaForScale(ParentScale, 0.5f));

                if (LabelColor.A > 0)
                {
                    Vector2 LabelScreenDrawPosition = LocationCenterScreenPosition;



                    //Position label below the label for the location
                    LabelScreenDrawPosition.Y += ((offset.Y * 3f) * ParentScale);
                    LabelScreenDrawPosition.X -= (offset.X / 2) * ParentScale;


                    spriteBatch.DrawString(font,
                                            this.modelObj.Label,
                                            LabelScreenDrawPosition,
                                            LabelColor,
                                            0,
                                            ParentOffset,
                                            ParentScale,
                                            SpriteEffects.None,
                                            0);
                }

                if (ParentColor.A > 0)
                {
                    Vector2 ParentScreenDrawPosition = LocationCenterScreenPosition;

                    //Position parent label above the label for the location
                    ParentScreenDrawPosition.Y -= ((ParentOffset.Y * 3f) * ParentScale);
                    ParentScreenDrawPosition.X -= 0;// (ParentOffset.X) * ParentScale;

                    spriteBatch.DrawString(font,
                                            Parent.Parent.ToString(),
                                            ParentScreenDrawPosition,
                                            ParentColor,
                                            0,
                                            ParentOffset,
                                            ParentScale,
                                            SpriteEffects.None,
                                            0);
                }
            }
            else
            {
                if (color.A > 0)
                {
                    Vector2 LabelDrawPosition = LocationCenterScreenPosition;
                    LabelDrawPosition.Y += (font.LineSpacing * 0.66f) * baseScale;
                    //       LabelDrawPosition.X -= offset.X * baseScale;

                    spriteBatch.DrawString(font,
                                            this.modelObj.Label,
                                            LabelDrawPosition,
                                            color,
                                            0,
                                            offset,
                                            labelScale,
                                            SpriteEffects.None,
                                            0);
                }

            }


            DrawStructAndTagLabel(spriteBatch, font, LocationCenterScreenPosition, color, baseScale, DirectionToVisiblePlane);

            if (DirectionToVisiblePlane == 0)
            {
                float AlphaForSectionLabels = 0;
                
                //TODO: Fix labels for overlapped location circles
                
                //Indicate the z value for each adjacent location
                foreach (KeyValuePair<OverlappedLocation, GridCircle> adjLoc in this.OverlappingLinkedLocationCircles)
                {
                    string infoLabel = adjLoc.Key.Z.ToString();
                    float scale = (float)((adjLoc.Value.Radius / font.LineSpacing) / 2) * MagnificationFactor;
                    float LineStep = font.LineSpacing * scale;
                    //Info labels are smaller than main labels, so make sure they can be seen
                    if (LineStep < LabelVisibleCutoff)
                    {
                        break;
                    }

                    if (AlphaForSectionLabels == 0)
                    {
                        AlphaForSectionLabels = GetAlphaForScale(scale, DefaultAlpha);
                        if (AlphaForSectionLabels <= 0)
                        {
                            break;
                        }
                    }

                    Microsoft.Xna.Framework.Color InfoLabelColor = new Microsoft.Xna.Framework.Color(1.0f, 0, 0, AlphaForSectionLabels / 255f);

                    Vector2[] labelSizeArray = adjLoc.Key.GetInfoLabelSizeOnCircle(infoLabel, font, scale);

                    float yOffset = (LineStep / 3) * 2;
                    //float yOffset = 0; 

                    for (int iLine = 0; iLine < labelSizeArray.Length; iLine++)
                    {
                        //Get label string and offset for this line
                        string AdditionalLabel = adjLoc.Key._InfoLabelAfterSplit[iLine];
                        Vector2 labelOffset = labelSizeArray[iLine];

                        labelOffset.X /= 2; //Center the label on the annotation

                        //Position label below the label for the location
                        GridVector2 ScreenPosition = AnnotationOverlay.CurrentOverlay.Parent.WorldToScreen(adjLoc.Value.Center.X,
                                                                                                     adjLoc.Value.Center.Y);

                        Vector2 DrawPosition = new Vector2((float)ScreenPosition.X, (float)ScreenPosition.Y);

                        //DrawPosition.Y += labelOffset.Y/4 * scale;
                        DrawPosition.Y += yOffset;
                        DrawPosition.X += (labelOffset.X * scale);

                        spriteBatch.DrawString(font,
                            AdditionalLabel,
                            DrawPosition,
                            InfoLabelColor,
                            0,
                            labelSizeArray[iLine],
                            scale,
                            SpriteEffects.None,
                            0);

                        yOffset += LineStep;
                    }
                }
                
            }
            */
        }

        

        /*
        private void DrawStructAndTagLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                                            Microsoft.Xna.Framework.Graphics.SpriteFont font,
                                            Vector2 LocationCenterScreenPosition,
                                            Microsoft.Xna.Framework.Color color,
                                            float baseScale,
                                            int DirectionToVisiblePlane)
        {
            //Draw the info label. If we are drawing an off-section location, then indicate section number.  Otherwise use the parent label
            if (color.A <= 0)
                return;

            string fullLabelText = this.FullLabelText(DirectionToVisiblePlane);

            if (fullLabelText.Length > 0)
            {
                float InfoToLabelRatio = 1 / 2.5f;
                float InfoLabelScale = baseScale * InfoToLabelRatio;

                Vector2[] LabelOffsetArray = GetInfoLabelSizeOnCircle(fullLabelText, font, InfoLabelScale);

                if (!LabelIsTooSmallToSee(InfoLabelScale, font.LineSpacing))
                {
                    float LineStep = font.LineSpacing * InfoLabelScale;  //How much do we increment Y to move down a line?
                    float yOffset = -(font.LineSpacing * 0.66f) * InfoLabelScale;  //What is the offset to draw the line at the correct position?  We have to draw below label if it exists
                    //However we only need to drop half a line since the label straddles the center

                    Microsoft.Xna.Framework.Color InfoLabelColor = color;

                    if (DirectionToVisiblePlane != 0)
                    {
                        InfoLabelColor = new Microsoft.Xna.Framework.Color(255, 0, 0, color.A);
                    }
                    // InfoLabelColor.A = (byte)(GetAlphaForScale(InfoScale, 0.5f) * 255);

                    for (int iLine = 0; iLine < LabelOffsetArray.Length; iLine++)
                    {
                        //Get label string and offset for this line
                        string AdditionalLabel = _InfoLabelAfterSplit[iLine];
                        Vector2 labelOffset = LabelOffsetArray[iLine];

                        labelOffset.X /= 2; //Center the label on the annotation

                        //Position label below the label for the location
                        Vector2 DrawPosition = LocationCenterScreenPosition;
                        //DrawPosition.Y += labelOffset.Y/4 * scale;
                        DrawPosition.Y += yOffset;
                        DrawPosition.X += (labelOffset.X * InfoLabelScale);

                        spriteBatch.DrawString(font,
                            AdditionalLabel,
                            DrawPosition,
                            InfoLabelColor,
                            0,
                            LabelOffsetArray[iLine],
                            InfoLabelScale,
                            SpriteEffects.None,
                            0);

                        yOffset += LineStep;
                    }
                }
            }

        }
        */
        
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

        protected bool IsLocationPropertyAffectingLabels(string PropertyName)
        {
            return string.IsNullOrEmpty(PropertyName) ||
                PropertyName == "Terminal" ||
                PropertyName == "OffEdge" ||
                PropertyName == "Attributes";
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
