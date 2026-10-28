using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Actions
{

    /// <summary>
    /// Replace the exterior contour of an annotation with the passed contour
    /// </summary>
    internal class Change2DContourAction : IAction, IEquatable<Change2DContourAction>
    {
        public readonly LocationObj Location;
        private readonly IVolumeToSectionTransform Transform;

        /// <summary>
        /// The mosaic space polygon we want to commit to the database
        /// </summary>
        public readonly Polygon NewMosaicPolygon;

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly Polygon NewVolumePolygon;

        /// <summary>
        /// <see cref="NewVolumePolygon"/> as it will be drawn: its points are curve control points, so the choice shows the
        /// curve through them. Smoothed the first time it is drawn.
        /// </summary>
        public Polygon NewSmoothedVolumePolygon => _newSmoothedVolumePolygon.Value;

        private readonly Lazy<Polygon> _newSmoothedVolumePolygon;

        /// <summary>
        /// Cell ring the cut started from. When set, the choice fill reuses that triangulation
        /// instead of meshing <see cref="NewVolumePolygon"/> when the kept piece is almost the whole cell.
        /// </summary>
        internal Polygon PreviewOriginal;

        /// <summary>Cap removed from <see cref="PreviewOriginal"/> for a shrink choice. Not drawn by this action.</summary>
        internal Polygon PreviewExcludedPatch;

        /// <summary>Lobe added outside <see cref="PreviewOriginal"/> for a growth choice.</summary>
        internal Polygon PreviewAddedPatch;

        /// <summary>Pen path that cut <see cref="PreviewOriginal"/>. Used to keep the cap flood on the near side of the cut.</summary>
        internal IReadOnlyList<Vector2> PreviewPath;

        public LocationAction Type => LocationAction.CHANGEBOUNDARY;

        public RetraceCommandAction RetraceType { get; } = RetraceCommandAction.NONE;

        public Action Execute => OnExecute;

        public static implicit operator Action(Change2DContourAction a)
        {
            return a.Execute;
        }

        /// <summary>
        /// Indicates if this action represents the ClockWiseContour when we are cutting a shape in half
        /// </summary>
        internal bool ClockwiseContour = false;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.None;
        public Change2DContourAction(long locationID, RetraceCommandAction retraceType, Polygon newMosaicPolygon, Polygon? newVolumePolygon = null, bool ClockwiseContour = false, IVolumeToSectionTransform? transform = null)
        {
            this.ClockwiseContour = ClockwiseContour;
            RetraceType = retraceType;
            Location = Store.Locations[locationID];

            Transform = transform ?? WebAnnotation.AnnotationOverlay.ActiveSectionToVolumeTransform;

            NewMosaicPolygon = newMosaicPolygon;
            NewVolumePolygon = newVolumePolygon ?? Transform.TryMapShapeSectionToVolume(newMosaicPolygon);
            _newSmoothedVolumePolygon = SmoothWhenDrawn(NewVolumePolygon);
        }

        public Change2DContourAction(LocationObj location, RetraceCommandAction retraceType, Polygon newMosaicPolygon, Polygon? newVolumePolygon = null, bool ClockwiseContour = false, IVolumeToSectionTransform? transform = null)
        {
            this.ClockwiseContour = ClockwiseContour;
            RetraceType = retraceType;
            Location = location;
            Transform = transform ?? AnnotationOverlay.ActiveSectionToVolumeTransform;

            NewMosaicPolygon = newMosaicPolygon;

            NewVolumePolygon = newVolumePolygon is null ? Transform.TryMapShapeSectionToVolume(newMosaicPolygon) : newVolumePolygon;
            _newSmoothedVolumePolygon = SmoothWhenDrawn(NewVolumePolygon);
        }

        private static Lazy<Polygon> SmoothWhenDrawn(Polygon volumePolygon) =>
            new(() => volumePolygon?.Smooth(Global.NumClosedCurveInterpolationPointsForDisplay));

        public async void OnExecute()
        {
            Microsoft.SqlServer.Types.SqlGeometry original_mosaic_polygon = Location.MosaicShape.ToSqlGeometry();
            //var mosaic_polygon = Transform.TryMapShapeVolumeToSection(NewVolumePolygon);
            WebAnnotation.View.PolygonViewTimingLog.MarkBoundaryChange(Location.ID, "ChangeContour");
            Location.SetShapeFromGeometryInSection(Transform, NewMosaicPolygon.ToSqlGeometry());

            try
            {
                await Store.Locations.Save();
                UndoManager.PushLocationShape("Change Contour", Location, Transform, original_mosaic_polygon, Location.TypeCode);
            }
            catch (System.ServiceModel.FaultException e)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
                Location.SetShapeFromGeometryInSection(Transform, original_mosaic_polygon);
            }
        }

        public bool Equals(IAction other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Type != other.Type)
            {
                return false;
            }

            if (other is not Change2DContourAction other_action)
            {
                return false;
            }

            return Equals(other_action);
        }

        public bool Equals(Change2DContourAction other)
        {
            if (other.Location.ID != Location.ID)
            {
                return false;
            }

            return NewVolumePolygon.Equals(other.NewVolumePolygon);
        }

        public override string ToString() => $"{base.ToString()} {Type} {RetraceType}";
    }

    /// <summary>
    /// Replace the exterior contour of an annotation with the passed contour
    /// </summary>
    internal class Change1DContourAction(LocationObj location, Polyline newVolumePolyline, IVolumeToSectionTransform? transform = null) : IAction, IEquatable<Change1DContourAction>
    {
        public readonly LocationObj Location = location;
        private readonly IVolumeToSectionTransform Transform = transform ?? AnnotationOverlay.ActiveSectionToVolumeTransform;

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly Polyline NewVolumePolyline = newVolumePolyline;

        /// <summary>
        /// The volume space polygon after smoothing
        /// </summary>
        public readonly Polyline NewSmoothVolumePolyline = newVolumePolyline.Smooth(Global.NumOpenCurveInterpolationPoints);

        public LocationAction Type => LocationAction.CHANGEBOUNDARY;

        public Action Execute => OnExecute;

        public static implicit operator Action(Change1DContourAction a)
        {
            return a.Execute;
        }

        /// <summary>
        /// Maps the new volume polyline to the section, replaces the location's shape with it, and saves it.
        /// Changes nothing, and writes a trace line, when any vertex of the polyline cannot be mapped to the section.
        /// On a save fault the location's original shape is restored.
        /// </summary>
        public async void OnExecute()
        {
            Microsoft.SqlServer.Types.SqlGeometry originalShape = Location.MosaicShape.ToSqlGeometry();
            Polyline mosaic_shape = Transform.TryMapShapeVolumeToSection(NewVolumePolyline);
            if (mosaic_shape is null)
            {
                Trace.WriteLine("Change1DContourAction: new polyline crosses an unmapped region of the section; location " + Location.ID + " unchanged.", "WebAnnotation");
                return;
            }

            Location.SetShapeFromGeometryInSection(Transform, mosaic_shape.ToSqlGeometry());

            try
            {
                await Store.Locations.Save();
                UndoManager.PushLocationShape("Change Contour", Location, Transform, originalShape, Location.TypeCode);
            }
            catch (System.ServiceModel.FaultException e)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
                Location.SetShapeFromGeometryInSection(Transform, originalShape);
            }
        }

        public bool Equals(IAction other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Type != other.Type)
            {
                return false;
            }

            if (other is not Change1DContourAction other_action)
            {
                return false;
            }

            return Equals(other_action);
        }

        public bool Equals(Change1DContourAction other)
        {
            if (other.Location.ID != Location.ID)
            {
                return false;
            }

            return NewVolumePolyline.Equals(other.NewVolumePolyline);
        }

        public override string ToString() => $"{base.ToString()} {Type}";

    }
}
