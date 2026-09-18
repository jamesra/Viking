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
            Assert.IsTrue(mesh.WindingInteriorSeeds.Count > 0, "The Delaunay pass must retain an interior winding seed.");
            foreach (WindingInteriorSeed seed in mesh.WindingInteriorSeeds)
            {
                Assert.AreEqual(ShapeRelation.Contained, mesh.Shapes[seed.ShapeIndex].GetRelation((IPoint2D)seed.Position.XY()),
                    "Only Delaunay triangle centroids contained by an annotation polygon may become winding seeds.");
            }

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
            Assert.AreEqual(0, mesh.ManifoldReport.IsolatedEdges,
                "Stacked-square generation must not leave stranded non-contour edges.");
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
        /// CapCircleEnd triangles span Z (contour to pole), so the old sidewall test treated the contour as
        /// the "lower" vertex of an upper dome and voted flip. An already-outward upper cap must not flip.
        /// </summary>
        [TestMethod]
        public void FaceNeedsFlip_OutwardUpperCap_FalseWhenPoleIsAboveContour()
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
            const int ring = 8;
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 0, ring), new Vector3(10, 0, 10)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 1, ring), new Vector3(7, 7, 10)));
            mesh.AddVertex(new MorphMeshVertex(default(MedialAxisIndex), new Vector3(0, 0, 15)));
            mesh.AddFace(0, 1, 2);
            IFace face = mesh.Faces.First();

            Vector3 n = mesh.Normal(face);
            Assert.IsTrue(n.Z > 0.3, "Setup: upper cap must point toward +Z (away from the solid).");

            var ctx = MorphMeshOutwardOrientation.ShapeContext.FromAccumulated(
                [
                    new MorphMeshOutwardOrientation.ShapeAtZ { Shape = Square(10), IsUpper = true, Z = 10 },
                ],
                new Dictionary<int, bool> { [0] = true });

            Assert.IsFalse(MorphMeshOutwardOrientation.FaceNeedsFlipForOutward(mesh, face, ctx),
                "Cap faces with a medial vertex must not use the contour-vs-pole sidewall Z test.");
        }

        /// <summary>Medial-axis polygon caps, not only circle fans, must point away from the upper end.</summary>
        [TestMethod]
        public void EnsureFacesHaveExternalNormals_GenericUpperCapPointsPositiveZ()
        {
            BajajGeneratorMesh mesh = new([Square(10), Square(8)], [0, 10], [false, true]);
            BajajMeshGenerator.GenerateFaces(mesh);
            mesh.CapMeshEnd(true);
            mesh.EnsureFacesHaveExternalNormals();
            mesh.RecalculateNormals();

            MorphMeshVertex peak = mesh.Vertices
                .Where(vertex => vertex.MedialAxisIndex.HasValue && vertex.Position.Z > 10)
                .MaxBy(vertex => vertex.Position.Z);

            Assert.IsNotNull(peak, "The upper cap must contain a medial-axis peak.");
            Assert.IsTrue(peak.Normal.Z > 0.3,
                $"Upper medial-axis cap normal {peak.Normal} must point +Z, away from the solid.");
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

        /// <summary>
        /// Parity distinguishes a ray that starts outside a shell from one that starts inside. The first outside
        /// hit enters the solid, so its outward normal opposes the ray before the winding is propagated.
        /// </summary>
        [TestMethod]
        public void Reorient_OutsideSeed_UsesEvenParityAndOrientsClosedShellOutward()
        {
            Mesh3D<MorphMeshVertex> mesh = InwardCube();
            var options = RaySeedOptions([new WindingInteriorSeed(new Vector3(3, 0, 0), 0)]);

            MeshWindingReorientation.Reorient(mesh, options);

            foreach (IFace face in mesh.Faces)
            {
                Vector3 center = mesh[face.iVerts].Aggregate(Vector3.Zero, (sum, vertex) => sum + vertex.Position)
                    / face.iVerts.Length;
                Assert.IsTrue(Vector3.Dot(mesh.Normal(face), center) > 0,
                    $"Face at {center} must point away from the cube center.");
            }
        }

        /// <summary>Thirty-two degenerate directions fail loudly instead of silently publishing unknown winding.</summary>
        [TestMethod]
        public void Reorient_ThirtyTwoGrazes_ThrowsHelpMe()
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 0, 3), new Vector3(0, 0, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 1, 3), new Vector3(1, 0, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 2, 3), new Vector3(0, 1, 0)));
            mesh.AddFace(0, 1, 2);

            var options = RaySeedOptions([new WindingInteriorSeed(new Vector3(0.25, 0.25, 0), 0)]);

            InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
                () => MeshWindingReorientation.Reorient(mesh, options));
            StringAssert.Contains(error.Message, "Help me");
            StringAssert.Contains(error.Message, "all 32 rays");
        }

        /// <summary>Open polyline sheets need consistent winding only and must not require an outward ray seed.</summary>
        [TestMethod]
        public void Reorient_PolylineSheet_DoesNotRequireRaySeed()
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
            mesh.AddVertex(new MorphMeshVertex(new PolylineIndex(0, 0, 3), new Vector3(0, 0, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolylineIndex(0, 1, 3), new Vector3(1, 0, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolylineIndex(0, 2, 3), new Vector3(0, 1, 1)));
            mesh.AddFace(0, 1, 2);

            var options = new MeshWindingReorientation.Options
            {
                SeedByInteriorRay = true,
                InteriorSeeds = [],
                OutwardShapeIndices = new HashSet<int>(),
                FailureContext = "polyline test"
            };

            MeshWindingReorientation.Reorient(mesh, options);
            Assert.AreEqual(1, mesh.Faces.Count);
        }

        /// <summary>
        /// Open sheets must not use closed-shell even-parity. An origin outside the patch AABB is rejected
        /// instead of silently flipping the sheet as if it were a solid.
        /// </summary>
        [TestMethod]
        public void Reorient_OpenPatchOutsideOrigin_ThrowsHelpMe()
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
            AddVert(mesh, 0, 0, 0, 0);
            AddVert(mesh, 1, 0, 0, 1);
            AddVert(mesh, 0, 1, 0, 2);
            mesh.AddFace(0, 1, 2);

            var options = RaySeedOptions([new WindingInteriorSeed(new Vector3(10, 10, 10), 0)]);

            InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
                () => MeshWindingReorientation.Reorient(mesh, options));
            StringAssert.Contains(error.Message, "Help me");
            StringAssert.Contains(error.Message, "open-patch seed is not local");
        }

        /// <summary>
        /// Two patches that share only a three-face junction keep independent seeds. A seed on the first
        /// patch must not be reused as a fallback for the second.
        /// </summary>
        [TestMethod]
        public void Reorient_NonManifoldSeparatedPatches_EachUsesOwnShapeSeed()
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 0, 4), new Vector3(0, 0, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 1, 4), new Vector3(1, 0, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 2, 4), new Vector3(1, 1, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(1, 0, 4), new Vector3(0, 1, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(1, 1, 4), new Vector3(0.5, -1, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(1, 2, 4), new Vector3(0.5, 0.5, 1)));

            mesh.AddFace(0, 1, 2);
            mesh.AddFace(0, 1, 4);
            mesh.AddFace(0, 1, 5);
            mesh.AddFace(1, 4, 3);

            var options = new MeshWindingReorientation.Options
            {
                RespectAnchorFaces = false,
                RunRepairPass = false,
                SeedByInteriorRay = true,
                InteriorSeeds =
                [
                    new WindingInteriorSeed(new Vector3(0.7, 0.3, 0.1), 0),
                    new WindingInteriorSeed(new Vector3(0.6, -0.4, 0.1), 1)
                ],
                OutwardShapeIndices = new HashSet<int> { 0, 1 },
                FailureContext = "non-manifold separated patches"
            };

            MeshWindingReorientation.Reorient(mesh, options);
            AssertAllTwoFaceEdgesOpposite(mesh);
        }

        /// <summary>
        /// A later Help-me failure must not leave an earlier patch flipped. Planning records every reversal
        /// and applies them only after every required patch has a seed.
        /// </summary>
        [TestMethod]
        public void Reorient_SecondPatchFails_LeavesFirstPatchUnchanged()
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 0, 3), new Vector3(0, 0, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 1, 3), new Vector3(1, 0, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 2, 3), new Vector3(0, 1, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(1, 0, 3), new Vector3(10, 0, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(1, 1, 3), new Vector3(11, 0, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(1, 2, 3), new Vector3(10, 1, 0)));
            mesh.AddFace(2, 1, 0);
            mesh.AddFace(3, 4, 5);

            IFace first = mesh.Faces.First(f => f.iVerts.Contains(0));
            int[] firstVertsBefore = [.. first.iVerts];

            var options = new MeshWindingReorientation.Options
            {
                RespectAnchorFaces = false,
                RunRepairPass = false,
                SeedByInteriorRay = true,
                InteriorSeeds =
                [
                    new WindingInteriorSeed(new Vector3(0.25, 0.25, -0.05), 0),
                    new WindingInteriorSeed(new Vector3(10.25, 0.25, 0), 1)
                ],
                OutwardShapeIndices = new HashSet<int> { 0, 1 },
                FailureContext = "atomic failure test"
            };

            InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
                () => MeshWindingReorientation.Reorient(mesh, options));
            StringAssert.Contains(error.Message, "Help me");

            IFace firstAfter = mesh.Faces.First(f => f.iVerts.Contains(0));
            CollectionAssert.AreEqual(firstVertsBefore, firstAfter.iVerts.ToArray(),
                "A failed later patch must not apply reversals planned for an earlier valid patch.");
        }

        /// <summary>The same mesh and seeds produce the same winding without Random.Shared.</summary>
        [TestMethod]
        public void Reorient_ClosedShell_IsDeterministicAcrossCalls()
        {
            string first = FaceWindingSignature(OrientCopy(InwardCube()));
            string second = FaceWindingSignature(OrientCopy(InwardCube()));
            Assert.AreEqual(first, second);
        }

        /// <summary>Named scale-aware tolerances must still orient solids at extreme coordinate scales.</summary>
        [TestMethod]
        public void Reorient_ClosedShell_WorksAtTinyAndHugeScales()
        {
            AssertCubeFacesPointOutward(OrientScaledCube(1e-6), 1e-6);
            AssertCubeFacesPointOutward(OrientScaledCube(1e6), 1e6);
        }

        /// <summary>
        /// A trusted first exit must not be rejected because a farther triangle is a vertex graze.
        /// </summary>
        [TestMethod]
        public void Reorient_DistantGraze_DoesNotRejectNearestExit()
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 0, 8), new Vector3(-1, -1, 1)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 1, 8), new Vector3(1, -1, 1)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 2, 8), new Vector3(0, 2, 1)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 3, 8), new Vector3(0, 0, 5)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 4, 8), new Vector3(2, 0, 5)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 5, 8), new Vector3(0, 2, 5)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 6, 8), new Vector3(1, -1, 1)));
            mesh.AddFace(0, 1, 2);
            mesh.AddFace(1, 4, 2);
            mesh.AddFace(2, 4, 5);
            mesh.AddFace(3, 4, 5);

            var options = RaySeedOptions([new WindingInteriorSeed(new Vector3(0, 0, 0), 0)]);
            MeshWindingReorientation.Reorient(mesh, options);

            IFace near = mesh.Faces.First(f => f.iVerts.Contains(0) && f.iVerts.Contains(1) && f.iVerts.Contains(2));
            Vector3 nearNormal = mesh.Normal(near);
            Assert.IsTrue(nearNormal.Z > 0,
                "The nearest +Z exit from an interior seed must keep an outward normal.");
        }

        /// <summary>BFS that reaches a face by two incompatible paths fails instead of a greedy repair.</summary>
        [TestMethod]
        public void Reorient_MobiusStrip_ThrowsNonOrientable()
        {
            Mesh3D<MorphMeshVertex> mesh = MobiusStrip();

            var options = RaySeedOptions([new WindingInteriorSeed(new Vector3(1, 0.5, 0.1), 0)]);
            InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
                () => MeshWindingReorientation.Reorient(mesh, options));
            StringAssert.Contains(error.Message, "Non-orientable patch");
        }

        /// <summary>
        /// Slice generation must keep faces when a patch is non-orientable. Throwing discarded the RPC1 2628
        /// cell-body slice (locations 98942…) after 174 faces existed, leaving a full-width blank band.
        /// </summary>
        [TestMethod]
        public void Reorient_MobiusStrip_WhenNotThrowing_KeepsFacesAndReportsFailure()
        {
            Mesh3D<MorphMeshVertex> mesh = MobiusStrip();
            int faceCount = mesh.Faces.Count;
            var options = RaySeedOptions([new WindingInteriorSeed(new Vector3(1, 0.5, 0.1), 0)]) with
            {
                SkipFailedPatches = true
            };

            MeshWindingReorientation.Result result = MeshWindingReorientation.Reorient(mesh, options);

            Assert.AreEqual(faceCount, mesh.Faces.Count);
            Assert.AreEqual(1, result.PatchFailures.Count);
            StringAssert.Contains(result.PatchFailures[0], "Non-orientable patch");
        }

        /// <summary>
        /// A Möbius patch must not block winding of a separate closed shell, or a giant cell-body slice
        /// loses every correctly tiled region because one 174-face cycle conflicted.
        /// </summary>
        [TestMethod]
        public void Reorient_SkipFailedPatch_OrientsOtherClosedShell()
        {
            Mesh3D<MorphMeshVertex> mesh = InwardCube();
            AddMobiusStrip(mesh, shapeIndex: 1, origin: new Vector3(10, 0, 0));

            var options = new MeshWindingReorientation.Options
            {
                RespectAnchorFaces = false,
                RunRepairPass = false,
                SeedByInteriorRay = true,
                SkipFailedPatches = true,
                InteriorSeeds =
                [
                    new WindingInteriorSeed(new Vector3(0, 0, 0), 0),
                    new WindingInteriorSeed(new Vector3(11, 0.5, 0.1), 1)
                ],
                OutwardShapeIndices = new HashSet<int> { 0, 1 },
                FailureContext = "mixed patch skip test"
            };

            MeshWindingReorientation.Result result = MeshWindingReorientation.Reorient(mesh, options);

            Assert.AreEqual(1, result.PatchFailures.Count);
            StringAssert.Contains(result.PatchFailures[0], "Non-orientable patch");
            foreach (IFace face in mesh.Faces.Where(f => mesh[f.iVerts[0]].ShapeIndex?.ShapeIndex == 0))
            {
                Vector3 center = mesh[face.iVerts].Aggregate(Vector3.Zero, (sum, vertex) => sum + vertex.Position)
                    / face.iVerts.Length;
                Assert.IsTrue(Vector3.Dot(mesh.Normal(face), center) > 0,
                    $"Cube face at {center} must point away from the origin after the Möbius patch is skipped.");
            }
        }

        /// <summary>
        /// <see cref="BajajGeneratorMesh.EnsureFacesHaveExternalNormals"/> is the production path that used to
        /// throw out of ConvertToMesh. It must record the winding error and keep the faces.
        /// </summary>
        [TestMethod]
        public void EnsureFacesHaveExternalNormals_NonOrientable_KeepsFacesAndRecordsError()
        {
            BajajGeneratorMesh mesh = new([Square(10), Square(8)], [0, 10], [false, true]);
            AddMobiusStrip(mesh, shapeIndex: 0, origin: Vector3.Zero);
            mesh.AddWindingInteriorSeed(new Vector3(1, 0.5, 0.1), 0);

            mesh.EnsureFacesHaveExternalNormals();

            Assert.AreEqual(6, mesh.Faces.Count);
            Assert.IsTrue(mesh.GenerationHadErrors);
            Assert.IsTrue(mesh.GenerationErrors.Any(e => e.Contains("Non-orientable patch")),
                string.Join("; ", mesh.GenerationErrors));
        }

        /// <summary>
        /// Same-Z corresponding verts collapse to a point. Walking through that edge glued two zero-area
        /// fans into a patch whose 32 rays all missed on RPC1 2628 slice 445.
        /// </summary>
        [TestMethod]
        public void Reorient_ZeroLengthCorrespondingEdge_WhenSkipping_DoesNotThrow()
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
            Vector3 shared = new(0, 0, 10);
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 0, 3), shared));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(1, 0, 3), shared));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 1, 3), new Vector3(1, 0, 10)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(1, 1, 3), new Vector3(0, 1, 10)));
            mesh.AddFace(0, 1, 2);
            mesh.AddFace(0, 1, 3);

            var options = RaySeedOptions([new WindingInteriorSeed(new Vector3(0.25, 0.25, 10), 0)]) with
            {
                SkipFailedPatches = true,
                OutwardShapeIndices = new HashSet<int> { 0, 1 }
            };

            MeshWindingReorientation.Result result = MeshWindingReorientation.Reorient(mesh, options);
            Assert.AreEqual(2, mesh.Faces.Count);
            Assert.AreEqual(0, result.PatchFailures.Count);
        }

        /// <summary>
        /// An open sheet whose interior seed sits on the plane cannot be ray-seeded. Production keeps the
        /// faces and orients by consistency instead of discarding the slice.
        /// </summary>
        [TestMethod]
        public void Reorient_OpenPatchOnPlane_WhenSkipping_UsesConsistencyOnly()
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 0, 3), new Vector3(0, 0, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 1, 3), new Vector3(1, 0, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, 2, 3), new Vector3(0, 1, 0)));
            mesh.AddFace(0, 1, 2);

            var options = RaySeedOptions([new WindingInteriorSeed(new Vector3(0.25, 0.25, 0), 0)]) with
            {
                SkipFailedPatches = true
            };

            MeshWindingReorientation.Result result = MeshWindingReorientation.Reorient(mesh, options);
            Assert.AreEqual(1, mesh.Faces.Count);
            Assert.AreEqual(0, result.PatchFailures.Count);
        }

        private static MeshWindingReorientation.Options RaySeedOptions(
            IReadOnlyList<WindingInteriorSeed> seeds) => new()
        {
            RespectAnchorFaces = false,
            RunRepairPass = false,
            SeedByInteriorRay = true,
            InteriorSeeds = seeds,
            OutwardShapeIndices = new HashSet<int> { 0 },
            FailureContext = "winding unit test"
        };

        private static Mesh3D<MorphMeshVertex> MobiusStrip()
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
            AddMobiusStrip(mesh, shapeIndex: 0, origin: Vector3.Zero);
            return mesh;
        }

        private static void AddMobiusStrip(Mesh3D<MorphMeshVertex> mesh, int shapeIndex, Vector3 origin)
        {
            int first = mesh.Vertices.Count;
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(shapeIndex, 0, 6), origin + new Vector3(0, 0, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(shapeIndex, 1, 6), origin + new Vector3(1, 0, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(shapeIndex, 2, 6), origin + new Vector3(2, 0, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(shapeIndex, 3, 6), origin + new Vector3(0, 1, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(shapeIndex, 4, 6), origin + new Vector3(1, 1, 0)));
            mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(shapeIndex, 5, 6), origin + new Vector3(2, 1, 0)));
            mesh.AddFace(first + 0, first + 1, first + 4);
            mesh.AddFace(first + 0, first + 4, first + 3);
            mesh.AddFace(first + 1, first + 2, first + 5);
            mesh.AddFace(first + 1, first + 5, first + 4);
            mesh.AddFace(first + 2, first + 3, first + 0);
            mesh.AddFace(first + 2, first + 0, first + 5);
        }

        private static Mesh3D<MorphMeshVertex> InwardCube()
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
            Vector3[] positions =
            [
                new(-1, -1, -1), new(1, -1, -1), new(1, 1, -1), new(-1, 1, -1),
                new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1)
            ];
            for (int i = 0; i < positions.Length; i++)
                mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, i, positions.Length), positions[i]));

            int[][] outward =
            [
                [0, 2, 1], [0, 3, 2],
                [4, 5, 6], [4, 6, 7],
                [0, 1, 5], [0, 5, 4],
                [1, 2, 6], [1, 6, 5],
                [2, 3, 7], [2, 7, 6],
                [3, 0, 4], [3, 4, 7]
            ];
            foreach (int[] face in outward)
                mesh.AddFace(face[2], face[1], face[0]);

            return mesh;
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

        private static Mesh3D<MorphMeshVertex> OrientCopy(Mesh3D<MorphMeshVertex> mesh)
        {
            MeshWindingReorientation.Reorient(mesh, RaySeedOptions([new WindingInteriorSeed(new Vector3(0, 0, 0), 0)]));
            return mesh;
        }

        private static Mesh3D<MorphMeshVertex> OrientScaledCube(double scale)
        {
            Mesh3D<MorphMeshVertex> mesh = NewMesh();
            Vector3[] positions =
            [
                new(-1, -1, -1), new(1, -1, -1), new(1, 1, -1), new(-1, 1, -1),
                new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1)
            ];
            for (int i = 0; i < positions.Length; i++)
                mesh.AddVertex(new MorphMeshVertex(new PolygonIndex(0, i, positions.Length), positions[i] * scale));

            int[][] inward =
            [
                [0, 2, 1], [0, 3, 2],
                [4, 5, 6], [4, 6, 7],
                [0, 1, 5], [0, 5, 4],
                [1, 2, 6], [1, 6, 5],
                [2, 3, 7], [2, 7, 6],
                [3, 0, 4], [3, 4, 7]
            ];
            foreach (int[] face in inward)
                mesh.AddFace(face[2], face[1], face[0]);

            MeshWindingReorientation.Reorient(mesh, RaySeedOptions([new WindingInteriorSeed(Vector3.Zero, 0)]));
            return mesh;
        }

        private static void AssertCubeFacesPointOutward(Mesh3D<MorphMeshVertex> mesh, double scale)
        {
            foreach (IFace face in mesh.Faces)
            {
                Vector3 center = mesh[face.iVerts].Aggregate(Vector3.Zero, (sum, vertex) => sum + vertex.Position)
                    / face.iVerts.Length;
                Assert.IsTrue(Vector3.Dot(mesh.Normal(face), center) > 0,
                    $"Scale {scale}: face at {center} must point away from the cube center.");
            }
        }

        private static string FaceWindingSignature(Mesh3D<MorphMeshVertex> mesh) =>
            string.Join("|", mesh.Faces.Select(f => string.Join(",", f.iVerts)).OrderBy(s => s));

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
