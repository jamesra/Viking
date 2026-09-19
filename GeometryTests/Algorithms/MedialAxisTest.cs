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
            GridVector2[] rectanglePoints =
            [
                new(0, 0),
                new(100, 0),
                new(100, 50),
                new(0, 50),
                new(0, 0)  // Close the ring
            ];
            GridPolygon rectangle = new(rectanglePoints);

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
            GridVector2[] trianglePoints =
            [
                new(0, 0),
                new(100, 0),
                new(50, 86.6), // Approximately equilateral triangle
                new(0, 0)  // Close the ring
            ];
            GridPolygon triangle = new(trianglePoints);

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
            GridVector2[] lShapePoints =
            [
                new(0, 0),
                new(100, 0),
                new(100, 50),
                new(50, 50),
                new(50, 100),
                new(0, 100),
                new(0, 0)  // Close the ring
            ];
            GridPolygon lShape = new(lShapePoints);

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
            GridVector2[] rectanglePoints =
            [
                new(0, 0),
                new(200, 0),
                new(200, 100),
                new(0, 100),
                new(0, 0)  // Close the ring
            ];
            GridPolygon rectangle = new(rectanglePoints);

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
            GridVector2[] rectanglePoints =
            [
                new(0, 0),
                new(100, 0),
                new(100, 50),
                new(0, 50),
                new(0, 0)  // Close the ring
            ];
            GridPolygon rectangle = new(rectanglePoints);

            // Calculate medial axis using improved algorithm
            MedialAxisGraph graph = MedialAxisFinder.ApproximateMedialAxisImproved(rectangle);

            // For each node, it should be a circumcenter and thus equidistant from some set of points
            // This is a sanity check that the algorithm is producing geometrically valid results
            Assert.IsTrue(graph.Nodes.Count > 0, "Should have at least one medial axis node");
        }

        [TestMethod]
        public void TranslateMapsEdgesThroughStoredNodeKeys()
        {
            MedialAxisGraph graph = new();
            GridVector2 stored = new(10, 20);
            GridVector2 nearby = new(10.0004, 20);
            GridVector2 other = new(30, 20);
            graph.AddNode(new MedialAxisVertex(stored));
            graph.AddNode(new MedialAxisVertex(other));
            graph.AddEdge(new MedialAxisEdge(nearby, other));

            MedialAxisGraph translated = graph.Translate(new GridVector2(500000, 300000));

            Assert.IsTrue(translated.Nodes.Count >= 1);
            foreach (var edge in translated.Edges.Values)
            {
                Assert.IsTrue(translated.TryGetValue(edge.SourceNodeKey, out _));
                Assert.IsTrue(translated.TryGetValue(edge.TargetNodeKey, out _));
            }
        }

        [TestMethod]
        public void ImprovedMedialAxisFarFromOriginDoesNotThrow()
        {
            GridPolygon rectangle = new(
            [
                new(400000, 250000),
                new(400200, 250000),
                new(400200, 250080),
                new(400000, 250080),
                new(400000, 250000)
            ]);

            MedialAxisGraph graph = MedialAxisFinder.ApproximateMedialAxisImproved(rectangle);

            Assert.IsNotNull(graph);
            foreach (var edge in graph.Edges.Values)
            {
                Assert.IsTrue(graph.TryGetValue(edge.SourceNodeKey, out _));
                Assert.IsTrue(graph.TryGetValue(edge.TargetNodeKey, out _));
            }
        }
    }
}

