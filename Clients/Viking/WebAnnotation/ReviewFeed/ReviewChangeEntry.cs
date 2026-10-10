using System;
using System.Collections.Generic;
using Geometry;
using SqlGeometryUtils;
using WebAnnotationModel;

namespace WebAnnotation.ReviewFeed
{
    /// <summary>
    /// One row in the Review change feed. Snapshotted from a poll arrival so the list
    /// does not hold live store references that can mutate under the WPF binder.
    /// </summary>
    public sealed class ReviewChangeEntry
    {
        /// <summary>
        /// Builds a feed row from a store location. Prefers volume shape (viewer space);
        /// falls back to mosaic when volume geometry is missing.
        /// </summary>
        public static ReviewChangeEntry FromLocation(LocationObj location)
        {
            if (location is null)
                throw new ArgumentNullException(nameof(location));

            StructureObj parent = location.Parent;
            string typeName = parent?.Type?.Name
                ?? parent?.Type?.Code
                ?? "Location";
            string label = parent?.Label;

            bool useVolume = location.VolumeShape != null && !location.VolumeShape.IsNull;
            var shape = useVolume ? location.VolumeShape : location.MosaicShape;
            ReviewChangeCoordinateSpace space = useVolume
                ? ReviewChangeCoordinateSpace.Volume
                : ReviewChangeCoordinateSpace.Mosaic;

            Rectangle bbox = default;
            IReadOnlyList<Vector2> ring = Array.Empty<Vector2>();
            if (shape != null && !shape.IsNull)
            {
                bbox = shape.BoundingBox();
                try
                {
                    ring = shape.ToPoints() ?? Array.Empty<Vector2>();
                }
                catch (Exception)
                {
                    ring = Array.Empty<Vector2>();
                }
            }

            string username = location.Username;
            if (string.IsNullOrWhiteSpace(username))
                username = Viking.UI.State.UserCredentials?.UserName;

            double centerX = useVolume ? location.VolumePosition.X : location.Position.X;
            double centerY = useVolume ? location.VolumePosition.Y : location.Position.Y;

            return new ReviewChangeEntry(
                location.ID,
                location.ParentID,
                label,
                typeName,
                location.Section,
                location.LastModified,
                username,
                centerX,
                centerY,
                bbox.Left,
                bbox.Bottom,
                bbox.Right,
                bbox.Top,
                CopyRing(ring),
                space,
                isDeleted: false);
        }

        /// <summary>
        /// Marks a location that disappeared in a poll delete set.
        /// </summary>
        public static ReviewChangeEntry Deleted(long locationId, DateTime deletedAtUtc) =>
            new(
                locationId,
                structureId: null,
                structureLabel: null,
                typeName: "Deleted",
                section: 0,
                lastModifiedUtc: deletedAtUtc,
                username: null,
                centerX: 0,
                centerY: 0,
                minX: 0,
                minY: 0,
                maxX: 0,
                maxY: 0,
                shapeRing: Array.Empty<Vector2>(),
                coordinateSpace: ReviewChangeCoordinateSpace.Volume,
                isDeleted: true);

        public ReviewChangeEntry(
            long locationId,
            long? structureId,
            string structureLabel,
            string typeName,
            int section,
            DateTime lastModifiedUtc,
            string username,
            double centerX,
            double centerY,
            double minX,
            double minY,
            double maxX,
            double maxY,
            IReadOnlyList<Vector2> shapeRing,
            ReviewChangeCoordinateSpace coordinateSpace,
            bool isDeleted)
        {
            LocationId = locationId;
            StructureId = structureId;
            StructureLabel = structureLabel;
            TypeName = typeName ?? "Location";
            Section = section;
            LastModifiedUtc = lastModifiedUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(lastModifiedUtc, DateTimeKind.Utc)
                : lastModifiedUtc.ToUniversalTime();
            Username = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
            CenterX = centerX;
            CenterY = centerY;
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            ShapeRing = shapeRing ?? Array.Empty<Vector2>();
            CoordinateSpace = coordinateSpace;
            IsDeleted = isDeleted;
        }

        /// <summary>Legacy name for <see cref="ShapeRing"/> (mask exterior in the entry's space).</summary>
        public IReadOnlyList<Vector2> MosaicRing => ShapeRing;

        public long LocationId { get; }
        public long? StructureId { get; }
        public string StructureLabel { get; }
        public string TypeName { get; }
        public int Section { get; }
        public DateTime LastModifiedUtc { get; }

        /// <summary>Last user to edit the location (from the annotation store), when known.</summary>
        public string Username { get; }

        public double CenterX { get; }
        public double CenterY { get; }
        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }

        /// <summary>Exterior ring in <see cref="CoordinateSpace"/> used to paint the gallery-style mask.</summary>
        public IReadOnlyList<Vector2> ShapeRing { get; }

        /// <summary>Volume (preferred) or mosaic space for bbox, ring, and EM crop.</summary>
        public ReviewChangeCoordinateSpace CoordinateSpace { get; }

        public bool IsDeleted { get; }

        /// <summary>Local date and time for the card header, including year (e.g. 10/08/2026 14:32).</summary>
        public string TimeLabel
        {
            get
            {
                DateTime local = LastModifiedUtc.ToLocalTime();
                return local.ToString("MM/dd/yyyy HH:mm");
            }
        }

        /// <summary>Username line for non-deleted cards; empty when unknown.</summary>
        public string UsernameLabel => string.IsNullOrWhiteSpace(Username) ? "" : Username;

        /// <summary>Caption for the card (id, Z, type, structure). Username is shown separately.</summary>
        public string Caption
        {
            get
            {
                if (IsDeleted)
                    return $"#{LocationId} · deleted";

                string structure = StructureId.HasValue
                    ? $"{TypeName} {StructureId.Value}"
                    : TypeName;
                string label = string.IsNullOrWhiteSpace(StructureLabel)
                    ? ""
                    : $" · {StructureLabel}";
                return $"#{LocationId} · Z {Section} · {structure}{label}";
            }
        }

        /// <summary>Single-line deleted row: time · id · deleted · optional user.</summary>
        public string DeletedRowText
        {
            get
            {
                string user = string.IsNullOrWhiteSpace(Username) ? "" : $" · {Username}";
                return $"{TimeLabel} · #{LocationId} · deleted{user}";
            }
        }

        public double BBoxWidth => Math.Max(0, MaxX - MinX);
        public double BBoxHeight => Math.Max(0, MaxY - MinY);

        static Vector2[] CopyRing(IReadOnlyList<Vector2> ring)
        {
            if (ring is null || ring.Count == 0)
                return Array.Empty<Vector2>();
            var copy = new Vector2[ring.Count];
            for (int i = 0; i < ring.Count; i++)
                copy[i] = ring[i];
            return copy;
        }
    }
}
