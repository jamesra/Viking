using Geometry;
using Microsoft.Xna.Framework;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation.UI.AutoPolygonize;
using VikingXNAGraphics;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.UI.Actions
{
    /// <summary>
    /// Replace the exterior contour of an annotation with the passed contour
    /// </summary>
    internal class ChangeToPolygonAction : IAction, IActionView, IEquatable<ChangeToPolygonAction>
    {
        public readonly LocationObj Location;
        private readonly IVolumeToSectionTransform Transform;

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly Polygon NewVolumePolygon;

        /// <summary>
        /// The volume space polygon after smoothing
        /// </summary>
        public readonly Polygon NewSmoothVolumePolygon;

        public LocationAction Type => LocationAction.CHANGETYPE;

        public Action Execute => OnExecute;

        public static implicit operator Action(ChangeToPolygonAction a)
        {
            return a.Execute;
        }

        public IRenderable? Passive { get; set; } = null;

        public IRenderable? Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.None;

        public ChangeToPolygonAction(LocationObj location, Polygon newVolumePolygon, IVolumeToSectionTransform? transform = null)
        {
            Location = location;
            Transform = transform ?? AnnotationOverlay.ActiveSectionToVolumeTransform;
            NewVolumePolygon = newVolumePolygon;
            NewSmoothVolumePolygon = NewVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints, Global.PenSimplifyThreshold);

            CreateDefaultVisuals();
        }

        /// <summary>
        /// Maps the new volume polygon to the section, makes the location a curve polygon with that shape, and saves it.
        /// Changes nothing, and writes a trace line, when any vertex of the polygon cannot be mapped to the section.
        /// On a save fault the location's original type and shape are restored. A circle converted this way (a pen loop drawn
        /// around it) then merges with its structure's circles the loop touches, and with its polygons once the user agrees.
        /// </summary>
        public async void OnExecute()
        {
            Microsoft.SqlServer.Types.SqlGeometry original_mosaic_polygon = Location.MosaicShape.ToSqlGeometry();
            LocationType originalType = Location.TypeCode;
            Polygon mosaic_polygon = LocationVolumeGeometry.TryMapPolygonToMosaic(NewVolumePolygon, Transform);
            if (mosaic_polygon is null)
            {
                Trace.WriteLine("ChangeToPolygonAction: new polygon crosses an unmapped region of the section; location " + Location.ID + " unchanged.", "WebAnnotation");
                return;
            }

            Location.TypeCode = LocationType.CURVEPOLYGON;
            Location.SetShapeFromGeometryInSection(Transform, mosaic_polygon.ToSqlGeometry());

            bool saved;
            try
            {
                saved = await Store.Locations.Save();
                UndoManager.PushLocationShape("Change Type", Location, Transform, original_mosaic_polygon, originalType);
            }
            catch (System.ServiceModel.FaultException e)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
                Location.TypeCode = originalType;
                Location.SetShapeFromGeometryInSection(Transform, original_mosaic_polygon);
                return;
            }

            if (saved && originalType == LocationType.CIRCLE)
                await MergeWithSameStructureAsync();
        }

        /// <summary>
        /// A loop drawn around a circle also takes in the structure's other circles it touches. The structure's polygons it touches
        /// are merged only after the user says so. Works in Viking and Jotunn: the transform is the action's own, and the question
        /// is owned by Viking's viewer when there is one. Failures are reported, never thrown, as this runs from async void.
        /// </summary>
        private async Task MergeWithSameStructureAsync()
        {
            try
            {
                Viking.UI.Controls.SectionViewerControl? viewer = AnnotationOverlay.CurrentOverlay?.Parent;
                await SameStructureMerge.MergeAfterPenLoopAsync(
                    Location,
                    Transform,
                    AnnotationOverlay.ActiveDownsample,
                    polygons => System.Windows.Forms.MessageBox.Show(
                        viewer,
                        PolygonMergeQuestion(polygons),
                        "Merge polygons",
                        System.Windows.Forms.MessageBoxButtons.YesNo,
                        System.Windows.Forms.MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes,
                    () => viewer?.Invalidate());
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"ChangeToPolygonAction: merging location {Location.ID} with its structure failed: {ex}", "WebAnnotation");
                System.Windows.Forms.MessageBox.Show($"Could not merge location {Location.ID} with the other annotations of its structure: {ex.Message}",
                    "Merge polygons", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>The question asked before a pen loop's polygon takes in <paramref name="polygons"/> of the same structure.</summary>
        internal static string PolygonMergeQuestion(IReadOnlyList<LocationObj> polygons) =>
            polygons.Count == 1
                ? $"The new outline overlaps another polygon of this structure (location {polygons[0].ID}). Merge them into one?"
                : $"The new outline overlaps {polygons.Count} other polygons of this structure ({string.Join(", ", polygons.Select(p => p.ID))}). Merge them into one?";

        public void CreateDefaultVisuals()
        {
            SolidPolygonView view = new(NewSmoothVolumePolygon, Color.Green.SetAlpha(0.5f));
            Passive = view;
            Active = new SolidPolygonView(NewSmoothVolumePolygon, Color.Green.SetAlpha(1f));
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

            if (other is not ChangeToPolygonAction other_action)
            {
                return false;
            }

            return Equals(other_action);
        }

        public bool Equals(ChangeToPolygonAction other)
        {
            if (other.Location.ID != Location.ID)
            {
                return false;
            }

            return NewVolumePolygon.Equals(other.NewVolumePolygon);
        }
    }

    /// <summary>
    /// Replace the exterior contour of an annotation with the passed contour
    /// </summary>
    internal class ChangeToPolylineAction : IAction, IActionView, IEquatable<ChangeToPolylineAction>
    {
        public readonly LocationObj Location;
        private readonly IVolumeToSectionTransform Transform;

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly Polyline NewVolumePolyline;

        /// <summary>
        /// The volume space polygon after smoothing
        /// </summary>
        public readonly Polyline NewSmoothVolumePolyline;

        public LocationAction Type => LocationAction.CHANGETYPE;

        public Action Execute => OnExecute;

        public static implicit operator Action(ChangeToPolylineAction a)
        {
            return a.Execute;
        }

        public IRenderable? Passive { get; set; } = null;

        public IRenderable? Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.None;

        public ChangeToPolylineAction(LocationObj location, Polyline newVolumePolyline, IVolumeToSectionTransform? transform = null)
        {
            Location = location;
            Transform = transform ?? AnnotationOverlay.ActiveSectionToVolumeTransform;
            NewVolumePolyline = newVolumePolyline;
            NewSmoothVolumePolyline = NewVolumePolyline.Smooth(Global.NumClosedCurveInterpolationPoints);

            CreateDefaultVisuals();
        }

        /// <summary>
        /// Maps the new volume polyline to the section, makes the location a polyline with that shape, and saves it.
        /// Changes nothing, and writes a trace line, when any vertex of the polyline cannot be mapped to the section.
        /// On a save fault the location's original type and shape are restored.
        /// </summary>
        public async void OnExecute()
        {
            Microsoft.SqlServer.Types.SqlGeometry originalShape = Location.MosaicShape.ToSqlGeometry();
            LocationType originalType = Location.TypeCode;
            Polyline mosaic_polygon = LocationVolumeGeometry.TryMapPolylineToMosaic(NewVolumePolyline, Transform);
            if (mosaic_polygon is null)
            {
                Trace.WriteLine("ChangeToPolylineAction: new polyline crosses an unmapped region of the section; location " + Location.ID + " unchanged.", "WebAnnotation");
                return;
            }

            Location.TypeCode = LocationType.POLYLINE;
            Location.SetShapeFromGeometryInSection(Transform, mosaic_polygon.ToSqlGeometry());

            try
            {
                await Store.Locations.Save();
                UndoManager.PushLocationShape("Change Type", Location, Transform, originalShape, originalType);
            }
            catch (System.ServiceModel.FaultException e)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
                Location.TypeCode = originalType;
                Location.SetShapeFromGeometryInSection(Transform, originalShape);
            }
        }

        public void CreateDefaultVisuals()
        {
            PolyLineView view = new(NewSmoothVolumePolyline, Color.Green.SetAlpha(0.5f));
            Passive = view;
            Active = new PolyLineView(NewSmoothVolumePolyline, Color.Green.SetAlpha(1f));
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

            if (other is not ChangeToPolylineAction other_action)
            {
                return false;
            }

            return Equals(other_action);
        }

        public bool Equals(ChangeToPolylineAction other)
        {
            if (other.Location.ID != Location.ID)
            {
                return false;
            }

            return NewVolumePolyline.Equals(other.NewVolumePolyline);
        }
    }
}
