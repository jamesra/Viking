using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using Path = System.IO.Path;

namespace Viking.Benchmarks.VolumeBench
{
    /// <summary>One location as saved by prime. Shapes are WKB in base64, exactly as the annotation service returned them.</summary>
    internal sealed class SnapshotLocation
    {
        public long Id { get; set; }
        public long? ParentId { get; set; }
        public short TypeCode { get; set; }
        public double? Width { get; set; }
        public long LastModifiedTicks { get; set; }
        public double Z { get; set; }
        public string MosaicWkb { get; set; }
        public string VolumeWkb { get; set; }
    }

    /// <summary>All locations on one section, read-only, as fetched by prime.</summary>
    internal sealed class SectionSnapshot
    {
        public int Section { get; set; }
        public string FetchedUtc { get; set; }
        public string Endpoint { get; set; }
        public List<SnapshotLocation> Locations { get; set; } = [];

        public static string PathFor(BenchOptions options, int section) => Path.Combine(options.AnnotationRoot, $"{section:D4}.json");

        public static SectionSnapshot TryLoad(BenchOptions options, int section)
        {
            string path = PathFor(options, section);
            return File.Exists(path) ? JsonSerializer.Deserialize<SectionSnapshot>(File.ReadAllText(path), ResultsFile.JsonOptions) : null;
        }

        public void Save(BenchOptions options)
        {
            Directory.CreateDirectory(options.AnnotationRoot);
            File.WriteAllText(PathFor(options, Section), JsonSerializer.Serialize(this, ResultsFile.JsonOptions));
        }
    }

    internal enum ReplayStatus
    {
        Ok,
        NoMosaicShape,
        MapFailed,
        SmoothFailed,
        Invalid,
        Error,
    }

    /// <summary>A snapshot location with its shapes parsed, ready to replay.</summary>
    internal sealed class ReplayLocation
    {
        public SnapshotLocation Source;
        public LocationType Type;
        public SqlGeometry Mosaic;
        public SqlGeometry StoredVolume;

        /// <summary>Bounding box of the stored volume shape, used to pick a scene's annotations. Null without a stored shape.</summary>
        public Rectangle? StoredVolumeBounds;

        /// <summary>Radius in pixels, computed from the mosaic shape the way <c>LocationObj.CalculateRadius</c> does.</summary>
        public double Radius;
    }

    /// <summary>The outcome of replaying one location.</summary>
    internal sealed class ReplayResult
    {
        public ReplayStatus Status;
        public bool DiffersFromStored;
        public SqlGeometry Unsmoothed;
        public SqlGeometry Shape;
    }

    /// <summary>
    /// The VikingAU per-location update (Clients/VikingAU/Program.cs, UpdateVolumeShape) without the type repair and
    /// without saving: map the mosaic shape to volume space, smooth it, check it, and compare with the stored volume shape.
    /// Split into separate passes so each step can be timed with its allocations.
    /// </summary>
    internal static class AnnotationReplay
    {
        /// <summary>VikingAU's threshold for "points differ": squared distance, so 0.5 pixels.</summary>
        private const double DifferenceEpsilonSquared = 0.25;

        public static ReplayLocation Parse(SnapshotLocation s)
        {
            ReplayLocation r = new() { Source = s, Type = (LocationType)s.TypeCode };
            if (!string.IsNullOrEmpty(s.MosaicWkb))
            {
                r.Mosaic = Convert.FromBase64String(s.MosaicWkb).ToSqlGeometry();
                r.Radius = CalculateRadius(r.Mosaic, s.Width);
            }

            if (!string.IsNullOrEmpty(s.VolumeWkb))
            {
                r.StoredVolume = Convert.FromBase64String(s.VolumeWkb).ToSqlGeometry();
                r.StoredVolumeBounds = r.StoredVolume.BoundingBox();
            }

            return r;
        }

        private static double CalculateRadius(SqlGeometry shape, double? width)
        {
            int dimension = shape.STDimension().Value;
            return dimension switch
            {
                0 => 8,
                1 => shape.STLength().Value / 2.0,
                2 => Math.Sqrt(shape.STArea().Value / Math.PI),
                _ => (width ?? 1.0) / 2.0,
            };
        }

        public static void Map(IVolumeToSectionTransform mapper, ReplayLocation loc, ReplayResult result)
        {
            if (loc.Mosaic is null)
            {
                result.Status = ReplayStatus.NoMosaicShape;
                return;
            }

            try
            {
                result.Unsmoothed = mapper.TryMapShapeSectionToVolume(loc.Mosaic);
                result.Status = result.Unsmoothed is null ? ReplayStatus.MapFailed : ReplayStatus.Ok;
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or FormatException)
            {
                result.Status = ReplayStatus.Error;
            }
        }

        public static void Smooth(ReplayLocation loc, ReplayResult result)
        {
            if (result.Status != ReplayStatus.Ok)
                return;

            try
            {
                result.Shape = loc.Type.GetSmoothedShape(result.Unsmoothed);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or FormatException)
            {
                result.Status = ReplayStatus.SmoothFailed;
            }
        }

        public static void Check(ReplayLocation loc, ReplayResult result)
        {
            if (result.Status != ReplayStatus.Ok)
                return;

            try
            {
                if (!result.Shape.STIsValid().IsTrue)
                {
                    result.Status = ReplayStatus.Invalid;
                    return;
                }

                if (loc.StoredVolume is null)
                {
                    result.DiffersFromStored = true;
                    return;
                }

                Vector2[] original = loc.StoredVolume.ToPoints();
                Vector2[] updated = result.Shape.ToPoints();
                result.DiffersFromStored = AnyPointsAreDifferent(original, updated) ||
                                           result.Shape.GeometryType() != loc.StoredVolume.GeometryType();
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or FormatException)
            {
                result.Status = ReplayStatus.Error;
            }
        }

        private static bool AnyPointsAreDifferent(Vector2[] original, Vector2[] updated)
        {
            if (original.Length != updated.Length)
                return true;

            for (int i = 0; i < updated.Length; i++)
            {
                if (Vector2.DistanceSquared(original[i], updated[i]) > DifferenceEpsilonSquared)
                    return true;
            }

            return false;
        }

        public static AnnotationOutcome ToOutcome(ReplayResult result)
        {
            AnnotationOutcome o = new() { Status = result.Status.ToString(), DiffersFromStored = result.DiffersFromStored };
            if (result.Shape is null || result.Shape.IsNull)
                return o;

            Vector2[] points = result.Shape.ToPoints();
            o.Points = points.Length;
            if (points.Length == 0)
                return o;

            o.CenterX = points.Average(p => p.X);
            o.CenterY = points.Average(p => p.Y);
            o.MinX = points.Min(p => p.X);
            o.MinY = points.Min(p => p.Y);
            o.MaxX = points.Max(p => p.X);
            o.MaxY = points.Max(p => p.Y);
            return o;
        }
    }
}
