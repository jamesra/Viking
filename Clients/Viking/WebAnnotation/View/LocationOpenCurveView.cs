using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundCurve;
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
using HorizontalAlignment = RoundCurve.HorizontalAlignment;

namespace WebAnnotation.View
{
    internal class LocationOpenCurveView : LocationCurveView, IColorView, IRenderedLabelView
    {
        public static uint NumInterpolationPoints = Global.NumOpenCurveInterpolationPoints;

        private GridVector2[] _MosaicCurveControlPoints;

        private SqlGeometry _RenderedVolumeShape;

        private GridVector2[] _VolumeCurveControlPoints;
        public CurveLabel curveLabel;
        public CurveLabel curveParentLabel;

        public CurveView curveView;

        public LocationOpenCurveView(LocationObj obj, IVolumeToSectionTransform mapper, double lineWidth) : base(obj,
            mapper)
        {
            //RegisterForLocationEvents();
            //RegisterForStructureChangeEvents();

            Color color = obj.Parent is null ? Color.Gray.SetAlpha(0.5f) : obj.Parent.Type.Color.ToXNAColor(0.5f);
            curveView = new CurveView(VolumeControlPoints, color, false, Global.NumOpenCurveInterpolationPoints,
                lineWidth: lineWidth, lineStyle: LineStyle.Tubular, controlPointRadius: lineWidth / 2.0,
                ShowControlPoints: !Global.PenMode);
            CreateLabelViews(VolumeControlPoints, obj.ParentID);
        }

        public LocationOpenCurveView(LocationObj obj, IVolumeToSectionTransform mapper) : base(obj, mapper)
        {
            //RegisterForLocationEvents();
            //RegisterForStructureChangeEvents();

            Color color = obj.Parent is null ? Color.Gray.SetAlpha(0.5f) : obj.Parent.Type.Color.ToXNAColor(0.5f);
            curveView = new CurveView(VolumeControlPoints, color, false, Global.NumOpenCurveInterpolationPoints,
                lineWidth: obj.Width.Value, lineStyle: LineStyle.Tubular, controlPointRadius: obj.Width.Value / 2.0,
                ShowControlPoints: !Global.PenMode);
            CreateLabelViews(VolumeControlPoints, obj.ParentID);
        }

        public Color LabelTextColor
        {
            get => curveLabel.Color;
            set => curveLabel.Color = value;
        }

        public float LabelTextAlpha
        {
            get => curveLabel.Alpha;
            set => curveLabel.Alpha = value;
        }

        public float ParentLabelTextAlpha
        {
            get => curveParentLabel.Alpha;
            set => curveParentLabel.Alpha = value;
        }

        public override GridVector2[] MosaicCurveControlPoints
        {
            get
            {
                _MosaicCurveControlPoints ??= [.. MosaicControlPoints.CalculateCurvePoints(NumInterpolationPoints, false)];

                return _MosaicCurveControlPoints;
            }
        }

        public override GridVector2[] VolumeCurveControlPoints
        {
            get
            {
                _VolumeCurveControlPoints ??= [.. VolumeControlPoints.CalculateCurvePoints(NumInterpolationPoints, false)];

                return _VolumeCurveControlPoints;
            }
        }

        public override SqlGeometry VolumeShapeAsRendered
        {
            get
            {
                _RenderedVolumeShape ??= VolumeCurveControlPoints.ToSqlGeometry().STBuffer(LineWidth / 2.0);

                return _RenderedVolumeShape;
            }
        }

        public override double LineWidth => curveView.LineWidth;

        public override double ControlPointRadius => LineWidth / 2.0;

        public override Color Color
        {
            get => curveView.Color;
            set => curveView.Color = value;
        }

        public override float Alpha
        {
            get => curveView.Alpha;
            set => curveView.Alpha = value;
        }

        /// <summary>
        ///     Draw the text for the location at the specified screen coordinates
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="font"></param>
        /// <param name="ScreenDrawPosition">Center of the annotation in screen space, which is the coordinate system used for text</param>
        /// <param name="MagnificationFactor"></param>
        /// <param name="DirectionToVisiblePlane">The Z distance of the location to the plane viewed by user.</param>
        public void DrawLabel(GraphicsDevice device, SpriteBatch spriteBatch, SpriteFont font, Scene scene)
        {
            if (font is null)
            {
                throw new ArgumentNullException("font");
            }

            if (spriteBatch is null)
            {
                throw new ArgumentNullException("spriteBatch");
            }

            CurveManager curveManager = DeviceEffectsStore<CurveManager>.TryGet(device);
            if (curveManager is null)
            {
                return;
            }

            CurveLabel.Draw(device, scene, spriteBatch, font, curveManager, [curveLabel, curveParentLabel]);
        }

        private void CreateLabelViews(GridVector2[] controlPoints, long? ParentID)
        {
            string LabelText = this.ParentID + " " + FullLabelText();

            string ParentStructureLabelText = "";
            if (Parent != null && Parent.ParentID.HasValue)
            {
                ParentStructureLabelText = Parent.ParentID.ToString();
                LabelText = Parent.Type.Code + " " + LabelText;
            }

            Color LabelColor = modelObj.IsUnverifiedTerminal ? Color.Yellow : Color.Black;
            LabelColor = LabelColor.SetAlpha(0.5f);
            Color ParentLabelColor = new(1.0f, 0, 0, 0.5f);

            curveLabel = new CurveLabel(LabelText, controlPoints, LabelColor, false, lineWidth: LineWidth);
            curveParentLabel = new CurveLabel(ParentStructureLabelText, controlPoints, ParentLabelColor, false,
                lineWidth: LineWidth);

            curveLabel.Alignment = HorizontalAlignment.Left;
            curveParentLabel.Alignment = HorizontalAlignment.Right;

            float TotalLabelLength = curveLabel.Text.Length + 1 + curveParentLabel.Text.Length;
            curveLabel.Max_Curve_Length_To_Use_Normalized = curveLabel.Text.Length / TotalLabelLength;

            curveParentLabel.Max_Curve_Length_To_Use_Normalized = curveParentLabel.Text.Length / TotalLabelLength;
            curveParentLabel.LabelEndDistance = 0.90f;
        }

        public static void Draw(GraphicsDevice device,
            Scene scene,
            CurveManager curveManager,
            BasicEffect basicEffect,
            OverlayShaderEffect overlayEffect,
            LocationOpenCurveView[] listToDraw)
        {
            CurveView.Draw(device, scene, curveManager, basicEffect, overlayEffect, 0,
                [.. listToDraw.Select(l => l.curveView)]);
        }


        internal override void OnParentPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == "Label" || args.PropertyName == "Attributes")
            {
                CreateLabelViews(VolumeControlPoints, ParentID);
            }

            base.OnParentPropertyChanged(o, args);
        }

        internal override void OnObjPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            if (IsLocationPropertyAffectingLabels(args.PropertyName))
            {
                CreateLabelViews(VolumeControlPoints, ParentID);
            }

            base.OnObjPropertyChanged(o, args);
        }

        public override List<IAction> GetPenActionsForShapeAnnotation(Path path,
            IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber)
        {
            List<IAction> listActions = [];
            IVolumeToSectionTransform mapper =
                AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform;

            if (path.HasSelfIntersection == false)
            {
                //If it is an open curve then offer to replace our curve with the new shape.
                IAction changeContour = new Change1DContourAction(modelObj, new GridPolyline(path.SimplifiedPath));
                listActions.Add(changeContour);
            }

            listActions.AddRange(interaction_log.IdentifyPossibleLinkActions(modelObj.ID));
            return listActions;
        }
    }
}