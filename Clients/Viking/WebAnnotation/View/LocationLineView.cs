using Geometry;
using Viking.Input;
using Rectangle = Geometry.Rectangle;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
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
    internal class AdjacentLocationLineView : LocationLineViewBase
    {
        protected PolyLineView upPolyLineView;
        protected PolyLineView downPolyLineView;

        public Color Color
        {
            get => upPolyLineView.Color;
            set
            {
                upPolyLineView.Color = value;
                downPolyLineView.Color = value;
            }
        }

        public override double LineWidth => upPolyLineView.LineWidth;

        public override double ControlPointRadius => LineWidth / 2.0;

        public AdjacentLocationLineView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj, mapper)
        {
            upPolyLineView = new PolyLineView(VolumeControlPoints, obj.Parent.Type.Color.ToXNAColor().ConvertToHCL(0.5f), GlobalPrimitives.UpArrowTexture, obj.Width.Value, lineStyle: LineStyle.Tubular);
            downPolyLineView = new PolyLineView(VolumeControlPoints, obj.Parent.Type.Color.ToXNAColor().ConvertToHCL(0.5f), GlobalPrimitives.DownArrowTexture, obj.Width.Value, lineStyle: LineStyle.Tubular);
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          OverlayShaderEffect overlayEffect,
                          AdjacentLocationLineView[] listToDraw,
                          int VisibleSectionNumber)
        {
            PolyLineView[] linesToDraw = [.. listToDraw.Select(l => l.modelObj.Z < VisibleSectionNumber ? l.downPolyLineView : l.upPolyLineView)];
            PolyLineView.Draw(device, scene, OverlayStyle.Luma, linesToDraw);
        }

        public override LocationAction GetPenContactActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID) => throw new NotImplementedException();

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            LocationID = ID;
            if (modifierKeys.ShiftOrCtrlPressed())
            {
                return LocationAction.NONE;
            }

            return LocationAction.CREATELINKEDLOCATION;
        }

        public override List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber) => throw new NotImplementedException();/*
            LocationID = this.ID;
            return LocationAction.NONE;
            */
    }

    internal class LocationLineView : LocationLineViewBase
    {
        protected PolyLineView polyLineView;


        public Color Color
        {
            get => polyLineView.Color;
            set => polyLineView.Color = value;
        }

        public override double LineWidth => polyLineView.LineWidth;

        public override double ControlPointRadius => LineWidth / 2.0;

        public LocationLineView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper, Texture2D? texture = null) : base(obj, mapper)
        {
            bool[] success = mapper.TrySectionToVolume(obj.MosaicShape.ToPoints(), out Geometry.Vector2[] volumePoints);
            polyLineView = success.All(s => s == true)
                ? new PolyLineView(volumePoints, obj.Parent.Type.Color.ToXNAColor(0.5f), texture)
                : throw new ArgumentException($"Could not map location {obj.ID} to volume");
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          OverlayShaderEffect overlayEffect,
                          LocationLineView[] listToDraw) => PolyLineView.Draw(device, scene, OverlayStyle.Luma, [.. listToDraw.Select(l => l.polyLineView)]);

        public override LocationAction GetPenContactActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            LocationID = ID;
            if (modifierKeys.ShiftPressed())
            {
                return LocationAction.NONE;
            }
            else
            {
                return LocationAction.CREATELINKEDLOCATION;
            }
        }

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            LocationID = ID;
            if (modifierKeys.ShiftPressed())
            {
                return LocationAction.NONE;
            }
            else if (modifierKeys.CtrlPressed())
            {
                //Allow user to add a control point if the mouse is not over an existing control point
                if (!polyLineView.ControlPoints.Select(p => new Circle(p, LineWidth / 2.0)).Any(c => c.Covers(WorldPosition)))
                {
                    return LocationAction.ADDCONTROLPOINT;
                }

                return LocationAction.NONE;
            }

            return LocationAction.CREATELINKEDLOCATION;
        }

    }

    internal abstract class LocationLineViewBase(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : MultipleControlPointLocationCanvasViewBase(obj, mapper)
    {
        public override bool IsVisible(VikingXNA.Scene scene) => LocationCanvasView.IsPolygonVisible(BoundingBox, scene) && this.LineWidth >= SmallestRenderedSizeAccessor();

        public virtual bool IsLabelVisible(Scene scene) => IsVisible(scene);

        private Rectangle? _bbox;
        public override Rectangle BoundingBox
        {
            get
            {
                if (!_bbox.HasValue)
                {
                    _bbox = VolumeShapeAsRendered.BoundingBox();
                }

                return _bbox.Value;
            }
        }

        private ICollection<long> _OverlappedLinks;
        public override ICollection<long> OverlappedLinks
        {
            protected get => _OverlappedLinks;

            set => _OverlappedLinks = value;
        }

        public override double DistanceFromCenterNormalized(Geometry.Vector2 Position)
        {
            if (PointIntersectsAnyControlPoint(Position))
            {
                return VolumeControlPoints.Select(p => Geometry.Vector2.Distance(p, Position) / ControlPointRadius).Min();
            }
            else
            {
                //TODO: Find a more accurate measurement.  Returning 0 means the line is always on top in selection.
                LineSegment[] segs = LineSegment.SegmentsFromPoints(VolumeControlPoints);
                double MinDistance = segs.Min(l => l.DistanceToPoint(Position));
                return (LineWidth / 2.0) - MinDistance;
            }
        }

        protected bool PointIntersectsAnyControlPoint(Geometry.Vector2 WorldPosition)
        {
            Circle testCircle = new(WorldPosition, ControlPointRadius);
            return VolumeControlPoints.Any(p => testCircle.Covers(p));
        }

        protected virtual bool PointIntersectsAnyLineSegment(Geometry.Vector2 WorldPosition)
        {
            //TODO: This could be optimized considerably
            LineSegment[] lineSegs = LineSegment.SegmentsFromPoints(VolumeControlPoints);
            //Find the line segment the NewControlPoint intersects
            int iNearest = lineSegs.NearestSegment(WorldPosition, out double MinDistance);
            return MinDistance < LineWidth / 2.0f;
        }

        public override LocationAction GetPenContactActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            LocationID = ID;

            if (modifierKeys.ShiftPressed())
            {
                return LocationAction.TRANSLATE;
            }
            else
            {
                if (VisibleSectionNumber == (int)modelObj.Z)
                {
                    return LocationAction.NONE;// return LocationAction.CREATELINK;
                }
                else
                {
                    return LocationAction.NONE;
                }
            }
        }

        public override List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber)
        {
            List<IAction> actions = [];
            if (path.HasSelfIntersection)
            {
                Polygon closedpath = new(path.SimplifiedFirstLoop);
                ChangeToPolygonAction action = new(modelObj, closedpath);
                actions.Add(action);
            }
            else
            {
                Polyline openPath = new(path.SimplifiedPath);
                ChangeToPolylineAction action = new(modelObj, openPath);
                actions.Add(action);
            }

            return actions;
        }

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            LocationID = ID;

            if (modifierKeys.ShiftPressed())
            {
                //Allow user to add a control point if the mouse is not over an existing control point
                if (PointIntersectsAnyControlPoint(WorldPosition))
                {
                    return LocationAction.TRANSLATE;
                }

                return LocationAction.NONE;
            }
            else if (modifierKeys.CtrlPressed())
            {
                if (PointIntersectsAnyLineSegment(WorldPosition))
                {
                    //Allow user to add a control point if the mouse is not over an existing control point
                    if (PointIntersectsAnyControlPoint(WorldPosition))
                    {
                        if (VolumeControlPoints.Length > 2)
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
                else
                {
                    return LocationAction.NONE;
                }
            }
            else
            {
                if (VisibleSectionNumber == (int)modelObj.Z)
                {
                    //Find distance to nearest control point
                    if (PointIntersectsAnyControlPoint(WorldPosition))
                    {
                        return LocationAction.ADJUST;
                    }
                    else
                    {
                        return LocationAction.CREATELINK;
                    }
                }
                else
                {
                    return LocationAction.CREATELINKEDLOCATION;
                }
            }
        }

        public override string[] HelpStrings => [
                    "Hold left click + SHIFT on control point: Move all control points",
                    "Hold left click off control point: Create/Link annotation",
                    "Left click + CTRL on control point: Remove control point",
                    "Left click + CTRL off control point: Add a control point",
                ];

        public abstract double LineWidth { get; }

        public abstract double ControlPointRadius { get; }

        private SqlGeometry _VolumeShape;
        public override SqlGeometry VolumeShapeAsRendered
        {
            get
            {
                _VolumeShape ??= VolumeControlPoints.ToSqlGeometry().STBuffer(Math.Max(LineWidth, ControlPointRadius));

                return _VolumeShape;
            }
        }
    }

    public abstract class MultipleControlPointLocationCanvasViewBase : LocationCanvasView
    {
        /// <summary>
        /// Mosaic points composing the polyline, without added points to create a curve
        /// </summary>
        internal readonly Geometry.Vector2[] MosaicControlPoints;

        /// <summary>
        /// Mosaic points composing the polyline, without added points to create a curve
        /// </summary>
        internal readonly Geometry.Vector2[] VolumeControlPoints;

        public MultipleControlPointLocationCanvasViewBase(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj)
        {
            if (obj.MosaicShape is not IHasControlPoints controlPoints)
            {
                throw new ArgumentException(
                    $"Location {obj.ID} mosaic shape {obj.MosaicShape?.ShapeType} does not expose control points.",
                    nameof(obj));
            }

            MosaicControlPoints = [.. controlPoints.ControlPoints.Select(p => p.Convert())];
            VolumeControlPoints = mapper.SectionToVolume(MosaicControlPoints);
        }
    }
}
