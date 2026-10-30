using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace WebAnnotationTests.Tools
{
    /// <summary>
    /// Views, view models, commands and actions (<c>View/</c>, <c>ViewModel/</c>, <c>UI/</c>) never map between mosaic and
    /// volume space themselves and never read the stored volume cache. Drawing and previews take their volume geometry from
    /// the forward owner, <c>LocationVolumeGeometry</c> (<c>For</c>, or its static <c>TryMapPoints</c>/<c>TryMapPolygon</c>/
    /// <c>TryMapCircle</c>/<c>TryMapControlShape</c>/<c>MapShape</c> for geometry that is not a saved location's), and every
    /// edit is mapped back by the inverse owner (<c>LocationVolumeGeometry.ToMosaic</c>, <c>TryMapPointsToMosaic</c>,
    /// <c>TryMapPolygonToMosaic</c>, <c>TryMapPolylineToMosaic</c>). So no <c>SectionToVolume(</c> or <c>VolumeToSection(</c>
    /// in any form (<c>Try…</c>, <c>TryMapShape…</c>), and no <c>.VolumePosition</c> or <c>.VolumeShape</c> read. And no code
    /// but the owners (<c>LocationObjExtensions.SetShapeFromGeometryInSection</c>, which <c>TryApply</c> and
    /// <c>SetShapeFromPointsInVolume</c> save through) writes a <c>MosaicShape</c>, so the stored volume cache is always
    /// derived again. See <c>.cursor/rules/annotation-volume-geometry.mdc</c>.
    /// </summary>
    [TestClass]
    public class VolumeGeometryGuardTests
    {
        private static readonly Regex Forbidden = new(@"SectionToVolume\(|VolumeToSection\(|\.VolumePosition\b|\.VolumeShape\b", RegexOptions.Compiled);

        /// <summary>An assignment to a location's <c>MosaicShape</c> (member access or object initializer), not a comparison.</summary>
        private static readonly Regex MosaicShapeWrite = new(@"(?:\.|[{,]\s*)MosaicShape\s*=(?!=)", RegexOptions.Compiled);

        /// <summary>
        /// The client code checked for <see cref="MosaicShapeWrite"/>, relative to the repository root, and the one file there
        /// that may write it: the owner.
        /// </summary>
        private static readonly string[] MosaicShapeWriteRoots = ["Clients/Viking/WebAnnotation", "Clients/VikingAU", "SqlGeometryAnnotationExtensions"];

        private const string MosaicShapeOwner = "SqlGeometryAnnotationExtensions/LocationObjExtensions.cs";

        private static readonly Regex InlineBlockComment = new(@"/\*.*?\*/", RegexOptions.Compiled);

        /// <summary>The WebAnnotation folders checked: everything that draws, hit-tests or edits annotations.</summary>
        private static readonly string[] Folders = ["View", "ViewModel", "UI"];

        /// <summary>
        /// The justified, read-only exceptions, as (file under WebAnnotation, text on the line, why). None of them maps an
        /// edit or draws from the cache.
        /// </summary>
        private static readonly (string File, string Line, string Why)[] AllowList =
        [
            ("ViewModel/Location_ViewModelBase.cs", "VolumeX => modelObj.VolumePosition.X", "the VolumeX property column shows the stored cache itself"),
            ("ViewModel/Location_ViewModelBase.cs", "VolumeY => modelObj.VolumePosition.Y", "the VolumeY property column shows the stored cache itself"),
            ("ViewModel/Structure.cs", "VolumePosition", "Structure.Center picks the location nearest the structure's weighted mean for navigation; not drawn"),
            ("UI/AutoPolygonize/AutoPolygonizeSelection.cs", "nameof(LocationObj.VolumeShape)", "IsGeometryProperty names the property in a change filter; it reads nothing"),
        ];

        private static string WebAnnotationDirectory([CallerFilePath] string thisFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "WebAnnotation"));

        /// <summary>
        /// The code lines of a file, with line comments, XML doc lines and <c>/* */</c> blocks (commented-out code) blanked,
        /// numbered from 1.
        /// </summary>
        private static IEnumerable<(int Number, string Code)> CodeLines(string path)
        {
            bool inBlock = false;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string code = lines[i].Trim();
                if (inBlock)
                {
                    int end = code.IndexOf("*/", StringComparison.Ordinal);
                    if (end < 0)
                        continue;

                    inBlock = false;
                    code = code.Substring(end + 2).Trim();
                }

                code = InlineBlockComment.Replace(code, string.Empty).Trim();
                int start = code.IndexOf("/*", StringComparison.Ordinal);
                if (start >= 0)
                {
                    inBlock = true;
                    code = code.Substring(0, start).Trim();
                }

                if (code.Length == 0 || code.StartsWith("//") || code.StartsWith("*"))
                    continue;

                yield return (i + 1, code);
            }
        }

        [TestMethod]
        public void ViewsViewModelsAndCommandsNeverMapOrReadTheStoredVolumeCache()
        {
            string root = WebAnnotationDirectory();
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "View")), "WebAnnotation sources not found at " + root);

            List<string> violations = [];
            foreach (string folder in Folders)
            {
                foreach (string path in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = path.Substring(root.Length + 1).Replace('\\', '/');
                    foreach (var (number, code) in CodeLines(path))
                    {
                        if (!Forbidden.IsMatch(code) || AllowList.Any(a => a.File == relative && code.Contains(a.Line)))
                            continue;

                        violations.Add($"{relative}:{number}: {code}");
                    }
                }
            }

            Assert.AreEqual(0, violations.Count, "map through LocationVolumeGeometry (forward) or LocationVolumeGeometry.ToMosaic (inverse), and do not read the stored cache:"
                + Environment.NewLine + string.Join(Environment.NewLine, violations));
        }

        [TestMethod]
        public void EveryAllowListEntryStillMatchesALine()
        {
            string root = WebAnnotationDirectory();
            foreach (var (file, line, why) in AllowList)
            {
                string path = Path.Combine(root, file);
                Assert.IsTrue(File.Exists(path), $"{file} ({why}) no longer exists");
                Assert.IsTrue(CodeLines(path).Any(l => Forbidden.IsMatch(l.Code) && l.Code.Contains(line)), $"{file}: '{line}' ({why}) is stale; remove it");
            }
        }

        /// <summary>
        /// The guard sees commands: a mapping call in <c>UI/</c> code is flagged, and one inside a <c>/* */</c> block of
        /// commented-out code is not.
        /// </summary>
        [TestMethod]
        public void TheGuardFlagsCommandCodeButNotCommentedOutBlocks()
        {
            string file = Path.GetTempFileName();
            try
            {
                File.WriteAllLines(file,
                [
                    "Vector2[] mosaic = mapping.VolumeToSection(points);",
                    "/*",
                    "Vector2[] old = mapping.VolumeToSection(points);",
                    "*/",
                    "x = 1; /* mapping.SectionToVolume(p) */",
                    "loc.VolumeShape = shape;",
                ]);

                int[] flagged = [.. CodeLines(file).Where(l => Forbidden.IsMatch(l.Code)).Select(l => l.Number)];
                CollectionAssert.AreEqual(new[] { 1, 6 }, flagged);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Only the owner writes a location's <c>MosaicShape</c>: an edit sets it through
        /// <c>SetShapeFromGeometryInSection</c> (or <c>TryApply</c>), which derives <c>VolumeShape</c> again under the volume's
        /// default transform. A direct write leaves the stored cache describing the old shape.
        /// </summary>
        [TestMethod]
        public void OnlyTheOwnerWritesAMosaicShape()
        {
            string repository = Path.GetFullPath(Path.Combine(WebAnnotationDirectory(), "..", "..", ".."));
            List<string> violations = [];
            foreach (string root in MosaicShapeWriteRoots)
            {
                string directory = Path.Combine(repository, root);
                Assert.IsTrue(Directory.Exists(directory), directory + " not found");
                foreach (string path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
                {
                    string relative = path.Substring(repository.Length + 1).Replace('\\', '/');
                    if (relative == MosaicShapeOwner || relative.Contains("/obj/") || relative.Contains("/bin/"))
                        continue;

                    foreach (var (number, code) in CodeLines(path))
                    {
                        if (MosaicShapeWrite.IsMatch(code))
                            violations.Add($"{relative}:{number}: {code}");
                    }
                }
            }

            Assert.AreEqual(0, violations.Count, "set a mosaic shape with SetShapeFromGeometryInSection or TryApply, which derive the stored volume shape again:"
                + Environment.NewLine + string.Join(Environment.NewLine, violations));
        }

        [TestMethod]
        public void TheGuardFlagsADirectMosaicShapeWrite()
        {
            string[] lines =
            [
                "modelObj.MosaicShape = poly.ToSqlGeometry();",
                "if (loc.MosaicShape == null) return;",
                "SqlGeometry MosaicShape = TryTransform(shape);",
                "LocationObj l = new() { MosaicShape = shape };",
                "loc.MosaicShape.STIsValid();",
            ];

            bool[] flagged = [.. lines.Select(line => MosaicShapeWrite.IsMatch(line))];
            CollectionAssert.AreEqual(new[] { true, false, false, true, false }, flagged);
        }
    }
}
