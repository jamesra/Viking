using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.SqlServer.Types;
using WebAnnotationModel;
using SqlGeometryUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VikingXNA;
using VikingXNAGraphics;

namespace WebAnnotation.View
{
    class AdjacentLocationLineView : LocationLineViewBase
    {
        protected PolyLineView upPolyLineView;
        protected PolyLineView downPolyLineView;

        public Color Color
        {
            get { return upPolyLineView.Color; }
            set { upPolyLineView.Color = value;
                downPolyLineView.Color = value;
            }
        }

        public override double Width
        {
            get { return upPolyLineView.LineWidth; }
        }

        public AdjacentLocationLineView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionMapper mapper) : base(obj, mapper)
        {
            upPolyLineView = new PolyLineView(VolumeControlPoints, obj.Parent.Type.Color.ToXNAColor().ConvertToHSL(0.5f), GlobalPrimitives.UpArrowTexture);
            downPolyLineView = new PolyLineView(VolumeControlPoints, obj.Parent.Type.Color.ToXNAColor().ConvertToHSL(0.5f), GlobalPrimitives.DownArrowTexture);
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect,
                          AdjacentLocationLineView[] listToDraw,
                          int VisibleSectionNumber)
        {
            PolyLineView[] linesToDraw = listToDraw.Select(l => l.modelObj.Z < VisibleSectionNumber ? l.downPolyLineView : l.upPolyLineView).ToArray();
            PolyLineView.Draw(device, scene, lineManager, basicEffect, overlayEffect, linesToDraw);
        }

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber)
        {
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
    }

    class LocationLineView : LocationLineViewBase
    {
        protected PolyLineView polyLineView;

        

        public Color Color
        {
            get { return polyLineView.Color; }
            set { polyLineView.Color = value; }
        }

        public override double Width
        {
            get { return polyLineView.LineWidth; }
        }

        public LocationLineView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionMapper mapper, Texture2D texture = null) : base(obj, mapper)
        {
            polyLineView = new PolyLineView(obj.VolumeShape.ToPoints(), obj.Parent.Type.Color.ToXNAColor(0.5f), texture);
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect,
                          LocationLineView[] listToDraw)
        {
            PolyLineView.Draw(device, scene, lineManager, basicEffect, overlayEffect, listToDraw.Select(l => l.polyLineView).ToArray());
        }

        public override LocationAction GetMouseShiftClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber)
        {
            //Allow user to add a control point if the mouse is not over an existing control point
            if(!polyLineView.ControlPoints.Select(p => new GridCircle(p, Width / 2.0)).Any(c => c.Contains(WorldPosition)))
                return LocationAction.ADDCONTROLPOINT;

            return LocationAction.NONE;
        }
    }
    
    abstract class LocationLineViewBase : MultipleControlPointLocationCanvasViewBase
    {
        public LocationLineViewBase(LocationObj obj, Viking.VolumeModel.IVolumeToSectionMapper mapper) : base(obj, mapper)
        { }

        public override bool IsVisible(VikingXNA.Scene scene)
        {
            if (this.Width / scene.DevicePixelWidth < 2.0)
                return false;

            return scene.VisibleWorldBounds.Intersects(this.BoundingBox);
        }

        public override bool IsLabelVisible(Scene scene)
        {
            return IsVisible(scene);
        }

        public override GridRectangle BoundingBox
        {
            get
            {
                return this.VolumeShapeAsRendered.Envelope();
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



        public override double DistanceFromCenterNormalized(GridVector2 Position)
        {
            //TODO: Find a more accurate measurement.  Returning 0 means the line is always on top in selection.
            GridLineSegment[] segs = GridLineSegment.SegmentsFromPoints(this.VolumeControlPoints);
            double MinDistance = segs.Min(l => l.DistanceToPoint(Position));
            return (MinDistance - (this.Width / 2.0));
        }

        protected override void OnObjPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            /*
            if (args.PropertyName == "MosaicShape")
            {
                MosaicControlPoints = obj.MosaicShape.ToPoints();
                VolumeControlPoints = mapper.SectionToVolume(MosaicControlPoints);
            }

            if (args.PropertyName == "VolumeShape")
            {
                _RenderedVolumeShape = null;
                _VolumeControlPoints = null;
            } 
            */
        }

        public override void DrawLabel(SpriteBatch spriteBatch, SpriteFont font, Scene scene, int DirectionToVisiblePlane)
        {
            return;
        }

        public bool PointIntersectsAnyControlPoint(GridVector2 WorldPosition)
        {
            return VolumeControlPoints.Select(p => new GridCircle(p, Width / 2.0)).Any(c => c.Contains(WorldPosition));
        }

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber)
        {        
            if (VisibleSectionNumber == (int)this.modelObj.Z)
            {
                //Find distance to nearest control point
                if (PointIntersectsAnyControlPoint(WorldPosition))
                    return LocationAction.ADJUST;
                else
                    return LocationAction.CREATELINK;
            }
            else
            {
                return LocationAction.CREATELINKEDLOCATION;
            }
        }

        public override LocationAction GetMouseShiftClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber)
        {
            //Allow user to add a control point if the mouse is not over an existing control point
            if (PointIntersectsAnyControlPoint(WorldPosition))
                return LocationAction.TRANSLATE;
            
            return LocationAction.NONE;
        }

        public override LocationAction GetMouseControlClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber)
        {
            //Allow user to add a control point if the mouse is not over an existing control point
            if (PointIntersectsAnyControlPoint(WorldPosition))
            {
                if (VolumeControlPoints.Length > 2)
                    return LocationAction.REMOVECONTROLPOINT;
                else
                    return LocationAction.NONE;
            }
            else
                return LocationAction.ADDCONTROLPOINT;

            //return LocationAction.NONE;
        }

        public abstract double Width { get; }

        private SqlGeometry _VolumeShape;
        public override SqlGeometry VolumeShapeAsRendered
        {
            get
            {
                if (_VolumeShape == null)
                {
                    _VolumeShape = this.VolumeControlPoints.ToPolyLine().STBuffer(Width);                 
                }

                return _VolumeShape;
            }
        }
    }

    public abstract class MultipleControlPointLocationCanvasViewBase : LocationCanvasView
    {
        /// <summary>
        /// Mosaic points composing the polyline, without added points to create a curve
        /// </summary>
        protected readonly GridVector2[] MosaicControlPoints;

        /// <summary>
        /// Mosaic points composing the polyline, without added points to create a curve
        /// </summary>
        protected readonly GridVector2[] VolumeControlPoints;

        public MultipleControlPointLocationCanvasViewBase(LocationObj obj, Viking.VolumeModel.IVolumeToSectionMapper mapper) : base(obj)
        {
            MosaicControlPoints = obj.MosaicShape.ToPoints();
            VolumeControlPoints = mapper.SectionToVolume(MosaicControlPoints);
        }
    }
}
