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
                return times.Any(server_transform_time => server_transform_time <= CacheLastModifiedUtc);
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
                catch (Exception e)
                {
                    System.Diagnostics.Trace.WriteLine("Unable to delete cache file " + FilePath);
                }
            }

            return false;
        }
    }
}
