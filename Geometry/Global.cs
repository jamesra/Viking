using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    public class Global
    {
        public const float Epsilon = 0.001f;

        /// <summary>
        /// Value used to round values.  Currently used to ensure points hash to the same value if they are
        /// within an epsilon distance
        /// </summary>
        public const int SignificantDigits = 3;

        public const float EpsilonSquared = Global.Epsilon * Global.Epsilon;

        public static bool TryUseNativeMKL()
        {
            bool loaded = false;
            try
            {
                loaded = MathNet.Numerics.Control.TryUseNativeMKL();
                System.Diagnostics.Trace.WriteLine($"\n\nGeometry: Native MKL library {(loaded ? "" : "not")} found");
                System.Diagnostics.Trace.WriteLine($"Geometry: Mathnet.Numerics:\n{MathNet.Numerics.Control.Describe()}\n\n");
                return loaded;
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("Geometry: Unable to load Native MKL library.  Exception text:\n" + e.Message);
                return false;
            }

            
        }

        public static bool IsCacheFileValid(string CacheStosPath, DateTime time)
        {
            return IsCacheFileValid(CacheStosPath, new DateTime[] { time });
        }

        public static bool IsCacheFileValid(string CacheStosPath, ICollection<DateTime> times)
        {
            if (System.IO.File.Exists(CacheStosPath))
            {
                DateTime CacheLastModifiedUtc = System.IO.File.GetLastWriteTimeUtc(CacheStosPath);
                return times.All(server_transform_time => server_transform_time <= CacheLastModifiedUtc);
            }

            return false;
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
                catch (System.IO.IOException e)
                {
                    System.Diagnostics.Trace.WriteLine("Unable to delete cache file " + FilePath);
                }
            }

            return false;
        }

        public static uint NumCurveInterpolationPoints(bool Closed)
        {
            return Closed ? NumClosedCurveInterpolationPoints : NumOpenCurveInterpolationPoints;
        }

        //TODO: Choose number of points based on distance between control points
        static public readonly uint NumOpenCurveInterpolationPoints = 3;
        static public readonly uint NumClosedCurveInterpolationPoints = 8;
    }
}
