using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.UI.Controls;
using Viking.VolumeModel;
using WebAnnotationModel;

namespace WebAnnotation
{
    /// <summary>
    /// One section the volume-position job should correct. Captured on the UI thread before the background task starts.
    /// </summary>
    internal readonly struct VolumePositionSection
    {
        public int Number { get; }
        public string? DefaultChannel { get; }
        public string? DefaultPyramidTransform { get; }

        public VolumePositionSection(int number, string? defaultChannel, string? defaultPyramidTransform)
        {
            Number = number;
            DefaultChannel = defaultChannel;
            DefaultPyramidTransform = defaultPyramidTransform;
        }
    }

    /// <summary>How many locations on one section were saved with a new volume shape.</summary>
    internal readonly struct SectionCorrectionCount
    {
        public long SectionNumber { get; }
        public int Corrections { get; }

        public SectionCorrectionCount(long sectionNumber, int corrections)
        {
            SectionNumber = sectionNumber;
            Corrections = corrections;
        }
    }

    /// <summary>
    /// Outcome of one volume-position pass. <see cref="Corrections"/> lists only sections that saved at least one location.
    /// </summary>
    internal sealed class VolumePositionUpdateResult
    {
        public bool Cancelled { get; }
        public string? Error { get; }
        public IReadOnlyList<SectionCorrectionCount> Corrections { get; }

        public VolumePositionUpdateResult(bool cancelled, string? error, IReadOnlyList<SectionCorrectionCount>? corrections)
        {
            Cancelled = cancelled;
            Error = error;
            Corrections = corrections ?? [];
        }
    }

    /// <summary>
    /// Remaps each location's mosaic shape into volume space and writes VolumeShape, matching VikingAU without the translation file.
    /// Runs on a background task. Uses a private <see cref="LocationStore"/> so the viewer's unsaved edits are not saved with the correction.
    /// Sections are processed one at a time so the status line can name the current section and its update count.
    /// A cancel discards the section in progress. Sections already saved stay saved.
    /// </summary>
    internal static class VolumePositionUpdateJob
    {
        private const double EpsilonSquared = 0.25;
        private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Called via Task.Run from <see cref="UI.VolumePositionUpdateLauncher"/>. Does not touch the UI thread.
        /// </summary>
        public static async Task<VolumePositionUpdateResult> RunAsync(
            MappingManager mappings,
            IReadOnlyList<VolumePositionSection> sections,
            string? volumeTransformName,
            IProgress<ViewerTaskProgress>? progress,
            CancellationToken cancellationToken)
        {
            var corrections = new List<SectionCorrectionCount>();
            LocationStore locations = new();
            DateTime lastProgressUtc = DateTime.MinValue;

            try
            {
                for (int index = 0; index < sections.Count; index++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return Cancelled(corrections);

                    VolumePositionSection section = sections[index];
                    int position = index + 1;
                    Report(progress, ref lastProgressUtc, index, sections.Count,
                        SectionMessage(section.Number, position, sections.Count, 0), force: true);

                    var onSection = locations.GetObjectsForSection(section.Number);
                    MappingBase? mapper = mappings.GetMapping(
                        volumeTransformName,
                        section.Number,
                        section.DefaultChannel,
                        section.DefaultPyramidTransform);
                    if (mapper is null)
                    {
                        return new VolumePositionUpdateResult(
                            cancelled: false,
                            error: $"No mapping found for section {section.Number}.",
                            corrections: corrections);
                    }

                    await mapper.Initialize(cancellationToken).ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested)
                        return Cancelled(corrections);

                    int updated = 0;
                    foreach (LocationObj location in onSection.Values)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return Cancelled(corrections);

                        try
                        {
                            if (TryUpdateVolumeShape(location, mapper))
                                updated++;
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"Location {location.ID} on section {section.Number} was skipped: {ex.Message}");
                        }

                        Report(progress, ref lastProgressUtc, index, sections.Count,
                            SectionMessage(section.Number, position, sections.Count, updated), force: false);
                    }

                    Report(progress, ref lastProgressUtc, index, sections.Count,
                        SectionMessage(section.Number, position, sections.Count, updated), force: true);

                    if (cancellationToken.IsCancellationRequested)
                        return Cancelled(corrections);

                    if (updated > 0)
                    {
                        try
                        {
                            if (!locations.Save())
                            {
                                return new VolumePositionUpdateResult(
                                    cancelled: false,
                                    error: $"Failed to save section {section.Number}.",
                                    corrections: corrections);
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"Failed to save section {section.Number}: {ex}");
                            return new VolumePositionUpdateResult(
                                cancelled: false,
                                error: $"Section {section.Number} failed to save: {ex.Message}",
                                corrections: corrections);
                        }

                        corrections.Add(new SectionCorrectionCount(section.Number, updated));
                    }

                    locations.RemoveSection(section.Number);
                    mappings.SectionMappingCache.Remove(section.Number);
                    Report(progress, ref lastProgressUtc, position, sections.Count,
                        SectionMessage(section.Number, position, sections.Count, updated), force: true);
                }
            }
            catch (OperationCanceledException)
            {
                return Cancelled(corrections);
            }

            if (cancellationToken.IsCancellationRequested)
                return Cancelled(corrections);

            Report(progress, ref lastProgressUtc, sections.Count, sections.Count,
                sections.Count == 0 ? "No sections to update" : $"Finished {sections.Count} sections", force: true);

            return new VolumePositionUpdateResult(cancelled: false, error: null, corrections: corrections);
        }

        private static VolumePositionUpdateResult Cancelled(List<SectionCorrectionCount> corrections)
            => new(cancelled: true, error: null, corrections: corrections);

        private static string SectionMessage(int sectionNumber, int position, int count, int updated)
            => $"Section {sectionNumber} ({position}/{count}) — {updated} updated";

        private static void Report(
            IProgress<ViewerTaskProgress>? progress,
            ref DateTime lastProgressUtc,
            int completed,
            int total,
            string message,
            bool force)
        {
            if (progress is null)
                return;

            DateTime now = DateTime.UtcNow;
            if (!force && now - lastProgressUtc < ProgressInterval)
                return;

            lastProgressUtc = now;
            progress.Report(new ViewerTaskProgress(completed, total, message));
        }

        /// <summary>
        /// Returns true when the location should be saved. Same shape test as VikingAU: control points farther than ε² = 0.25, a geometry-type change, or a repaired location type.
        /// </summary>
        private static bool TryUpdateVolumeShape(LocationObj location, MappingBase mapper)
        {
            if (location.MosaicShape is null)
                return false;

            bool typeUpdated = false;
            if (!IsLocationTypeValid(location))
            {
                if (TryRepairLocationType(location))
                {
                    Trace.WriteLine($"Repaired type for location {location.ID}");
                    typeUpdated = true;
                }
                else
                {
                    Trace.WriteLine($"Unable to repair type for location {location.ID}");
                }
            }

            SqlGeometry? updatedVolumeShape = VolumeShapeForLocation(location, mapper);
            if (updatedVolumeShape is null)
            {
                Trace.WriteLine("Could not map location ID : " + location.ID);
                return false;
            }

            if (!updatedVolumeShape.STIsValid().IsTrue)
            {
                Trace.WriteLine($"Location {location.ID} invalid : {updatedVolumeShape.IsValidDetailed()} ");
                return false;
            }

            SqlGeometry? original = location.VolumeShape;
            bool shapeChanged = original is null
                || !original.STIsValid().IsTrue
                || updatedVolumeShape.GeometryType() != original.GeometryType()
                || AnyPointsAreDifferent(original.ToPoints(), updatedVolumeShape.ToPoints());

            if (shapeChanged)
            {
                location.VolumeShape = updatedVolumeShape;
                return true;
            }

            return typeUpdated;
        }

        private static SqlGeometry? VolumeShapeForLocation(LocationObj location, MappingBase mapper)
        {
            SqlGeometry? unsmoothed = mapper.TryMapShapeSectionToVolume(location.MosaicShape);
            if (unsmoothed is null)
                return null;

            return location.TypeCode.GetSmoothedShape(unsmoothed);
        }

        private static bool IsLocationTypeValid(LocationObj location)
        {
            switch (location.MosaicShape.GeometryType())
            {
                case SupportedGeometryType.POINT:
                    return location.TypeCode == LocationType.POINT;
                case SupportedGeometryType.CURVEPOLYGON:
                    return location.TypeCode == LocationType.CIRCLE;
                case SupportedGeometryType.POLYLINE:
                    return location.TypeCode == LocationType.POLYLINE
                        || location.TypeCode == LocationType.OPENCURVE
                        || location.TypeCode == LocationType.CLOSEDCURVE;
                case SupportedGeometryType.POLYGON:
                    return location.TypeCode == LocationType.POLYGON
                        || location.TypeCode == LocationType.CURVEPOLYGON;
                default:
                    return false;
            }
        }

        /// <summary>Returns true when the location type or mosaic shape was changed so it matches the stored geometry.</summary>
        private static bool TryRepairLocationType(LocationObj location)
        {
            switch (location.MosaicShape.GeometryType())
            {
                case SupportedGeometryType.POINT:
                    if (location.TypeCode != LocationType.POINT)
                    {
                        location.TypeCode = LocationType.POINT;
                        return true;
                    }
                    break;
                case SupportedGeometryType.CURVEPOLYGON:
                    if (location.TypeCode != LocationType.CIRCLE)
                    {
                        location.TypeCode = LocationType.CIRCLE;
                        return true;
                    }
                    break;
                case SupportedGeometryType.POLYLINE:
                    if (location.TypeCode == LocationType.CIRCLE)
                    {
                        location.TypeCode = LocationType.POLYLINE;
                        location.Width = 8.0;
                        return true;
                    }

                    if (location.TypeCode == LocationType.POLYGON || location.TypeCode == LocationType.CURVEPOLYGON)
                    {
                        SqlGeometry newShape = location.MosaicShape.ToPoints().ToPolygon();
                        if (newShape.STIsValid().IsTrue)
                        {
                            location.MosaicShape = newShape;
                            location.Width = null;
                            return true;
                        }

                        return false;
                    }
                    break;
                case SupportedGeometryType.POLYGON:
                    if (location.TypeCode == LocationType.CLOSEDCURVE || location.TypeCode == LocationType.POLYLINE)
                    {
                        SqlGeometry newShape = location.MosaicShape.ToPoints().ToSqlGeometry();
                        if (newShape.STIsValid().IsTrue)
                        {
                            location.MosaicShape = newShape;
                            location.Width = 8;
                            return true;
                        }

                        return false;
                    }
                    break;
            }

            return false;
        }

        private static bool AnyPointsAreDifferent(Vector2[] original, Vector2[] updated)
        {
            if (original.Length != updated.Length)
                return true;

            for (int i = 0; i < updated.Length; i++)
            {
                if (Vector2.DistanceSquared(original[i], updated[i]) > EpsilonSquared)
                    return true;
            }

            return false;
        }
    }
}
