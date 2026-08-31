using Geometry;
using Geometry.JSON;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MorphologyMeshTest
{
    /// <summary>
    /// RecalculateNormals(IEnumerable&lt;int&gt;) used to ignore its argument and rebuild every normal in the
    /// composite, which made merging cost O(composite) per slice instead of O(merged side).  Now that it only
    /// touches the vertices it is handed, these tests pin the result to what a full recompute produces: the
    /// filtered path is only a valid optimization if it is indistinguishable from the parameterless overload.
    /// </summary>
    [TestClass]
    public class IncrementalNormalsTests
    {
        private const double Tolerance = 1e-12;

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

        private static BajajGeneratorMesh Rc1Mesh()
        {
            var (lower, upper, lowerZ, upperZ) = LoadRc1Pair();
            BajajGeneratorMesh mesh = new([lower, upper], [lowerZ, upperZ], [false, true]);
            BajajMeshGenerator.GenerateFaces(mesh);
            Assert.IsTrue(mesh.Faces.Count > 0, "Expected the cached RC1 slice pair to produce faces.");
            return mesh;
        }

        private static Polygon Square(double halfWidth) => new(
        [
            new Vector2(-halfWidth, -halfWidth),
            new Vector2(halfWidth, -halfWidth),
            new Vector2(halfWidth, halfWidth),
            new Vector2(-halfWidth, halfWidth),
            new Vector2(-halfWidth, -halfWidth),
        ]);

        private static Vector3[] Normals(MeshBase3D<MorphMeshVertex> mesh) =>
            [.. mesh.Vertices.Select(v => v.Normal)];

        private static void AssertNormalsMatch(Vector3[] expected, Vector3[] actual, IEnumerable<int> indicies, string what)
        {
            foreach (int i in indicies)
            {
                Assert.AreEqual(expected[i].X, actual[i].X, Tolerance, $"{what}: vertex {i} X");
                Assert.AreEqual(expected[i].Y, actual[i].Y, Tolerance, $"{what}: vertex {i} Y");
                Assert.AreEqual(expected[i].Z, actual[i].Z, Tolerance, $"{what}: vertex {i} Z");
            }
        }

        /// <summary>Real contour data: handing the filtered overload every index must equal a full recompute.</summary>
        [TestMethod]
        public void RecalculateNormals_Filtered_AllVerticiesOfRc1Pair_MatchesFullRecompute()
        {
            BajajGeneratorMesh mesh = Rc1Mesh();

            mesh.RecalculateNormals(Enumerable.Range(0, mesh.Vertices.Count));
            Vector3[] filtered = Normals(mesh);

            mesh.RecalculateNormals();
            Vector3[] full = Normals(mesh);

            AssertNormalsMatch(full, filtered, Enumerable.Range(0, mesh.Vertices.Count), "RC1 pair, all vertices");
        }

        /// <summary>
        /// The production callers pass a mesh_to_global map, which repeats an index once per referencing vertex.
        /// De-duplicating must not change the accumulated normal.
        /// </summary>
        [TestMethod]
        public void RecalculateNormals_Filtered_RepeatedIndicies_MatchesFullRecompute()
        {
            BajajGeneratorMesh mesh = Rc1Mesh();

            int[] withRepeats = [.. Enumerable.Range(0, mesh.Vertices.Count).SelectMany(i => new[] { i, i, i })];

            mesh.RecalculateNormals(withRepeats);
            Vector3[] filtered = Normals(mesh);

            mesh.RecalculateNormals();
            Vector3[] full = Normals(mesh);

            AssertNormalsMatch(full, filtered, Enumerable.Range(0, mesh.Vertices.Count), "RC1 pair, repeated indicies");
        }

        /// <summary>
        /// Face.Equals ignores winding, so a face reversed after the cache was populated must not keep its old
        /// normal.  The filtered overload evicts only the faces incident to the vertices it was given, so a
        /// reversal among them still has to be picked up.
        /// </summary>
        [TestMethod]
        public void RecalculateNormals_Filtered_AfterReverse_UsesNewWindingNotCache()
        {
            Mesh3D<MorphMeshVertex> mesh = new();
            AddVert(mesh, 0, 0, 0, 0);
            AddVert(mesh, 1, 0, 0, 1);
            AddVert(mesh, 0, 1, 0, 2);
            mesh.AddFace(0, 1, 2);

            mesh.RecalculateNormals();
            Vector3 before = mesh[0].Normal;
            Assert.IsTrue(before.Z > 0.5, "CCW triangle in XY should have a +Z normal.");

            IFace original = mesh.Faces.First();
            mesh.RemoveFace(original);
            mesh.AddFace(0, 2, 1);

            mesh.RecalculateNormals([0, 1, 2]);
            Vector3[] filtered = Normals(mesh);

            Assert.IsTrue(Vector3.Dot(before, filtered[0]) < -0.5,
                "Filtered recompute must follow the reversed winding; a winding-blind cache would keep the old +Z.");

            mesh.RecalculateNormals();
            AssertNormalsMatch(Normals(mesh), filtered, [0, 1, 2], "Reversed face");
        }

        /// <summary>
        /// Reversing one face inside a real mesh: only the vertices of that face are handed to the filtered
        /// overload, and the whole mesh must still end up where a full recompute would put it.
        /// </summary>
        [TestMethod]
        public void RecalculateNormals_Filtered_AfterReverseInRc1Pair_MatchesFullRecompute()
        {
            BajajGeneratorMesh mesh = Rc1Mesh();
            mesh.RecalculateNormals();

            IFace target = mesh.Faces.ElementAt(mesh.Faces.Count / 2);
            int[] iVerts = [.. target.iVerts];

            mesh.RemoveFace(target);
            mesh.AddFace(new Face([.. Enumerable.Reverse(iVerts)]));

            mesh.RecalculateNormals(iVerts);
            Vector3[] filtered = Normals(mesh);

            mesh.RecalculateNormals();
            Vector3[] full = Normals(mesh);

            AssertNormalsMatch(full, filtered, Enumerable.Range(0, mesh.Vertices.Count), "RC1 pair after one reversal");
        }

        /// <summary>
        /// Two disjoint triangles standing in for two merged slices: recomputing the second must leave the
        /// first's normals untouched and still agree with a full recompute.
        /// </summary>
        [TestMethod]
        public void RecalculateNormals_Filtered_Subset_LeavesUntouchedVerticiesAlone()
        {
            Mesh3D<MorphMeshVertex> mesh = new();
            AddVert(mesh, 0, 0, 0, 0);
            AddVert(mesh, 1, 0, 0, 1);
            AddVert(mesh, 0, 1, 0, 2);
            AddVert(mesh, 10, 0, 0, 3);
            AddVert(mesh, 11, 0, 1, 4);
            AddVert(mesh, 10, 1, 1, 5);
            mesh.AddFace(0, 1, 2);
            mesh.AddFace(3, 4, 5);

            mesh.RecalculateNormals();
            Vector3[] full = Normals(mesh);

            Vector3 sentinel = new(0, 0, -1);
            for (int i = 0; i < 3; i++)
                mesh[i].Normal = sentinel;

            mesh.RecalculateNormals([3, 4, 5]);

            Vector3[] after = Normals(mesh);
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(sentinel, after[i], $"Vertex {i} was not in the affected set and must not be recomputed.");

            AssertNormalsMatch(full, after, [3, 4, 5], "Disjoint subset");
        }

        /// <summary>
        /// Incremental assembly against the whole-mesh answer: build a frustum, then add a second frustum's
        /// geometry and recompute only the new indicies.
        /// </summary>
        [TestMethod]
        public void RecalculateNormals_Filtered_IncrementalAppend_MatchesFullRecompute()
        {
            BajajGeneratorMesh first = new([Square(10), Square(8)], [0.0, 10.0], [false, true]);
            BajajMeshGenerator.GenerateFaces(first);
            Assert.IsTrue(first.Faces.Count > 0, "Expected the stacked-square frustum to produce faces.");

            Mesh3D<MorphMeshVertex> composite = new();
            int[] iAdded = new int[first.Vertices.Count];
            for (int i = 0; i < first.Vertices.Count; i++)
                iAdded[i] = composite.AddVertex(MorphMeshVertex.Duplicate(first[i]));

            foreach (IEdgeKey ek in first.Edges.Keys)
                composite.AddEdge(new Edge(iAdded[ek.A], iAdded[ek.B]));

            foreach (IFace f in first.Faces)
                composite.AddFace(new Face([.. f.iVerts.Select(iv => iAdded[iv])]));

            composite.RecalculateNormals(iAdded);
            Vector3[] filtered = Normals(composite);

            composite.RecalculateNormals();
            Vector3[] full = Normals(composite);

            AssertNormalsMatch(full, filtered, Enumerable.Range(0, composite.Vertices.Count), "Incremental append");
        }

        private static void AddVert(Mesh3D<MorphMeshVertex> mesh, double x, double y, double z, int i) =>
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, i, 16), new Vector3(x, y, z)));
    }
}
