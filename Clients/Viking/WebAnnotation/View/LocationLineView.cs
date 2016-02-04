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

        public AdjacentLocationLineView(LocationObj obj) : base(obj)
        {
            upPolyLineView = new PolyLineView(obj.VolumeShape.ToPoints(), obj.Parent.Type.Color.ToXNAColor().ConvertToHSL(0.5f), GlobalPrimitives.UpArrowTexture);
            downPolyLineView = new PolyLineView(obj.VolumeShape.ToPoints(), obj.Parent.Type.Color.ToXNAColor().ConvertToHSL(0.5f), GlobalPrimitives.DownArrowTexture);
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

        public LocationLineView(LocationObj obj, Texture2D texture = null) : base(obj)
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
    
    abstract class LocationLineViewBase : LocationCanvasView
    { 
        public override bool IsVisible(VikingXNA.Scene scene)
        {
            return scene.VisibleWorldBounds.Intersects(this.BoundingBox);
        }
                
        public override GridRectangle BoundingBox
        {
            get
            {
                return this.RenderedVolumeShape.Envelope();
            }
        }

        public override bool Intersects(GridVector2 Position)
        {
            return this.RenderedVolumeShape.Intersects(Position);
        }

        public override bool Intersects(SqlGeometry shape)
        {
            return this.RenderedVolumeShape.STIntersects(shape).IsTrue;
        }
         
        public override double Distance(GridVector2 Position)
        {
            return this.RenderedVolumeShape.Distance(Position);
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
            if (args.PropertyName == "MosaicShape")
            {
                _MosaicControlPoints = null;
            }

            if (args.PropertyName == "VolumeShape")
            {
                _RenderedVolumeShape = null;
                _VolumeControlPoints = null;
            } 
        }

        public override void DrawLabel(SpriteBatch spriteBatch, SpriteFont font, Scene scene, float MagnificationFactor, int DirectionToVisiblePlane)
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


        public override IList<LocationCanvasView> OverlappingLinks
        {
            get
            {
                return new List<LocationCanvasView>();
            }
        }

        public abstract double Width { get; }

        public LocationLineViewBase(LocationObj obj) : base(obj)
        {
        }

        private SqlGeometry _RenderedVolumeShape;
        public virtual SqlGeometry RenderedVolumeShape
        {
            get
            {
                if (_RenderedVolumeShape == null)
                {
                    _RenderedVolumeShape = this.VolumeShape.STBuffer(this.Width / 2.0);
                    //_RenderedVolumeShape = this.modelObj.VolumeShape.STBuffer(this.Width);
                }

                return _RenderedVolumeShape;
            }
        }
        
        public SqlGeometry VolumeShape
        {
            get { return this.modelObj.VolumeShape; }
        }

        private GridVector2[] _MosaicControlPoints;
        public virtual GridVector2[] MosaicControlPoints
        {
            get
            {
                if (_MosaicControlPoints == null)
                {
                    _MosaicControlPoints = modelObj.MosaicShape.ToPoints();
                }

                return _MosaicControlPoints;
            }
        }

        private GridVector2[] _VolumeControlPoints;
        public virtual GridVector2[] VolumeControlPoints
        {
            get
            {
                if (_VolumeControlPoints == null)
                {
                    _VolumeControlPoints = modelObj.VolumeShape.ToPoints();
                }

                return _VolumeControlPoints;
            }
        }
         
        
    }
}
