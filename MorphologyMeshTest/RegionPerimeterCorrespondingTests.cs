using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System.Collections.Generic;
using System.Linq;

namespace MorphologyMeshTest
{
    [TestClass]
    public class RegionPerimeterCorrespondingTests
    {
        /// <summary>
        /// Two lobes that only share a corresponding pinch (same XY, different Z) must split into
        /// two triangulable halves instead of throwing NotImplementedException.
        /// </summary>
        [TestMethod]
        public void RegionPerimeterToFaces_NonAdjacentCorresponding_ProducesFaces()
        {
            Polygon left = new(
            [
                new Vector2(0, 0),
                new Vector2(-5, -5),
                new Vector2(-10, 0),
                new Vector2(-5, 5),
                new Vector2(0, 0),
            ]);
            Polygon right = new(
            [
                new Vector2(0, 0),
                new Vector2(5, -5),
                new Vector2(10, 0),
                new Vector2(5, 5),
                new Vector2(0, 0),
            ]);

            BajajGeneratorMesh mesh = new([left, right], [0.0, 10.0], [false, true]);

            MorphMeshVertex pinchLower = mesh.Vertices.Single(v =>
                v.Position.XY() == new Vector2(0, 0) && v.Position.Z == 0);
            MorphMeshVertex pinchUpper = mesh.Vertices.Single(v =>
                v.Position.XY() == new Vector2(0, 0) && v.Position.Z == 10);
            Assert.AreEqual(pinchUpper.Index, pinchLower.Corresponding);
            Assert.AreEqual(pinchLower.Index, pinchUpper.Corresponding);

            int VertAt(double x, double y, double z) =>
                mesh.Vertices.Single(v => v.Position == new Vector3(x, y, z)).Index;

            // Figure-8 perimeter: left lobe then right, pinch partners non-adjacent.
            List<int> face =
            [
                pinchLower.Index,
                VertAt(-5, -5, 0),
                VertAt(-10, 0, 0),
                VertAt(-5, 5, 0),
                pinchUpper.Index,
                VertAt(5, -5, 10),
                VertAt(10, 0, 10),
                VertAt(5, 5, 10),
            ];

            List<MorphMeshFace> faces = MorphRenderMesh.RegionPerimeterToFaces(mesh, face);

            Assert.IsTrue(faces.Count >= 2, "Split halves should each triangulate.");
            Assert.IsTrue(faces.All(f => f.iVerts.Length == 3), "Expected triangular faces from triangulation.");
            Assert.IsTrue(faces.All(f => f.iVerts.All(i => i >= 0 && i < mesh.Vertices.Count)));
        }
    }
}
