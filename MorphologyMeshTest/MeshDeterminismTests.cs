using Geometry;
using Geometry.JSON;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Mesh generation has to be reproducible.  A whole-cell BAJAJMULTITEST export of structure 180 was observed
    /// producing a different vertex and triangle count on every run from the same binary and the same input
    /// (168042/112338, 168114/112333, 168183/112331 and 168225/112329 across four runs), which means exported
    /// geometry is not reproducible and no before/after comparison can be trusted as a regression gate.
    ///
    /// These tests build the same slice pair repeatedly and compare the results, so the source can be narrowed
    /// down without a 60 second whole-cell run.
    /// </summary>
    [TestClass]
    public class MeshDeterminismTests
    {
        private const int Iterations = 12;

        private sealed record MeshShape(int Vertices, int Edges, int Faces)
        {
            public static MeshShape Of(BajajGeneratorMesh mesh) =>
                new(mesh.Vertices.Count, mesh.Edges.Count, mesh.Faces.Count);
        }

        private static (Polygon Lower, Polygon Upper, double LowerZ, double UpperZ) LoadRc1Pair()
        {
            string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Testdata", "rc1-structure-1724-adjacent-pair.json");
            Assert.IsTrue(System.IO.File.Exists(path), $"Cached slice pair is missing: {path}");

            using JsonDocument doc = JsonDocument.Parse(System.IO.File.ReadAllText(path));
            JsonElement root = doc.RootElement;

            return (
                GeometryJSONExtensions.PolygonFromJSON(root.GetProperty("lower").GetRawText()),
                GeometryJSONExtensions.PolygonFromJSON(root.GetProperty("upper").GetRawText()),
                root.GetProperty("lowerZ").GetDouble(),
                root.GetProperty("upperZ").GetDouble());
        }

        private static void AssertAllIdentical(IReadOnlyList<MeshShape> results, string what)
        {
            MeshShape first = results[0];
            var distinct = results.Distinct().ToArray();

            Assert.AreEqual(1, distinct.Length,
                $"{what} is not reproducible across {results.Count} runs of identical input. " +
                $"Observed: {string.Join(", ", distinct.Select(d => $"{d.Vertices}v/{d.Edges}e/{d.Faces}f"))}. " +
                $"Per-run: {string.Join(" ", results.Select(r => $"{r.Vertices}/{r.Edges}/{r.Faces}"))}");
        }

        /// <summary>Real contour data, sequentially regenerated in one process.</summary>
        [TestMethod]
        public void GenerateFaces_Rc1Pair_IsReproducibleSequentially()
        {
            var (lower, upper, lowerZ, upperZ) = LoadRc1Pair();

            List<MeshShape> results = [];
            for (int i = 0; i < Iterations; i++)
            {
                BajajGeneratorMesh mesh = new([lower, upper], [lowerZ, upperZ], [false, true]);
                BajajMeshGenerator.GenerateFaces(mesh);
                results.Add(MeshShape.Of(mesh));
            }

            AssertAllIdentical(results, "Sequential RC1 slice pair generation");
        }

        /// <summary>
        /// The production path generates slices concurrently (Task.Factory.StartNew per slice in
        /// BajajMeshGenerator), so any shared mutable state between concurrent generations shows up here but not
        /// in the sequential case.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_Rc1Pair_IsReproducibleUnderConcurrency()
        {
            var (lower, upper, lowerZ, upperZ) = LoadRc1Pair();

            MeshShape[] results = new MeshShape[Iterations];
            System.Threading.Tasks.Parallel.For(0, Iterations, i =>
            {
                BajajGeneratorMesh mesh = new([lower, upper], [lowerZ, upperZ], [false, true]);
                BajajMeshGenerator.GenerateFaces(mesh);
                results[i] = MeshShape.Of(mesh);
            });

            AssertAllIdentical(results, "Concurrent RC1 slice pair generation");
        }

        /// <summary>
        /// Synthetic shapes exercise the untiled-region and medial-axis paths that add vertices beyond the input
        /// contours, which is where a differing vertex count has to originate.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_MismatchedSquares_IsReproducible()
        {
            List<MeshShape> results = [];
            for (int i = 0; i < Iterations; i++)
            {
                BajajGeneratorMesh mesh = new([Square(10), Square(4)], [0.0, 10.0], [false, true]);
                BajajMeshGenerator.GenerateFaces(mesh);
                results.Add(MeshShape.Of(mesh));
            }

            AssertAllIdentical(results, "Mismatched-square generation");
        }

        /// <summary>Two contours on the lower level against one above forces branch/region handling.</summary>
        [TestMethod]
        public void GenerateFaces_BranchingShapes_IsReproducible()
        {
            List<MeshShape> results = [];
            for (int i = 0; i < Iterations; i++)
            {
                BajajGeneratorMesh mesh = new(
                    [OffsetSquare(-8, 0, 5), OffsetSquare(8, 0, 5), OffsetSquare(0, 0, 12)],
                    [0.0, 0.0, 10.0],
                    [false, false, true]);
                BajajMeshGenerator.GenerateFaces(mesh);
                results.Add(MeshShape.Of(mesh));
            }

            AssertAllIdentical(results, "Branching generation");
        }

        private static Polygon Square(double halfWidth) => OffsetSquare(0, 0, halfWidth);

        private static Polygon OffsetSquare(double cx, double cy, double halfWidth) =>
            new(
            [
                new Vector2(cx - halfWidth, cy - halfWidth),
                new Vector2(cx + halfWidth, cy - halfWidth),
                new Vector2(cx + halfWidth, cy + halfWidth),
                new Vector2(cx - halfWidth, cy + halfWidth),
                new Vector2(cx - halfWidth, cy - halfWidth),
            ]);
    }
}
