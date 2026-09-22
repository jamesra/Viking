using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Geometry
{
    public class Global
    {
        public const double Epsilon = Tolerance.Epsilon;

        /// <summary>
        /// Value used to round values.  Currently used to ensure points hash to the same value if they are
        /// within an epsilon distance.
        /// </summary>
        public const int SignificantDigits = Tolerance.SignificantDigits;

        /// <summary>
        /// Transformed points are rounded to a set number of significant digits.  This prevents floating point
        /// precision errors from causing errors in various geometric tests.
        /// </summary>
        public const int TransformSignificantDigits = Tolerance.TransformSignificantDigits;

        public const double EpsilonSquared = Tolerance.EpsilonSquared;

        public static readonly Random Random = new();

        public static int GetRandomRequestDelay() => Random.Next(800, 1200);

        public static bool TryUseNativeMKL()
        {
            bool loaded = false;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    loaded = MathNet.Numerics.Control.TryUseNativeMKL();
                    System.Diagnostics.Trace.WriteLine($"\n\nGeometry: Native MKL library {(loaded ? "" : "not")} found");
                    System.Diagnostics.Trace.WriteLine($"Geometry: Mathnet.Numerics:\n{MathNet.Numerics.Control.Describe()}\n\n");
                    return loaded;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Trace.WriteLine($"Geometry: Unable to load Native MKL library. {e.Message}");
                    loaded = MathNet.Numerics.Control.TryUseNativeOpenBLAS();
                    System.Diagnostics.Trace.WriteLine($"\n\nGeometry: Native OpenBLAS library {(loaded ? "" : "not")} found");
                    System.Diagnostics.Trace.WriteLine($"Geometry: Mathnet.Numerics:\n{MathNet.Numerics.Control.Describe()}\n\n");
                    return loaded;
                }
            }
            else
            {
                try
                {
                    loaded = MathNet.Numerics.Control.TryUseNativeOpenBLAS();
                    System.Diagnostics.Trace.WriteLine($"\n\nGeometry: Native OpenBLAS library {(loaded ? "" : "not")} found");
                    System.Diagnostics.Trace.WriteLine($"Geometry: Mathnet.Numerics:\n{MathNet.Numerics.Control.Describe()}\n\n");
                    return loaded;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Trace.WriteLine($"Geometry: Unable to load Native OpenBLAS library. {e.Message}");
                    return false;
                }
            }

            return false;
        }

        public static bool IsCacheFileValid(string CacheStosPath, DateTime time) => IsCacheFileValid(CacheStosPath, [time]);

        public static bool IsCacheFileValid(string CacheStosPath, ICollection<DateTime> times)
        {
            FileInfo fInfo = new(CacheStosPath);
            if (false == fInfo.Exists)
                return false;

            DateTime CacheLastModifiedUtc = fInfo.LastWriteTimeUtc;
            return times.All(server_transform_time => server_transform_time <= CacheLastModifiedUtc);
        }

        public static bool TryDeleteCacheFile(string FilePath)
        {
            if (System.IO.File.Exists(FilePath))
            {
                try
                {
                    System.IO.File.Delete(FilePath);
                    return true;
                }
                catch (System.IO.IOException)
                {
                    System.Diagnostics.Trace.WriteLine("Unable to delete cache file " + FilePath);
                }
            }

            return false;
        }

        public static uint NumCurveInterpolationPoints(bool Closed) => Closed ? NumClosedCurveInterpolationPoints : NumOpenCurveInterpolationPoints;

        //TODO: Choose number of points based on distance between control points
        public static readonly uint NumOpenCurveInterpolationPoints = 3;
        public static readonly uint NumClosedCurveInterpolationPoints = 5;
    }
}
