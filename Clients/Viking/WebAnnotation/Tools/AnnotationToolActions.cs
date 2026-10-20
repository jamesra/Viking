using System;
using System.Collections.Generic;
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
        /// <summary>
        /// Creates a structure of <paramref name="typeId"/> (else the type chosen in the structure type list) with its first
        /// location, carrying <paramref name="tags"/> as Viking's structure hotkeys do. Nothing is created without a type.
        /// </summary>
        public static async Task CreateStructureAtAsync(
            AnnotationToolContext context,
            LocationType shapeType,
            Vector2 world,
            Vector2[] volumePoints,
            long? typeId = null,
            IReadOnlyList<string> tags = null)
        {
            try
            {
                typeId ??= context.SelectedStructureTypeId();
                if (!typeId.HasValue)
                {
                    context.SetStatus("Choose a structure type first");
                    return;
                }
                if (!Store.StructureTypes.TryGetObjectByID(typeId.Value, out StructureTypeObj type) || type == null)
                {
                    context.SetStatus($"Structure type {typeId.Value} is not in this volume");
                    return;
                }

                IVolumeToSectionTransform mapper = context.Mapper;
                if (!mapper.TryVolumeToSection(world, out Vector2 mosaic))
                    return;

                StructureObj structure = new(type);
                if (tags != null)
                {
                    foreach (string tag in tags)
                        await structure.ToggleAttribute(tag);
                }

                LocationObj location = new(structure, context.SectionNumber, shapeType);
                if (shapeType == LocationType.OPENCURVE || shapeType == LocationType.CLOSEDCURVE)
                    location.Width = 8.0; // Viking's width for new curves
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

        /// <summary>
        /// Viking's continue-last-trace: a location linked to the last edited one at <paramref name="world"/>, when that
        /// location is on another section. A cache miss still continues; the location is loaded from the server.
        /// </summary>
        public static bool TryContinueLastTrace(AnnotationToolContext context, Vector2 world)
        {
            if (!Global.LastEditedAnnotationID.HasValue)
                return false;
            if (Store.Locations.TryGetObjectByID(Global.LastEditedAnnotationID.Value, out LocationObj last) && last != null
                && (int)Math.Round(last.Z) == context.SectionNumber)
                return false;

            _ = CreateLinkedLocationAsync(context, Global.LastEditedAnnotationID.Value, world, LocationType.CIRCLE, null);
            return true;
        }

        /// <summary>Viking's structure tag hotkey: toggles the tag on the location's structure and saves, undoing it if the save fails.</summary>
        public static async Task ToggleStructureTagAsync(AnnotationToolContext context, LocationObj location, string tag, string value)
        {
            try
            {
                if (location?.ParentID is not long parentId)
                    return;
                StructureObj structure = await Store.Structures.GetObjectByID(parentId);
                if (structure == null)
                    return;

                await structure.ToggleAttribute(tag, value);
                if (!await Store.Structures.Save())
                {
                    await structure.ToggleAttribute(tag, value);
                    context.SetStatus($"Could not save tag {tag} on structure {parentId}");
                    return;
                }

                context.SetStatus($"Toggled {tag} on structure {parentId}");
                context.Invalidate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Toggle structure tag failed: {ex}");
            }
        }

        /// <summary>Viking's location tag hotkey: toggles the tag on the location and saves, undoing it if the save fails.</summary>
        public static async Task ToggleLocationTagAsync(AnnotationToolContext context, LocationObj location, string tag, string value)
        {
            try
            {
                await location.ToggleAttribute(tag, value);
                if (!await Store.Locations.Save())
                {
                    await location.ToggleAttribute(tag, value);
                    context.SetStatus($"Could not save tag {tag} on location {location.ID}");
                    return;
                }

                context.SetStatus($"Toggled {tag} on location {location.ID}");
                context.Invalidate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Toggle location tag failed: {ex}");
            }
        }

        /// <summary>Viking's terminal hotkey: flips the location's Terminal flag and saves, restoring it if the save fails.</summary>
        public static async Task ToggleTerminalAsync(AnnotationToolContext context, LocationObj location)
        {
            try
            {
                bool previous = location.Terminal;
                location.Terminal = !previous;
                if (!await Store.Locations.Save())
                {
                    location.Terminal = previous;
                    context.SetStatus($"Could not save location {location.ID}");
                    return;
                }

                UndoManager.PushLocationFlags("Toggle Terminal", location, previous, location.OffEdge);

                context.SetStatus(location.Terminal ? $"Location {location.ID} is terminal" : $"Location {location.ID} is not terminal");
                context.Invalidate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Toggle terminal failed: {ex}");
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

        /// <summary>Saves pending location edits; <paramref name="onSaved"/> runs once the server accepted them (undo records).</summary>
        public static async Task SaveLocationAsync(AnnotationToolContext context, LocationObj location, Action onSaved = null)
        {
            try
            {
                if (!await Store.Locations.Save())
                {
                    context.SetStatus($"Could not save location {location.ID}");
                    return;
                }

                onSaved?.Invoke();
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
