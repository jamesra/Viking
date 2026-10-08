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

        public static bool TryUseNativeMKL() => TryUseNativeMKL(out _);

        /// <summary>
        /// Selects the native MKL linear algebra provider (OpenBLAS when MKL throws or off Windows/Linux)
        /// and returns a multi-line report for the caller's startup log.
        /// The report names the MathNet assembly versions, the native library actually on disk,
        /// which provider ended up active, and the exception text when a load failed.
        /// It is also written with Trace, but Release builds strip Trace, so the caller must write
        /// <paramref name="report"/> itself for the line to survive in a Release log.
        /// A false result means MathNet stays on its managed provider, which is much slower for the RBF fallback solves.
        /// </summary>
        public static bool TryUseNativeMKL(out string report)
        {
            System.Text.StringBuilder lines = new();
            lines.AppendLine(DescribeMathNetVersions());

            bool loaded = false;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    loaded = MathNet.Numerics.Control.TryUseNativeMKL();
                    lines.AppendLine($"Geometry: Native MKL library {(loaded ? "loaded" : "NOT loaded (TryUseNativeMKL returned false)")}");
                }
                catch (Exception e)
                {
                    lines.AppendLine($"Geometry: Unable to load Native MKL library: {e.GetType().Name}: {e.Message}");
                    try
                    {
                        loaded = MathNet.Numerics.Control.TryUseNativeOpenBLAS();
                        lines.AppendLine($"Geometry: Native OpenBLAS library {(loaded ? "loaded" : "NOT loaded (TryUseNativeOpenBLAS returned false)")}");
                    }
                    catch (Exception openBlasException)
                    {
                        lines.AppendLine($"Geometry: Unable to load Native OpenBLAS library: {openBlasException.GetType().Name}: {openBlasException.Message}");
                    }
                }
            }
            else
            {
                try
                {
                    loaded = MathNet.Numerics.Control.TryUseNativeOpenBLAS();
                    lines.AppendLine($"Geometry: Native OpenBLAS library {(loaded ? "loaded" : "NOT loaded (TryUseNativeOpenBLAS returned false)")}");
                }
                catch (Exception e)
                {
                    lines.AppendLine($"Geometry: Unable to load Native OpenBLAS library: {e.GetType().Name}: {e.Message}");
                }
            }

            lines.AppendLine($"Geometry: Mathnet.Numerics:{Environment.NewLine}{MathNet.Numerics.Control.Describe()}");
            report = lines.ToString();
            System.Diagnostics.Trace.WriteLine(report);
            return loaded;
        }

        /// <summary>
        /// Assembly versions of MathNet and its providers plus the native MKL/OpenMP files found beside the app.
        /// Reads file metadata only; it does not load the native libraries, so it is safe to call before provider selection.
        /// </summary>
        private static string DescribeMathNetVersions()
        {
            System.Text.StringBuilder text = new();
            text.Append("Geometry: MathNet assemblies: ");
            text.Append(DescribeAssembly(typeof(MathNet.Numerics.Control).Assembly));
            text.Append("; ");
            text.Append(DescribeAssembly(typeof(MathNet.Numerics.Providers.MKL.MklProvider).Assembly));
            text.Append("; ");
            text.Append(DescribeAssembly(typeof(MathNet.Numerics.Providers.OpenBLAS.OpenBlasProvider).Assembly));

            // MathNet probes next to the provider assembly, not the process base directory (they differ when a host loads Viking.exe by path).
            string baseDirectory = System.IO.Path.GetDirectoryName(typeof(MathNet.Numerics.Providers.MKL.MklProvider).Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            foreach (string relativePath in new[]
            {
                System.IO.Path.Combine("runtimes", "win-x64", "native", "libMathNetNumericsMKL.dll"),
                System.IO.Path.Combine("runtimes", "win-x64", "native", "libiomp5md.dll"),
                System.IO.Path.Combine("x64", "MathNet.Numerics.MKL.dll"),
            })
            {
                string fullPath = System.IO.Path.Combine(baseDirectory, relativePath);
                text.AppendLine();
                text.Append("Geometry: native file ").Append(relativePath).Append(": ");
                try
                {
                    if (!File.Exists(fullPath))
                    {
                        text.Append("MISSING");
                        continue;
                    }

                    System.Diagnostics.FileVersionInfo version = System.Diagnostics.FileVersionInfo.GetVersionInfo(fullPath);
                    text.Append($"{new FileInfo(fullPath).Length:N0} bytes, file version {version.FileVersion ?? "unknown"}");
                }
                catch (Exception e)
                {
                    text.Append($"unreadable ({e.Message})");
                }
            }

            return text.ToString();
        }

        private static string DescribeAssembly(System.Reflection.Assembly assembly)
        {
            System.Reflection.AssemblyName name = assembly.GetName();
            string informational = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?.InformationalVersion;
            return $"{name.Name} {informational ?? name.Version.ToString()}";
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
