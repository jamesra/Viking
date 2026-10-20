using Microsoft.SqlServer.Types;
using System;
using System.Collections.Generic;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation
{
    /// <summary>
    /// A bounded stack of reversible annotation edits.  Edits push an entry after the server accepted them,
    /// and Ctrl+Z pops the newest entry and runs its inverse.
    /// Only in-place edits of an existing location (same ID) are recorded; creating or deleting rows is not undoable.
    /// </summary>
    internal static class UndoManager
    {
        private const int MaxEntries = 50;

        private sealed record UndoEntry(string Description, Action Undo);

        private static readonly LinkedList<UndoEntry> Entries = new();

        public static bool CanUndo => Entries.Count > 0;

        /// <summary>
        /// Description of the edit Ctrl+Z would revert, or null if there is nothing to undo
        /// </summary>
        public static string NextUndoDescription => Entries.Last?.Value.Description;

        /// <summary>Forgets every recorded edit without reverting any.</summary>
        public static void Clear() => Entries.Clear();

        public static void Push(string description, Action undo)
        {
            Entries.AddLast(new UndoEntry(description, undo));
            while (Entries.Count > MaxEntries)
            {
                Entries.RemoveFirst();
            }
        }

        /// <summary>
        /// Record that <paramref name="location"/> had <paramref name="oldShape"/> and <paramref name="oldType"/>
        /// before an edit that has already been saved.  Pass the mosaic shape captured before the edit.
        /// </summary>
        public static void PushLocationShape(string description, LocationObj location, IVolumeToSectionTransform transform, SqlGeometry oldShape, LocationType oldType)
        {
            long id = location.ID;
            Push(description, () => RestoreLocation(id, location, loc =>
            {
                loc.TypeCode = oldType;
                loc.SetShapeFromGeometryInSection(transform, oldShape);
            }));
        }

        /// <summary>
        /// Record that <paramref name="location"/> had the given terminal/off-edge flags before an edit that has already been saved.
        /// </summary>
        public static void PushLocationFlags(string description, LocationObj location, bool oldTerminal, bool oldOffEdge)
        {
            long id = location.ID;
            Push(description, () => RestoreLocation(id, location, loc =>
            {
                loc.Terminal = oldTerminal;
                loc.OffEdge = oldOffEdge;
            }));
        }

        /// <summary>
        /// Reverts the newest recorded edit and starts saving it.  Returns false if there was nothing to undo.
        /// </summary>
        public static bool UndoLast()
        {
            if (Entries.Last is null)
            {
                return false;
            }

            UndoEntry entry = Entries.Last.Value;
            Entries.RemoveLast();

            // The inverse edits the cached location; its save runs asynchronously and reports its own failure.
            entry.Undo();
            return true;
        }

        /// <summary>
        /// The location object may have been replaced by a refresh since the edit, so look it up again by ID.
        /// </summary>
        private static void RestoreLocation(long id, LocationObj originalReference, Action<LocationObj> restore)
        {
            LocationObj loc = Store.Locations.Contains(id) ? Store.Locations.GetCachedObject(id) : originalReference;
            if (loc is null)
            {
                return;
            }

            restore(loc);
            _ = AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
            Global.LastEditedAnnotationID = id;
            AnnotationOverlay.CurrentOverlay?.InvalidateParent();
        }
    }
}
