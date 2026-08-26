using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.Tools
{
    static class AnnotationToolActions
    {
        public static async Task CreateStructureAtAsync(
            AnnotationToolContext context,
            LocationType shapeType,
            Vector2 world,
            Vector2[] volumePoints)
        {
            try
            {
                long? typeId = context.SelectedStructureTypeId();
                if (!typeId.HasValue && Store.IsInitialized && Store.StructureTypes.RootObjects.Count > 0)
                    typeId = (long)Store.StructureTypes.RootObjects[0];
                if (!typeId.HasValue)
                    return;
                if (!Store.StructureTypes.TryGetObjectByID(typeId.Value, out StructureTypeObj type) || type == null)
                    return;

                IVolumeToSectionTransform mapper = context.Mapper;
                if (!mapper.TryVolumeToSection(world, out Vector2 mosaic))
                    return;

                StructureObj structure = new(type);
                LocationObj location = new(structure, context.SectionNumber, shapeType);
                if (shapeType == LocationType.CIRCLE)
                    LocationActions.UpdateCircleLocationNoSaveCallback(location, world, mosaic, 16);
                else if (volumePoints != null && volumePoints.Length > 0)
                    location.SetShapeFromPointsInVolume(mapper, volumePoints, null);

                var result = await Store.Structures.Create(structure, location);
                if (result.Location != null)
                    Global.LastEditedAnnotationID = result.Location.ID;
                context.Invalidate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Create structure failed: {ex}");
            }
        }

        public static async Task CreateLinkedLocationAsync(
            AnnotationToolContext context,
            long existingId,
            Vector2 world,
            LocationType shapeType,
            Vector2[] volumePoints)
        {
            try
            {
                LocationObj existing = await Store.Locations.GetObjectByID(existingId);
                if (existing is null)
                    return;

                IVolumeToSectionTransform mapper = context.Mapper;
                if (!mapper.TryVolumeToSection(world, out Vector2 mosaic))
                    return;

                int existingSection = (int)Math.Round(existing.Z);
                if (existingSection == context.SectionNumber)
                    return;

                LocationType type = shapeType == LocationType.CIRCLE ? LocationType.CIRCLE : existing.TypeCode;
                LocationObj created = new(existing.Parent, context.SectionNumber, type);
                if (type == LocationType.CIRCLE)
                    LocationActions.UpdateCircleLocationNoSaveCallback(created, world, mosaic, Math.Max(existing.Radius, Global.MinRadius));
                else if (volumePoints != null && volumePoints.Length > 0)
                    created.SetShapeFromPointsInVolume(mapper, volumePoints, null);

                await Store.Locations.Create(created, [existing.ID]);
                Global.LastEditedAnnotationID = created.ID;
                context.Invalidate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Create linked location failed: {ex}");
            }
        }

        public static async Task CreateLocationLinkAsync(AnnotationToolContext context, long a, long b)
        {
            try
            {
                await Store.LocationLinks.CreateLink(a, b);
                context.Invalidate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Create location link failed: {ex}");
            }
        }

        public static async Task SaveLocationAsync(AnnotationToolContext context, LocationObj location)
        {
            try
            {
                await Store.Locations.Save();
                Global.LastEditedAnnotationID = location.ID;
                context.Invalidate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Save location failed: {ex}");
            }
        }

        public static async Task DeleteLocationAsync(AnnotationToolContext context, LocationObj location)
        {
            try
            {
                await Store.Locations.Remove(location);
                if (Global.LastEditedAnnotationID == location.ID)
                    Global.LastEditedAnnotationID = null;
                context.Invalidate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Delete location failed: {ex}");
            }
        }
    }
}
