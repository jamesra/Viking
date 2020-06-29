using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Linq.Expressions;

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

        static Global()
        {
            try
            {
                MathNet.Numerics.Control.UseNativeMKL();
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("Unable to load Native MKL library.  Exception text:\n" + e.Message);
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
        static public readonly uint NumClosedCurveInterpolationPoints = 10;
    }
}
