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

        public override double LineWidth
        {
            get { return upPolyLineView.LineWidth; }
        }

        public override double ControlPointRadius
        {
            get
            {
                return LineWidth / 2.0;
            }
        }

        public AdjacentLocationLineView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj, mapper)
        {
            upPolyLineView = new PolyLineView(VolumeControlPoints, obj.Parent.Type.Color.ToXNAColor().ConvertToHSL(0.5f), GlobalPrimitives.UpArrowTexture, lineStyle: LineStyle.Tubular);
            downPolyLineView = new PolyLineView(VolumeControlPoints, obj.Parent.Type.Color.ToXNAColor().ConvertToHSL(0.5f), GlobalPrimitives.DownArrowTexture, lineStyle: LineStyle.Tubular);
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

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {
            LocationID = this.ID;
            if (ModifierKeys.ShiftOrCtrlPressed())
                return LocationAction.NONE;

            return LocationAction.CREATELINKEDLOCATION;
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

        public override double LineWidth
        {
            get { return polyLineView.LineWidth; }
        }

        public override double ControlPointRadius
        {
            get
            {
                return LineWidth / 2.0;
            }
        }

        public LocationLineView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper, Texture2D texture = null) : base(obj, mapper)
        {
            GridVector2[] volumePoints;
            bool[] success = mapper.TrySectionToVolume(obj.MosaicShape.ToPoints(), out volumePoints);
            if (success.All(s => s == true))
            {
                polyLineView = new PolyLineView(volumePoints, obj.Parent.Type.Color.ToXNAColor(0.5f), texture);
            }
            else
            {
                throw new ArgumentException(string.Format("Could not map location {0} to volume", obj.ID));
            }
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

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {
            LocationID = this.ID;
            if (ModifierKeys.ShiftPressed())
                return LocationAction.NONE;
            else if(ModifierKeys.CtrlPressed())
            {
                //Allow user to add a control point if the mouse is not over an existing control point
                if (!polyLineView.ControlPoints.Select(p => new GridCircle(p, LineWidth / 2.0)).Any(c => c.Contains(WorldPosition)))
                    return LocationAction.ADDCONTROLPOINT;

                return LocationAction.NONE;
            }

            return LocationAction.CREATELINKEDLOCATION;
        }
    }
    
    abstract class LocationLineViewBase : MultipleControlPointLocationCanvasViewBase
    {
        public LocationLineViewBase(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj, mapper)
        { }

        public override bool IsVisible(VikingXNA.Scene scene)
        {
            if (Math.Min(this.BoundingBox.Width, this.BoundingBox.Height) / scene.DevicePixelWidth < 2.0)
                return false;

            return scene.VisibleWorldBounds.Intersects(this.BoundingBox);
        }

        public virtual bool IsLabelVisible(Scene scene)
        {
            return IsVisible(scene);
        }

        private GridRectangle? _bbox;
        public override GridRectangle BoundingBox
        {
            get
            {
                if(!_bbox.HasValue)
                {
                    _bbox = this.VolumeShapeAsRendered.Envelope();
                }

                return _bbox.Value;
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
                _OverlappedLinks = value;
            }
        }



        public override double DistanceFromCenterNormalized(GridVector2 Position)
        {
            if (PointIntersectsAnyControlPoint(Position))
            {
                return VolumeControlPoints.Select(p => GridVector2.Distance(p, Position) / ControlPointRadius).Min();
            }
            else
            {
                //TODO: Find a more accurate measurement.  Returning 0 means the line is always on top in selection.
                GridLineSegment[] segs = GridLineSegment.SegmentsFromPoints(this.VolumeControlPoints);
                double MinDistance = segs.Min(l => l.DistanceToPoint(Position));
                return (this.LineWidth / 2.0) - MinDistance;
            }
        }
                
        public bool PointIntersectsAnyControlPoint(GridVector2 WorldPosition)
        {
            return VolumeControlPoints.Select(p => new GridCircle(p, ControlPointRadius)).Any(c => c.Contains(WorldPosition));
        }

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {
            LocationID = this.ID;

            if (ModifierKeys.ShiftPressed())
            {
                //Allow user to add a control point if the mouse is not over an existing control point
                if (PointIntersectsAnyControlPoint(WorldPosition))
                    return LocationAction.TRANSLATE;

                return LocationAction.NONE;
            }
            else if (ModifierKeys.CtrlPressed())
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
            else
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
        }

        public override string[] HelpStrings
        {
            get
            {
                return new string[] {
                    "Hold left click + SHIFT on control point: Move all control points",
                    "Hold left click off control point: Create/Link annotation",
                    "Left click + CTRL on control point: Remove control point",
                    "Left click + CTRL off control point: Add a control point",
                };
            }
        }

        public abstract double LineWidth { get; }

        public abstract double ControlPointRadius { get; }

        private SqlGeometry _VolumeShape;
        public override SqlGeometry VolumeShapeAsRendered
        {
            get
            {
                if (_VolumeShape == null)
                {
                    _VolumeShape = this.VolumeControlPoints.ToPolyLine().STBuffer(Math.Max(LineWidth, ControlPointRadius));                 
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
        internal readonly GridVector2[] MosaicControlPoints;

        /// <summary>
        /// Mosaic points composing the polyline, without added points to create a curve
        /// </summary>
        internal readonly GridVector2[] VolumeControlPoints;

        public MultipleControlPointLocationCanvasViewBase(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj)
        {
            MosaicControlPoints = obj.MosaicShape.ToPoints();
            VolumeControlPoints = mapper.SectionToVolume(MosaicControlPoints);
        }
    }
}
