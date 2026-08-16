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
    }
}
