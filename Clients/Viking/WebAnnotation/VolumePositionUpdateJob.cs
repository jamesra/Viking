using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.UI.Controls;
using Viking.VolumeModel;
using Microsoft.Extensions.DependencyInjection;
using WebAnnotationModel;
using WebAnnotationModel.gRPC;
using WebAnnotationModel.ServerInterface;
using WebAnnotationModel.Objects;

namespace WebAnnotation
{
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
    /// Update Volume Positions: the refresh of the stored volume cache after a section's alignment (.stos) changes. Writes each
    /// location's <c>VolumeShape</c> as <see cref="VolumeCache.Shape"/> under the volume's default transform, the value every
    /// client save writes (circles as the owner's exact circle), so the cache that server queries, OData and export read
    /// matches the new alignment. Mosaic shapes are not changed, except to repair a location whose type does not match its
    /// shape. VikingAU does the same without the viewer (plus its translation file). See
    /// <c>.cursor/rules/annotation-volume-geometry.mdc</c>.
    /// </summary>
    /// <remarks>
    /// Runs on a background task. Reads and writes each section through its own gRPC <see cref="ILocationsClient"/>, not the
    /// viewer's store, so the viewer's unsaved edits are not saved with the correction. Sections are processed one at a time so
    /// the status line can name the current section and its update count. A cancel discards the section in progress. Sections
    /// already saved stay saved.
    /// </remarks>
    internal static class VolumePositionUpdateJob
    {
        private const double EpsilonSquared = 0.25;
        private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Called on a worker thread (Task.Run) from <see cref="UI.VolumePositionUpdateLauncher"/>. Does not touch the UI thread.
        /// </summary>
        /// <param name="transforms">The open volume's default transforms (<see cref="IDefaultVolumeTransformProvider"/>).</param>
        /// <param name="sections">The section numbers to refresh.</param>
        public static async Task<VolumePositionUpdateResult> RunAsync(
            IDefaultVolumeTransformProvider transforms,
            IReadOnlyList<int> sections,
            IProgress<ViewerTaskProgress>? progress,
            CancellationToken cancellationToken)
        {
            var corrections = new List<SectionCorrectionCount>();
            DateTime lastProgressUtc = DateTime.MinValue;

            IServiceProvider? services = AnnotationBootstrap.Services;
            ILocationsClient? client = services?.GetService<IServerAnnotationsClientFactory<ILocationsClient>>()?.GetOrCreate();
            IObjectConverter<ILocation, LocationObj>? toClient = services?.GetService<IObjectConverter<ILocation, LocationObj>>();
            IObjectConverter<LocationObj, ILocation>? toServer = services?.GetService<IObjectConverter<LocationObj, ILocation>>();
            if (client is null || toClient is null || toServer is null)
                return new VolumePositionUpdateResult(cancelled: false, error: "The annotation service is not connected.", corrections: corrections);

            try
            {
                for (int index = 0; index < sections.Count; index++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return Cancelled(corrections);

                    int sectionNumber = sections[index];
                    int position = index + 1;
                    Report(progress, ref lastProgressUtc, index, sections.Count,
                        SectionMessage(sectionNumber, position, sections.Count, 0), force: true);

                    ServerUpdate<long, ILocation[]> sectionLocations =
                        await client.GetAsync(sectionNumber, null, cancellationToken).ConfigureAwait(false);
                    List<LocationObj> onSection = (sectionLocations.NewOrUpdated ?? Array.Empty<ILocation>())
                        .Select(toClient.Convert)
                        .ToList();
                    if (transforms.GetDefaultSectionToVolumeTransform(sectionNumber) is not IVolumeToSectionTransform defaultTransform)
                    {
                        return new VolumePositionUpdateResult(
                            cancelled: false,
                            error: $"The volume's default transform for section {sectionNumber} is not loaded, so volume positions were not updated from that section on. Reopen the volume once it loads.",
                            corrections: corrections);
                    }

                    int updated = 0;
                    List<LocationObj> changed = [];
                    foreach (LocationObj location in onSection)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return Cancelled(corrections);

                        try
                        {
                            if (TryUpdateVolumeShape(location, defaultTransform))
                            {
                                location.DBAction = DBACTION.UPDATE;
                                changed.Add(location);
                                updated++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"Location {location.ID} on section {sectionNumber} was skipped: {ex.Message}");
                        }

                        Report(progress, ref lastProgressUtc, index, sections.Count,
                            SectionMessage(sectionNumber, position, sections.Count, updated), force: false);
                    }

                    Report(progress, ref lastProgressUtc, index, sections.Count,
                        SectionMessage(sectionNumber, position, sections.Count, updated), force: true);

                    if (cancellationToken.IsCancellationRequested)
                        return Cancelled(corrections);

                    if (updated > 0)
                    {
                        try
                        {
                            await client.UpdateAsync(changed.Select(toServer.Convert).ToArray(), cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"Failed to save section {sectionNumber}: {ex}");
                            return new VolumePositionUpdateResult(
                                cancelled: false,
                                error: $"Section {sectionNumber} failed to save: {ex.Message}",
                                corrections: corrections);
                        }

                        corrections.Add(new SectionCorrectionCount(sectionNumber, updated));
                    }

                    Report(progress, ref lastProgressUtc, position, sections.Count,
                        SectionMessage(sectionNumber, position, sections.Count, updated), force: true);
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
        /// Sets the location's <c>VolumeShape</c> to <see cref="VolumeCache.Shape"/> under <paramref name="defaultTransform"/>
        /// and returns true when the location should be saved. Same shape test as VikingAU: control points farther than
        /// ε² = 0.25, a geometry-type change, or a repaired location type.
        /// </summary>
        /// <exception cref="ArgumentException">Even the owner's mapping-failure policy cannot place the location.</exception>
        internal static bool TryUpdateVolumeShape(LocationObj location, IVolumeToSectionTransform defaultTransform)
        {
            if (location.MosaicShape is null)
                return false;

            SqlGeometry? original = location.VolumeShape is null ? null : LocationVolumeGeometry.ToSqlGeometry(location.VolumeShape);
            bool typeUpdated = false;
            if (!IsLocationTypeValid(location))
            {
                if (TryRepairLocationType(location, defaultTransform))
                {
                    Trace.WriteLine($"Repaired type for location {location.ID}");
                    typeUpdated = true;
                }
                else
                {
                    Trace.WriteLine($"Unable to repair type for location {location.ID}");
                }
            }

            // The value every client save writes: the forward owner's cache shape under the default transform.
            IShape2D cache = VolumeCache.Shape(location, defaultTransform);
            SqlGeometry updatedVolumeShape = LocationVolumeGeometry.ToSqlGeometry(cache);
            if (!updatedVolumeShape.STIsValid().IsTrue)
            {
                Trace.WriteLine($"Location {location.ID} invalid : {updatedVolumeShape.IsValidDetailed()} ");
                return false;
            }

            bool shapeChanged = original is null
                || !original.STIsValid().IsTrue
                || updatedVolumeShape.GeometryType() != original.GeometryType()
                || AnyPointsAreDifferent(original.ToPoints(), updatedVolumeShape.ToPoints());

            if (shapeChanged)
            {
                location.VolumeShape = cache;
                return true;
            }

            return typeUpdated;
        }

        private static bool IsLocationTypeValid(LocationObj location)
        {
            switch (LocationVolumeGeometry.ToSqlGeometry(location.MosaicShape).GeometryType())
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

        /// <summary>
        /// Returns true when the location type or mosaic shape was changed so it matches the stored geometry. A new mosaic
        /// shape is saved through the owner (<see cref="TrySetRepairedShape"/>).
        /// </summary>
        private static bool TryRepairLocationType(LocationObj location, IVolumeToSectionTransform defaultTransform)
        {
            switch (LocationVolumeGeometry.ToSqlGeometry(location.MosaicShape).GeometryType())
            {
                case SupportedGeometryType.POINT:
                    if (location.TypeCode != LocationType.POINT)
                    {
                        location.TypeCode = LocationType.POINT;
                        // The database requires a null width on types without one (chk_Location_Width).
                        location.Width = null;
                        return true;
                    }
                    break;
                case SupportedGeometryType.CURVEPOLYGON:
                    if (location.TypeCode != LocationType.CIRCLE)
                    {
                        location.TypeCode = LocationType.CIRCLE;
                        location.Width = null;
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
                        SqlGeometry newShape = LocationVolumeGeometry.ToSqlGeometry(location.MosaicShape).ToPoints().ToPolygon();
                        return TrySetRepairedShape(location, defaultTransform, newShape, null);
                    }
                    break;
                case SupportedGeometryType.POLYGON:
                    if (location.TypeCode == LocationType.CLOSEDCURVE || location.TypeCode == LocationType.POLYLINE)
                    {
                        SqlGeometry newShape = LocationVolumeGeometry.ToSqlGeometry(location.MosaicShape).ToPoints().ToSqlGeometry();
                        return TrySetRepairedShape(location, defaultTransform, newShape, 8);
                    }
                    break;
            }

            return false;
        }

        /// <summary>
        /// Saves a repaired mosaic shape through the owner (<see cref="LocationObjExtensions.SetShapeFromGeometryInSection"/>)
        /// with its new <paramref name="width"/>. The width is set first because the cache is derived for it; when the shape is
        /// invalid or cannot be saved, the location keeps both its shape and its old width.
        /// </summary>
        private static bool TrySetRepairedShape(LocationObj location, IVolumeToSectionTransform defaultTransform, SqlGeometry newShape, double? width)
        {
            if (!newShape.STIsValid().IsTrue)
                return false;

            double? oldWidth = location.Width;
            location.Width = width;
            try
            {
                location.SetShapeFromGeometryInSection(defaultTransform, newShape);
                return true;
            }
            catch (ArgumentException e)
            {
                location.Width = oldWidth;
                Trace.WriteLine($"Location {location.ID}: the repaired shape could not be saved: {e.Message}");
                return false;
            }
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
