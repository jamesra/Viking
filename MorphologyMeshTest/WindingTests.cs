using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
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

        /// <summary>
        /// Tapered sidewalls span Z, so the cap-containment test never finds a contour (centroid Z is
        /// between slices). A second outward pass must not invert an already-correct tube; that is the
        /// BajajMultiTest composite bug where the trunk went dark while branches stayed lit.
        /// </summary>
        [TestMethod]
        public void OrientComponentsOutward_TaperedPrism_SecondPassDoesNotInvertSidewalls()
        {
            Polygon lower = Square(10);
            Polygon upper = Square(8);
            BajajGeneratorMesh mesh = new([lower, upper], [0, 10], [false, true]);
            BajajMeshGenerator.GenerateFaces(mesh);
            mesh.RecalculateNormals();

            AssertSidewallsPointAwayFromAxis(mesh);

            var ctx = MorphMeshOutwardOrientation.ShapeContext.FromSliceTopology(mesh.Topology);
            int flips = MorphMeshOutwardOrientation.OrientComponentsOutward(mesh, ctx);
            mesh.RecalculateNormals();

            Assert.AreEqual(0, flips, "A second outward pass must not flip an already-outward frustum.");
            AssertSidewallsPointAwayFromAxis(mesh);
        }

        /// <summary>
        /// Outward south wall (CCW lower edge + upper vertex). Composite vertices have no Corresponding
        /// and an empty IsUpper map; the face still must not be marked for flip.
        /// </summary>
        [TestMethod]
        public void FaceNeedsFlip_TaperedSouthWall_FalseWhenAlreadyOutwardWithoutCorresponding()
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
            const int ring = 4;
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 0, ring), new Vector3(-10, -10, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 1, ring), new Vector3(10, -10, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(1, 1, ring), new Vector3(8, -8, 10)));
            mesh.AddFace(0, 1, 2);
            IFace face = mesh.Faces.First();

            Vector3 n = mesh.Normal(face);
            Assert.IsTrue(n.Y < -0.3, "Setup: south wall must point toward -Y (outside the square).");

            var ctx = MorphMeshOutwardOrientation.ShapeContext.FromAccumulated(
                [
                    new MorphMeshOutwardOrientation.ShapeAtZ { Shape = Square(10), IsUpper = false, Z = 0 },
                    new MorphMeshOutwardOrientation.ShapeAtZ { Shape = Square(8), IsUpper = true, Z = 10 },
                ],
                new Dictionary<int, bool>());

            Assert.IsFalse(MorphMeshOutwardOrientation.FaceNeedsFlipForOutward(mesh, face, ctx),
                "Spanning sidewalls must use contour-edge winding, not cap containment between slice Z values.");
        }

        /// <summary>
        /// Two triangles sharing an edge, one reversed. Reorient walks only the 2-manifold corridor and
        /// makes the pair opposite-wound; vertex normals then point into the same hemisphere.
        /// </summary>
        [TestMethod]
        public void Reorient_TwoTrianglesOneReversed_SharedEdgeOppositeAndNormalsAgree()
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
            AddVert(mesh, 0, 0, 0, 0);
            AddVert(mesh, 1, 0, 0, 1);
            AddVert(mesh, 1, 1, 0, 2);
            AddVert(mesh, 0, 1, 0, 3);
            mesh.AddFace(0, 1, 2);
            mesh.AddFace(0, 3, 2);

            Assert.AreEqual(1, MeshWindingDiagnostics.Analyze(mesh).InconsistentManifoldEdges,
                "Setup: the shared edge should start inconsistent.");

            MeshWindingReorientation.Reorient(mesh, CompositeStyleOptions());
            AssertAllTwoFaceEdgesOpposite(mesh);

            mesh.RecalculateNormals();
            Assert.IsTrue(Vector3.Dot(mesh[0].Normal, mesh[2].Normal) > 0.5,
                "Shared-vertex normals must agree after consistent winding.");
            Assert.IsTrue(Vector3.Dot(mesh[1].Normal, mesh[3].Normal) > 0.5,
                "Unshared vertices of the two faces must still sit in the same hemisphere.");
        }

        /// <summary>
        /// Face.Equals ignores winding, so RecalculateNormals must not reuse a cached normal after ReverseFace.
        /// </summary>
        [TestMethod]
        public void RecalculateNormals_AfterReverse_UsesNewWindingNotCache()
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
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

            mesh.RecalculateNormals();
            Vector3 after = mesh[0].Normal;
            Assert.IsTrue(Vector3.Dot(before, after) < -0.5,
                "Vertex normals must follow the reversed winding; a winding-blind cache would keep the old +Z.");
        }

        /// <summary>
        /// Three faces on one edge cannot be oriented; they must not join 2-manifold patches.
        /// After Reorient every Faces.Count==2 edge is consistent; the 3-face edge may remain.
        /// </summary>
        [TestMethod]
        public void Reorient_NonManifoldBarrier_TwoFaceEdgesConsistent()
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
            AddVert(mesh, 0, 0, 0, 0);
            AddVert(mesh, 1, 0, 0, 1);
            AddVert(mesh, 1, 1, 0, 2);
            AddVert(mesh, 0, 1, 0, 3);
            AddVert(mesh, 0.5, -1, 0, 4);
            AddVert(mesh, 0.5, 0.5, 1, 5);
            AddVert(mesh, 2, -1, 0, 6);
            AddVert(mesh, 2.5, -2, 0, 7);

            mesh.AddFace(0, 1, 2);
            mesh.AddFace(0, 3, 2);
            mesh.AddFace(0, 1, 4);
            mesh.AddFace(0, 1, 5);
            mesh.AddFace(1, 4, 6);
            mesh.AddFace(4, 6, 7);

            var before = MeshWindingDiagnostics.Analyze(mesh);
            Assert.AreEqual(1, before.NonManifoldEdges, "Edge 0-1 should carry three faces.");
            Assert.IsTrue(before.InconsistentManifoldEdges > 0, "Setup includes reversed 2-face strips.");

            MeshWindingReorientation.Reorient(mesh, CompositeStyleOptions());

            var after = MeshWindingDiagnostics.Analyze(mesh);
            Assert.AreEqual(1, after.NonManifoldEdges, "Reorient must not try to dissolve the 3-face junction.");
            AssertAllTwoFaceEdgesOpposite(mesh);
            Assert.AreEqual(0, MeshWindingDiagnostics.CountInconsistentAwayFromNonManifold(mesh));
        }

        private static Mesh3D<MorphMeshVertex> NewMesh() => new();

        private static MeshWindingReorientation.Options CompositeStyleOptions() => new()
        {
            RespectAnchorFaces = false,
            AlwaysOrientOutward = false,
            RunRepairPass = false
        };

        private static void AssertSidewallsPointAwayFromAxis(Mesh3D<MorphMeshVertex> mesh)
        {
            int sampled = 0;
            foreach (MorphMeshVertex v in mesh.Vertices)
            {
                Vector2 xy = v.Position.XY();
                if (xy.Magnitude < 1)
                    continue;
                if (Math.Abs(v.Normal.Z) > 0.5)
                    continue;

                sampled++;
                Assert.IsTrue(Vector2.Dot(xy, v.Normal.XY()) > 0,
                    $"Sidewall vertex {v.Position} normal {v.Normal} must point away from the tube axis.");
            }

            Assert.IsTrue(sampled > 0, "Expected sidewall vertices with mostly-horizontal normals.");
        }

        private static void AddVert(Mesh3D<MorphMeshVertex> mesh, double x, double y, double z, int i) =>
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, i, 16), new Vector3(x, y, z)));

        private static void AssertAllTwoFaceEdgesOpposite(Mesh3D<MorphMeshVertex> mesh)
        {
            int shared = 0;
            foreach (KeyValuePair<IEdgeKey, IEdge> kvp in mesh.Edges)
            {
                IFace[] faces = [.. kvp.Value.Faces];
                if (faces.Length != 2)
                    continue;

                shared++;
                bool firstForward = TraversesForward(faces[0].iVerts, kvp.Key.A, kvp.Key.B);
                bool secondForward = TraversesForward(faces[1].iVerts, kvp.Key.A, kvp.Key.B);
                Assert.AreNotEqual(firstForward, secondForward,
                    $"Faces sharing edge ({kvp.Key.A},{kvp.Key.B}) traverse it in the same direction.");
            }

            Assert.IsTrue(shared > 0, "Expected at least one 2-face edge.");
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
