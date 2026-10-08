using Geometry;
using Geometry.Transforms;
using System;
using System.IO;
using System.Linq;
using Path = System.IO.Path;

namespace Viking.Benchmarks.Geometry
{
    /// <summary>
    /// Loads the RC2 section 646 transforms that VikingVolumeBench prime saved: the section-to-volume stos (a grid
    /// transform onto reference section 645) and the section's mosaic tile transforms.
    /// </summary>
    /// <remarks>
    /// The root defaults to %LOCALAPPDATA%\VikingVolumeBench and can be overridden with the VIKINGBENCH_ROOT environment
    /// variable. Throws with a pointer to prime when the files are missing.
    /// </remarks>
    internal static class Rc2Data
    {
        public const int Section = 646;

        public static string Root =>
            Environment.GetEnvironmentVariable("VIKINGBENCH_ROOT") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VikingVolumeBench");

        public static ITransform LoadSectionToVolume()
        {
            string dir = Path.Combine(Root, "viking-cache", "RC2", "StosZip", "SliceToVolume1");
            string file = Directory.Exists(dir) ? Directory.GetFiles(dir, $"{Section}-645*.stos", SearchOption.AllDirectories).FirstOrDefault() : null;
            if (file is null)
                throw new FileNotFoundException($"No {Section}-645 stos under {dir}. Run VikingVolumeBench prime and one run first.");
            return TransformFactory.ParseStos(file).GetAwaiter().GetResult();
        }

        public static ITransform[] LoadMosaicTiles()
        {
            string file = Path.Combine(Root, "mirror", "RC2", "TEM", $"{Section:D4}", "TEM", "Grid_Cel128_Mes8_Mes8_Thr0.25_it10_sp4.mosaic");
            if (!File.Exists(file))
                throw new FileNotFoundException($"Missing {file}. Run VikingVolumeBench prime first.");
            return TransformFactory.LoadMosaic(Path.GetDirectoryName(file), File.ReadAllLines(file), File.GetLastWriteTimeUtc(file));
        }

        /// <summary>Deterministic points spread over <paramref name="bounds"/>, inset 5% from each edge.</summary>
        public static Vector2[] Points(Rectangle bounds, int count, int seed)
        {
            Random random = new(seed);
            Vector2[] points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                points[i] = new Vector2(bounds.Left + (bounds.Width * (0.05 + (0.9 * random.NextDouble()))),
                                        bounds.Bottom + (bounds.Height * (0.05 + (0.9 * random.NextDouble()))));
            }
            return points;
        }
    }
}
