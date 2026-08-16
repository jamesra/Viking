using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System.Collections.Generic;
using System.Linq;

namespace MorphologyMeshTest
{
    [TestClass]
    public class WindingTests
    {
        private static Polygon Square(double halfWidth)
        {
            return new Polygon(new Vector2[]
            {
                new(-halfWidth, -halfWidth),
                new(halfWidth, -halfWidth),
                new(halfWidth, halfWidth),
                new(-halfWidth, halfWidth),
                new(-halfWidth, -halfWidth),
            });
        }

        /// <summary>
        /// Builds a truncated-pyramid mesh (a small upper square stacked over a larger lower square) through the
        /// real Bajaj generation pipeline, then verifies the reorientation in EnsureFacesHaveExternalNormals leaves
        /// every interior edge shared by two faces traversed in opposite directions (manifold-consistent winding).
        /// Inconsistent winding is exactly what produced the culling gaps in the rendered mesh.
        /// </summary>
        [TestMethod]
        public void EnsureFacesHaveExternalNormals_StackedSquares_ConsistentWinding()
        {
            Polygon lower = Square(10);
            Polygon upper = Square(8);

            IShape2D[] shapes = [lower, upper];
            double[] zLevels = [0, 10];
            bool[] isUpper = [false, true];

            BajajGeneratorMesh mesh = new(shapes, zLevels, isUpper);

            BajajMeshGenerator.GenerateFaces(mesh);

            Assert.IsTrue(mesh.Faces.Count > 0, "Pipeline should generate faces for a stacked-square frustum.");

            int sharedEdgeCount = 0;
            foreach (KeyValuePair<IEdgeKey, IEdge> kvp in mesh.Edges)
            {
                IFace[] faces = [.. kvp.Value.Faces];
                if (faces.Length != 2)
                    continue; //Only interior (manifold) edges constrain winding consistency

                sharedEdgeCount++;

                bool firstForward = TraversesForward(faces[0].iVerts, kvp.Key.A, kvp.Key.B);
                bool secondForward = TraversesForward(faces[1].iVerts, kvp.Key.A, kvp.Key.B);

                Assert.AreNotEqual(firstForward, secondForward,
                    $"Faces sharing edge ({kvp.Key.A},{kvp.Key.B}) traverse it in the same direction (inconsistent winding).");
            }

            Assert.IsTrue(sharedEdgeCount > 0, "Expected interior edges shared by two faces.");
        }

        private static bool TraversesForward(System.Collections.Immutable.ImmutableArray<int> iVerts, int a, int b)
        {
            for (int i = 0; i < iVerts.Length; i++)
            {
                int x = iVerts[i];
                int y = iVerts[(i + 1) % iVerts.Length];
                if (x == a && y == b)
                    return true;
                if (x == b && y == a)
                    return false;
            }

            return false;
        }
    }
}
