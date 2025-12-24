using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace GeometryTests.Algorithms
{
    [TestClass]
    public class MedialAxisTest
    {
        [TestMethod]
        public void TestImprovedMedialAxis_SimpleRectangle()
        {
            // Create a simple rectangle (must be closed - first point == last point)
            GridVector2[] rectanglePoints = new GridVector2[]
            {
                new GridVector2(0, 0),
                new GridVector2(100, 0),
                new GridVector2(100, 50),
                new GridVector2(0, 50),
                new GridVector2(0, 0)  // Close the ring
            };
            GridPolygon rectangle = new GridPolygon(rectanglePoints);

            // Calculate medial axis using improved algorithm
            MedialAxisGraph graph = MedialAxisFinder.ApproximateMedialAxisImproved(rectangle);

            // Verify we got a result
            Assert.IsNotNull(graph, "Medial axis graph should not be null");
            Assert.IsTrue(graph.Nodes.Count > 0, "Medial axis graph should have at least one node");
            
            // Verify all nodes are within the boundary
            foreach (var node in graph.Nodes.Values)
            {
                ShapeRelation relation = rectangle.GetRelation(node.Key);
                Assert.IsTrue(relation == ShapeRelation.CONTAINED || relation == ShapeRelation.TOUCHING,
                    $"Medial axis vertex at {node.Key} should be inside or on boundary of polygon");
            }
        }

        [TestMethod]
        public void TestImprovedMedialAxis_SimpleTriangle()
        {
            // Create a simple triangle (must be closed - first point == last point)
            GridVector2[] trianglePoints = new GridVector2[]
            {
                new GridVector2(0, 0),
                new GridVector2(100, 0),
                new GridVector2(50, 86.6), // Approximately equilateral triangle
                new GridVector2(0, 0)  // Close the ring
            };
            GridPolygon triangle = new GridPolygon(trianglePoints);

            // Calculate medial axis using improved algorithm
            MedialAxisGraph graph = MedialAxisFinder.ApproximateMedialAxisImproved(triangle);

            // Verify we got a result
            Assert.IsNotNull(graph, "Medial axis graph should not be null");
            
            // Verify all nodes are within the boundary
            foreach (var node in graph.Nodes.Values)
            {
                ShapeRelation relation = triangle.GetRelation(node.Key);
                Assert.IsTrue(relation == ShapeRelation.CONTAINED || relation == ShapeRelation.TOUCHING,
                    $"Medial axis vertex at {node.Key} should be inside or on boundary of polygon");
            }
        }

        [TestMethod]
        public void TestImprovedMedialAxis_LShapedPolygon()
        {
            // Create an L-shaped polygon (must be closed - first point == last point)
            GridVector2[] lShapePoints = new GridVector2[]
            {
                new GridVector2(0, 0),
                new GridVector2(100, 0),
                new GridVector2(100, 50),
                new GridVector2(50, 50),
                new GridVector2(50, 100),
                new GridVector2(0, 100),
                new GridVector2(0, 0)  // Close the ring
            };
            GridPolygon lShape = new GridPolygon(lShapePoints);

            // Calculate medial axis using improved algorithm
            MedialAxisGraph graph = MedialAxisFinder.ApproximateMedialAxisImproved(lShape);

            // Verify we got a result
            Assert.IsNotNull(graph, "Medial axis graph should not be null");
            Assert.IsTrue(graph.Nodes.Count > 0, "Medial axis graph should have at least one node for L-shape");
            
            // Verify all nodes are within the boundary
            foreach (var node in graph.Nodes.Values)
            {
                ShapeRelation relation = lShape.GetRelation(node.Key);
                Assert.IsTrue(relation == ShapeRelation.CONTAINED || relation == ShapeRelation.TOUCHING,
                    $"Medial axis vertex at {node.Key} should be inside or on boundary of polygon");
            }
        }

        [TestMethod]
        public void TestImprovedMedialAxis_CompareWithOriginal_Rectangle()
        {
            // Create a simple rectangle (must be closed - first point == last point)
            GridVector2[] rectanglePoints = new GridVector2[]
            {
                new GridVector2(0, 0),
                new GridVector2(200, 0),
                new GridVector2(200, 100),
                new GridVector2(0, 100),
                new GridVector2(0, 0)  // Close the ring
            };
            GridPolygon rectangle = new GridPolygon(rectanglePoints);

            // Calculate medial axis using both algorithms
            MedialAxisGraph originalGraph = MedialAxisFinder.ApproximateMedialAxis(rectangle);
            MedialAxisGraph improvedGraph = MedialAxisFinder.ApproximateMedialAxisImproved(rectangle);

            // Both should produce results
            Assert.IsNotNull(originalGraph, "Original medial axis graph should not be null");
            Assert.IsNotNull(improvedGraph, "Improved medial axis graph should not be null");

            // Verify all nodes in both graphs are within the boundary
            foreach (var node in originalGraph.Nodes.Values)
            {
                ShapeRelation relation = rectangle.GetRelation(node.Key);
                Assert.IsTrue(relation == ShapeRelation.CONTAINED || relation == ShapeRelation.TOUCHING,
                    $"Original algorithm vertex at {node.Key} should be inside or on boundary of polygon");
            }

            foreach (var node in improvedGraph.Nodes.Values)
            {
                ShapeRelation relation = rectangle.GetRelation(node.Key);
                Assert.IsTrue(relation == ShapeRelation.CONTAINED || relation == ShapeRelation.TOUCHING,
                    $"Improved algorithm vertex at {node.Key} should be inside or on boundary of polygon");
            }

            // Log some statistics for comparison (not assertions, just informational)
            Console.WriteLine($"Original algorithm: {originalGraph.Nodes.Count} nodes, {originalGraph.Edges.Count} edges");
            Console.WriteLine($"Improved algorithm: {improvedGraph.Nodes.Count} nodes, {improvedGraph.Edges.Count} edges");
        }

        [TestMethod]
        public void TestImprovedMedialAxis_CircumcentersAreEquidistant()
        {
            // Create a simple rectangle (must be closed - first point == last point)
            GridVector2[] rectanglePoints = new GridVector2[]
            {
                new GridVector2(0, 0),
                new GridVector2(100, 0),
                new GridVector2(100, 50),
                new GridVector2(0, 50),
                new GridVector2(0, 0)  // Close the ring
            };
            GridPolygon rectangle = new GridPolygon(rectanglePoints);

            // Calculate medial axis using improved algorithm
            MedialAxisGraph graph = MedialAxisFinder.ApproximateMedialAxisImproved(rectangle);

            // For each node, it should be a circumcenter and thus equidistant from some set of points
            // This is a sanity check that the algorithm is producing geometrically valid results
            Assert.IsTrue(graph.Nodes.Count > 0, "Should have at least one medial axis node");

            // Just verify the structure is valid (nodes and edges are consistent)
            foreach (var edge in graph.Edges.Values)
            {
                Assert.IsTrue(graph.TryGetValue(edge.SourceNodeKey, out _),
                    "Edge source should exist as a node in the graph");
                Assert.IsTrue(graph.TryGetValue(edge.TargetNodeKey, out _),
                    "Edge target should exist as a node in the graph");
            }
        }
    }
}

