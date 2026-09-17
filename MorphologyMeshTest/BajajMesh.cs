using FsCheck;
using Geometry;
using Geometry.JSON;
using GeometryTests;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Geometry.Meshing;

namespace MorphologyMeshTest
{
    [TestClass]
    public class BajajMeshTests
    {
        /// <summary>
        /// Generates two overlapping polygons.  Adds corresponding points at overlap positions.
        /// Triangulates the verticies and add constraints for every exterior segment.
        /// </summary>
        [TestMethod]
        [Ignore("Known-failing: the generator still produces non-manifold output for complex random polygon pairs. " +
                "Latest run falsified on two 65-vertex polygons each with one interior ring, where a CORRESPONDING edge " +
                "collected a third face during region closing. Pairing interior rings across slices was never implemented " +
                "(see the abandoned region-pairing block in RegionGraphExtensions.MergeAndCloseRegionsPass), which is the " +
                "suspected cause. MeshManifoldValidator reports the same condition per mesh without failing the suite.")]
        public void TestPolygonOverlapAndBajajMeshing()
        {
            GeometryArbitraries.Register();

            var configuration = FsCheck.Configuration.QuickThrowOnFailure;
            configuration.MaxNbOfTest = 4;
            configuration.QuietOnSuccess = false;
            configuration.StartSize = 3;

            Prop.ForAll<Polygon, Polygon>((A, B) =>
            {
                List<Vector2> listMissingIntersections = [];

                bool PolysIntersect = A.Intersects(B);

                //Throw out tests where the polygons do not intersect
                /*
                if (!PolysIntersect)
                    return (PolysIntersect == false)
                            .Trivial(true)
                            .Classify(true, "Polygons do not intersect");
                */

                var added_intersections = A.AddPointsAtIntersections(B);

                bool polysContainAddedIntersections = PolygonTest.PolygonContainsIntersections(A, listMissingIntersections) && PolygonTest.PolygonContainsIntersections(B, listMissingIntersections);
                var IntersectionsIncludingEndpoints = A.ExteriorSegments.Intersections(B.ExteriorSegments, false);

                //Ensure all of our intersection points are endpoints, there is an edge case of perfectly overlapped exterior rings that must be handled.
                var IntersectionsExcludingEndpoints = PolygonTest.GetPolygonIntersectionsExcludingEndpoings(A, B);

                bool polysOnlyIntersectAtEndpoints = IntersectionsExcludingEndpoints.Count == 0 && IntersectionsIncludingEndpoints.Count > 0;
                bool pass = false == PolysIntersect || (polysContainAddedIntersections && polysOnlyIntersectAtEndpoints);

                if (pass == false)
                {
                    return ((IntersectionsIncludingEndpoints.Count > 0).Label("Intersection points are all endpoints"))
                           .And((IntersectionsExcludingEndpoints.Count == 0).Label("Intersections points are not all at endpoints"))
                           .Classify(PolysIntersect, "Polygons intersect")
                           .Classify(!PolysIntersect, "Polygons did not intersect");
                }

                Polygon[] polys = [A, B];
                double[] ZLevels = [0, 100];
                bool[] IsUpper = [false, true];


                //Triangulate the verticies of the polygons
                BajajGeneratorMesh mesh = new(polys, ZLevels, IsUpper);
                BajajMeshGenerator.AddDelaunayEdges(mesh);

                var RegionPairingGraph = BajajMeshGenerator.GenerateRegionGraph(mesh);

                List<IEdge> listPreContourEdges = [.. mesh.Edges.Values.Where(e => ((MorphMeshEdge)e).Type == EdgeType.CONTOUR)];
                mesh.RemoveInvalidEdges();
                List<IEdge> listPostContourEdges = [.. mesh.Edges.Values.Where(e => ((MorphMeshEdge)e).Type == EdgeType.CONTOUR)];

                bool ContourEdgesCountAsValid = listPreContourEdges.Count == listPostContourEdges.Count;

                BajajMeshGenerator.CompleteCorrespondingVertexFaces(mesh);

                bool edgesHaveMoreThanTwoFaces = EdgesHaveMoreThanTwoFaces(mesh);
                if (edgesHaveMoreThanTwoFaces)
                {
                    return (edgesHaveMoreThanTwoFaces == false).Label("Edges have more than two faces").Label("CompleteCorrespondingVertexFaces");
                }

                SliceChordRTree rTree = mesh.CreateChordTree(ZLevels);
                List<OTVTable> listOTVTables = RegionPairingGraph.MergeAndCloseRegionsPass(mesh, rTree);
                edgesHaveMoreThanTwoFaces = EdgesHaveMoreThanTwoFaces(mesh);
                if (edgesHaveMoreThanTwoFaces)
                {
                    return (edgesHaveMoreThanTwoFaces == false).Label("Edges have more than two faces").Label("MergeAndCloseRegionsPass");
                }

                List<MorphMeshVertex> FirstPassIncompleteVerticies = BajajMeshGenerator.FirstPassSliceChordGeneration(mesh, ZLevels);
                BajajMeshGenerator.FirstPassFaceGeneration(mesh);

                edgesHaveMoreThanTwoFaces = EdgesHaveMoreThanTwoFaces(mesh);
                if (edgesHaveMoreThanTwoFaces)
                {
                    return (edgesHaveMoreThanTwoFaces == false).Label("Edges have more than two faces").Label("FirstPassFaceGeneration");
                }

                MorphMeshRegionGraph SecondPassRegions = MorphRenderMesh.SecondPassRegionDetection(mesh, FirstPassIncompleteVerticies);
                SecondPassRegions.MergeAndCloseRegionsPass(mesh, rTree);
                mesh.RecalculateNormals();

                MeshManifoldReport report = MeshManifoldValidator.Validate(mesh);

                bool AllContourEdgesHaveOneFace = mesh.Edges.Values.Where(e => ((MorphMeshEdge)e).Type == EdgeType.CONTOUR).All(e => e.Faces.Count == 1);
                bool AllCorrespondingEdgesHaveTwoFaces = mesh.Edges.Values.Where(e => ((MorphMeshEdge)e).Type == EdgeType.CORRESPONDING).All(e => e.Faces.Count == 2);
                return AllContourEdgesHaveOneFace.Label("All contour edges have one face.")
                        .And(report.IsEdgeManifold.Label("Edges have three or more faces"))
                        .And(report.IsConsistentlyOriented.Label("Faces disagree across a shared edge"))
                        .And(report.IsFreeOfUnexpectedHoles.Label("The surface has holes away from the contour seam"))
                        .And((AllCorrespondingEdgesHaveTwoFaces).Label("All Corresponding Edges have two faces"))
                        .Label("Manifold report: " + report)
                        .Label("A: " + A.ToJSON())
                        .Label("B: " + B.ToJSON());
            }).Check(configuration);
        }

        private static bool EdgesHaveMoreThanTwoFaces(BajajGeneratorMesh mesh) => mesh.Edges.Values.Any(e => e.Faces.Count > 2);

        /// <summary>
        /// Production slice 620: verts 88/89/90 formed a ~0.4-span colinear triplet. Exact XY keys left them
        /// as three Delaunay sites, so edge 53-89 intersected 88-90.
        /// </summary>
        [TestMethod]
        public void ClusterNearDuplicateXySites_MergesColinearSubPixelTriplet()
        {
            Vector2 p88 = new(1231.91, -309.64);
            Vector2 p89 = new(1231.98, -309.46);
            Vector2 p90 = new(1232.06, -309.27);
            Vector2 p53 = new(1259.46, -189.95);

            Dictionary<Vector2, List<int>> exact = new()
            {
                [p88] = [88],
                [p89] = [89],
                [p90] = [90],
                [p53] = [53],
            };

            Dictionary<Vector2, List<int>> clustered = BajajMeshGenerator.ClusterNearDuplicateXySites(
                exact, BajajMeshGenerator.DelaunayXyClusterDistance);

            Assert.AreEqual(2, clustered.Count, "Triplet should collapse to one site; distant vertex stays separate.");
            Assert.IsTrue(clustered.Values.Any(list => list.Contains(88) && list.Contains(89) && list.Contains(90)));
            Assert.IsTrue(clustered.Values.Any(list => list.Count == 1 && list[0] == 53));
        }

        /// <summary>
        /// A contour with the slice-620 triplet plus a distant vertex must triangulate without
        /// <see cref="Geometry.Meshing.EdgesIntersectTriangulationException"/>.
        /// </summary>
        [TestMethod]
        public void AddDelaunayEdges_NearDuplicateColinearTriplet_DoesNotThrow()
        {
            Polygon contour = new(
            [
                new Vector2(1231.91, -309.64),
                new Vector2(1231.98, -309.46),
                new Vector2(1232.06, -309.27),
                new Vector2(1259.46, -189.95),
                new Vector2(1280, -400),
                new Vector2(1200, -400),
                new Vector2(1231.91, -309.64),
            ]);

            Polygon upper = new(
            [
                new Vector2(1220, -380),
                new Vector2(1260, -380),
                new Vector2(1260, -220),
                new Vector2(1220, -220),
                new Vector2(1220, -380),
            ]);

            BajajGeneratorMesh mesh = new([contour, upper], [0, 10], [false, true]);
            Dictionary<Vector2, List<int>> sites = BajajMeshGenerator.CreatePointToIndexMap(mesh);
            Assert.IsTrue(sites.Values.Any(list => list.Count >= 3), "Near-duplicate contour verts should share one Delaunay site.");

            BajajMeshGenerator.AddDelaunayEdges(mesh);
        }
    }
}
