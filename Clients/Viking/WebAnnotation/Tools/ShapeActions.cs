using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation.UI.Commands.Segmentation;
using WebAnnotation.ViewModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Viking's "Change Shape" and "Simplify Shape" menu actions for hosts without its overlay. Each one saves and is
    /// recorded for Ctrl+Z.
    /// </summary>
    static class ShapeActions
    {
        /// <summary>Vertices of the ring that replaces a circle, as in Viking's "Convert to Polygon".</summary>
        const int CirclePolygonVertexCount = 24;

        /// <summary>A circle at the location's center with its radius.</summary>
        public static Task ConvertToCircleAsync(AnnotationToolContext context, LocationObj location)
        {
            IVolumeToSectionTransform mapper = context.Mapper;
            Vector2 mosaicCenter = location.Position;
            if (!mapper.TrySectionToVolume(mosaicCenter, out _))
            {
                context.SetStatus("This location could not be mapped onto the volume");
                return Task.CompletedTask;
            }

            return ChangeAndSaveAsync(context, location, "Change Shape", () =>
            {
                // location.Radius is a mosaic radius: the circle starts in mosaic space and its volume circle is mapped from it.
                double mosaicRadius = location.Radius;
                if (!LocationActions.UpdateCircleLocationFromMosaicNoSaveCallback(location, mapper, mosaicCenter, mosaicRadius))
                    return false;

                location.TypeCode = LocationType.CIRCLE;
                return true;
            });
        }

        /// <summary>
        /// Viking's "Convert to Polygon": a 24-sided ring on the circle's outline, without segmentation. Once saved, the polygon
        /// merges with the overlapping polygons of its structure on the section, as in Viking.
        /// </summary>
        public static async Task ConvertCircleToPolygonAsync(AnnotationToolContext context, LocationObj circle)
        {
            IVolumeToSectionTransform mapper = context.Mapper;
            double downsample = context.Host.Downsample;
            Vector2 center = circle.Position;
            double radius = circle.Radius;
            Vector2[] mosaicRing = new Vector2[CirclePolygonVertexCount + 1];
            for (int i = 0; i < CirclePolygonVertexCount; i++)
            {
                double angle = 2.0 * Math.PI * i / CirclePolygonVertexCount;
                mosaicRing[i] = new Vector2(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
            }

            mosaicRing[CirclePolygonVertexCount] = mosaicRing[0];
            IReadOnlyList<Vector2> volumeRing = CircleSegmentationPrompts.ToVolumePoints(mosaicRing, mapper);
            if (volumeRing.Count != mosaicRing.Length)
            {
                context.SetStatus("This circle could not be mapped onto the volume");
                return;
            }

            bool saved = await ChangeAndSaveAsync(context, circle, "Change Shape", () =>
            {
                LocationShapeUpdate.AssignSimplifiedVolumePolygon(circle, new Polygon(volumeRing.ToArray()), mapper, downsample);
                return true;
            });
            if (saved)
                await AnnotationToolActions.MergeOverlappingSiblingsAsync(context, circle, mapper);
        }

        /// <summary>Viking's "Simplify Shape": drops exterior vertices within the pen simplify threshold.</summary>
        public static Task SimplifyPolygonAsync(AnnotationToolContext context, LocationObj location)
        {
            return ChangeAndSaveAsync(context, location, "Simplify Shape", () =>
            {
                Polygon polygon = location.MosaicShape.ToPolygon();
                Polygon simple = new Polygon(polygon.ExteriorRing).Simplify(Global.PenSimplifyThreshold);
                if (simple.ExteriorRing.Length >= polygon.ExteriorRing.Length)
                {
                    context.SetStatus("The shape is already as simple as the threshold allows");
                    return false;
                }

                polygon.ExteriorRing = simple.ExteriorRing;
                location.SetShapeFromGeometryInSection(context.Mapper, polygon.ToSqlGeometry());
                return true;
            });
        }

        /// <summary>
        /// Viking's change-type hotkeys for curves and polygons: the location takes <paramref name="type"/> and the shape
        /// through <paramref name="volumePoints"/>.
        /// </summary>
        public static Task ReplaceWithPointsAsync(AnnotationToolContext context, LocationObj location, LocationType type, Vector2[] volumePoints)
        {
            IVolumeToSectionTransform mapper = context.Mapper;
            return ChangeAndSaveAsync(context, location, "Change Type", () =>
            {
                bool wasCurve = location.TypeCode is LocationType.OPENCURVE or LocationType.CLOSEDCURVE;
                location.TypeCode = type;
                if (!wasCurve && type is LocationType.OPENCURVE or LocationType.CLOSEDCURVE)
                    location.Width = 8.0; // Viking's width for new curves; a curve keeps the width it had
                location.SetShapeFromPointsInVolume(mapper, volumePoints, null);
                return true;
            });
        }

        /// <summary>Applies <paramref name="change"/> (false leaves the location alone), saves, and records the old shape.</summary>
        /// <returns>True when the change was made and saved.</returns>
        static async Task<bool> ChangeAndSaveAsync(AnnotationToolContext context, LocationObj location, string description, Func<bool> change)
        {
            IVolumeToSectionTransform mapper = context.Mapper;
            SqlGeometry undoShape = location.MosaicShape.ToSqlGeometry();
            LocationType undoType = location.TypeCode;
            try
            {
                if (!change())
                    return false;
            }
            catch (ArgumentException ex)
            {
                Trace.WriteLine($"{description} failed: {ex}");
                context.SetStatus($"{description} failed: {ex.Message}");
                return false;
            }

            context.Invalidate();
            return await AnnotationToolActions.SaveShapeEditAsync(context, location, description, mapper, undoShape, undoType);
        }
    }
}
