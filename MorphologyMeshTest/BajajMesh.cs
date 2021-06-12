using FsCheck;
using Geometry;
using Geometry.JSON;
using GeometryTests;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System.Collections.Generic;
using System.Linq;

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
        public void TestPolygonOverlapAndBajajMeshing()
        {
            GeometryArbitraries.Register();

            var configuration = Configuration.QuickThrowOnFailure;
            configuration.MaxNbOfTest = 4;
            configuration.QuietOnSuccess = false;
            configuration.StartSize = 3;

            Prop.ForAll<GridPolygon, GridPolygon>((A, B) =>
            {
                List<GridVector2> listMissingIntersections = new List<GridVector2>();

                bool PolysIntersect = A.Intersects(B);
                  
                //Throw out tests where the polygons do not intersect
                /*
                if (!PolysIntersect)
                    return (PolysIntersect == false)
                            .Trivial(true)
                            .Classify(true, "Polygons do not intersect");
                */

                var added_intersections = A.AddPointsAtIntersections(B);

                bool polysContainAddedIntersections = GridPolygonTest.PolygonContainsIntersections(A, listMissingIntersections) && GridPolygonTest.PolygonContainsIntersections(B, listMissingIntersections);
                var IntersectionsIncludingEndpoints = A.ExteriorSegments.Intersections(B.ExteriorSegments, false);

                //Ensure all of our intersection points are endpoints, there is an edge case of perfectly overlapped exterior rings that must be handled.
                var IntersectionsExcludingEndpoints = GridPolygonTest.GetPolygonIntersectionsExcludingEndpoings(A, B);

                bool polysOnlyIntersectAtEndpoints = IntersectionsExcludingEndpoints.Count == 0 && IntersectionsIncludingEndpoints.Count > 0;
                bool pass = false == PolysIntersect || (polysContainAddedIntersections && polysOnlyIntersectAtEndpoints);

                if (pass == false)
                {
                    return ((IntersectionsIncludingEndpoints.Count > 0).Label("Intersection points are all endpoints"))
                           .And((IntersectionsExcludingEndpoints.Count == 0).Label("Intersections points are not all at endpoints"))
                           .Classify(PolysIntersect, "Polygons intersect")
                           .Classify(!PolysIntersect, "Polygons did not intersect");
                }

                GridPolygon[] polys = new GridPolygon[] { A, B };
                double[] ZLevels = new double[] { 0, 100 };
                bool[] IsUpper = new bool[] { false, true };


                //Triangulate the verticies of the polygons
                BajajGeneratorMesh mesh = new BajajGeneratorMesh(polys, ZLevels, IsUpper);
                BajajMeshGenerator.AddDelaunayEdges(mesh);
                  
                var RegionPairingGraph = BajajMeshGenerator.GenerateRegionGraph(mesh);

                var listPreContourEdges = mesh.Edges.Values.Where(e => ((MorphMeshEdge)e).Type == EdgeType.CONTOUR).ToList();
                mesh.RemoveInvalidEdges();
                var listPostContourEdges = mesh.Edges.Values.Where(e => ((MorphMeshEdge)e).Type == EdgeType.CONTOUR).ToList();

                bool ContourEdgesCountAsValid = listPreContourEdges.Count == listPostContourEdges.Count;

                BajajMeshGenerator.CompleteCorrespondingVertexFaces(mesh);

                bool edgesHaveMoreThanTwoFaces = EdgesHaveMoreThanTwoFaces(mesh);
                if(edgesHaveMoreThanTwoFaces)
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

                edgesHaveMoreThanTwoFaces = EdgesHaveMoreThanTwoFaces(mesh);
                bool AllContourEdgesHaveOneFace = mesh.Edges.Values.Where(e => ((MorphMeshEdge)e).Type == EdgeType.CONTOUR).All(e => e.Faces.Count == 1);
                bool AllCorrespondingEdgesHaveTwoFaces = mesh.Edges.Values.Where(e => ((MorphMeshEdge)e).Type == EdgeType.CORRESPONDING).All(e => e.Faces.Count == 2);
                return AllContourEdgesHaveOneFace.Label("All contour edges have one face.")
                        .And((false == edgesHaveMoreThanTwoFaces).Label("Edges have three or more faces"))
                        .And((AllCorrespondingEdgesHaveTwoFaces).Label("All Corresponding Edges have two faces"))
                        .Label("A: " + A.ToJSON())
                        .Label("B: " + B.ToJSON());
            }).Check(configuration);
        }

        private static bool EdgesHaveMoreThanTwoFaces(BajajGeneratorMesh mesh)
        {
            return mesh.Edges.Values.Any(e => e.Faces.Count > 2);
        }
    }
}
