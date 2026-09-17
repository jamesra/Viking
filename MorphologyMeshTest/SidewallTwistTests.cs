using Geometry;
using Geometry.JSON;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.IO;
using System.Text.Json;

namespace MorphologyMeshTest
{
    [TestClass]
    public class SidewallTwistTests
    {
        static Polygon Square(double halfWidth) => new(
        [
            new Vector2(-halfWidth, -halfWidth),
            new Vector2(halfWidth, -halfWidth),
            new Vector2(halfWidth, halfWidth),
            new Vector2(-halfWidth, halfWidth),
            new Vector2(-halfWidth, -halfWidth),
        ]);

        static Polygon RotatedSquare(double halfWidth, double angleRadians)
        {
            double c = Math.Cos(angleRadians);
            double s = Math.Sin(angleRadians);
            Vector2[] corners =
            [
                new(-halfWidth, -halfWidth),
                new(halfWidth, -halfWidth),
                new(halfWidth, halfWidth),
                new(-halfWidth, halfWidth),
            ];
            Vector2[] ring = new Vector2[5];
            for (int i = 0; i < 4; i++)
                ring[i] = new(corners[i].X * c - corners[i].Y * s, corners[i].X * s + corners[i].Y * c);
            ring[4] = ring[0];
            return new Polygon(ring);
        }

        static SidewallTwistReport GenerateAndAnalyze(Polygon lower, Polygon upper, double lowerZ = 0, double upperZ = 10)
        {
            IShape2D[] shapes = [lower, upper];
            BajajGeneratorMesh mesh = new(shapes, [lowerZ, upperZ], [false, true]);
            BajajMeshGenerator.GenerateFaces(mesh);
            return SidewallTwistAnalyzer.Analyze(mesh);
        }

        /// <summary>
        /// Two CCW squares, one rotated a few degrees, share orientation. Crossing chords or Next+Previous
        /// fans here mean correspondence / OTV pairing is twisting the wall, not the cell's annotation data.
        /// </summary>
        [TestMethod]
        public void RotatedStackedSquares_HaveNoCrossingSliceChords()
        {
            Polygon lower = Square(10);
            // Half-width 6 keeps the rotated upper inside the lower (6√2 < 10) so XY outlines do not cross.
            Polygon upper = RotatedSquare(6, angleRadians: 8.0 * Math.PI / 180.0);

            SidewallTwistReport report = GenerateAndAnalyze(lower, upper);
            Console.WriteLine("Rotated stacked squares: " + report);

            Assert.IsTrue(report.SliceChordCount > 0 || report.SidewallFaceCount > 0,
                $"Expected a sidewall between the squares, not two disconnected caps. {report}");
            Assert.AreEqual(0, report.CrossingChordPairs,
                $"XY-crossing Z-chords indicate twisted sidewalls. {report}");
            Assert.AreEqual(0, report.FlippedDirectionFans,
                $"Next+Previous fans on the same corresponding pair hourglass the wall. {report}");
        }

        /// <summary>
        /// One adjacent Z pair from RC1 Muller cell 1724, cached so the suite does not need live OData.
        /// Same CrossingChordPairs / FlippedDirectionFans metric as the synthetic squares.
        /// </summary>
        [TestMethod]
        public void CachedRc1Structure1724Pair_HaveNoCrossingSliceChords()
        {
            string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Testdata", "rc1-structure-1724-adjacent-pair.json");
            Assert.IsTrue(File.Exists(path), $"Cached slice pair is missing: {path}");

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = doc.RootElement;
            Polygon lower = GeometryJSONExtensions.PolygonFromJSON(root.GetProperty("lower").GetRawText());
            Polygon upper = GeometryJSONExtensions.PolygonFromJSON(root.GetProperty("upper").GetRawText());
            double lowerZ = root.GetProperty("lowerZ").GetDouble();
            double upperZ = root.GetProperty("upperZ").GetDouble();

            SidewallTwistReport report = GenerateAndAnalyze(lower, upper, lowerZ, upperZ);
            Console.WriteLine($"{root.GetProperty("source").GetString()}: {report}");

            Assert.IsTrue(report.SliceChordCount > 0 || report.SidewallFaceCount > 0,
                $"Expected a sidewall between the cached pair. {report}");
            Assert.AreEqual(0, report.CrossingChordPairs,
                $"XY-crossing Z-chords on the cached 1724 pair. {report}");
            Assert.AreEqual(0, report.FlippedDirectionFans,
                $"Next+Previous fans on the cached 1724 pair. {report}");
        }
    }
}
