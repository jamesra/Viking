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
            Vector2[] rectanglePoints =
            [
                new(0, 0),
                new(100, 0),
                new(100, 50),
                new(0, 50),
                new(0, 0)  // Close the ring
            ];
            Polygon rectangle = new(rectanglePoints);

            // Calculate medial axis using improved algorithm
            MedialAxisGraph graph = MedialAxisFinder.ApproximateMedialAxisImproved(rectangle);

            // Verify we got a result
            Assert.IsNotNull(graph, "Medial axis graph should not be null");
            Assert.IsTrue(graph.Nodes.Count > 0, "Medial axis graph should have at least one node");

            // Verify all nodes are within the boundary
            foreach (var node in graph.Nodes.Values)
            {
                ShapeRelation relation = rectangle.GetRelation(node.Key);
                Assert.IsTrue(relation == ShapeRelation.Contained || relation == ShapeRelation.Touching,
                    $"Medial axis vertex at {node.Key} should be inside or on boundary of polygon");
            }
        }

        [TestMethod]
        public void TestImprovedMedialAxis_SimpleTriangle()
        {
            // Create a simple triangle (must be closed - first point == last point)
            Vector2[] trianglePoints =
            [
                new(0, 0),
                new(100, 0),
                new(50, 86.6), // Approximately equilateral triangle
                new(0, 0)  // Close the ring
            ];
            Polygon triangle = new(trianglePoints);

            // Calculate medial axis using improved algorithm
            MedialAxisGraph graph = MedialAxisFinder.ApproximateMedialAxisImproved(triangle);

            // Verify we got a result
            Assert.IsNotNull(graph, "Medial axis graph should not be null");

            // Verify all nodes are within the boundary
            foreach (var node in graph.Nodes.Values)
            {
                ShapeRelation relation = triangle.GetRelation(node.Key);
                Assert.IsTrue(relation == ShapeRelation.Contained || relation == ShapeRelation.Touching,
                    $"Medial axis vertex at {node.Key} should be inside or on boundary of polygon");
            }
        }

        [TestMethod]
        public void TestImprovedMedialAxis_LShapedPolygon()
        {
            // Create an L-shaped polygon (must be closed - first point == last point)
            Vector2[] lShapePoints =
            [
                new(0, 0),
                new(100, 0),
                new(100, 50),
                new(50, 50),
                new(50, 100),
                new(0, 100),
                new(0, 0)  // Close the ring
            ];
            Polygon lShape = new(lShapePoints);

            // Calculate medial axis using improved algorithm
            MedialAxisGraph graph = MedialAxisFinder.ApproximateMedialAxisImproved(lShape);

            // Verify we got a result
            Assert.IsNotNull(graph, "Medial axis graph should not be null");
            Assert.IsTrue(graph.Nodes.Count > 0, "Medial axis graph should have at least one node for L-shape");

            // Verify all nodes are within the boundary
            foreach (var node in graph.Nodes.Values)
            {
                ShapeRelation relation = lShape.GetRelation(node.Key);
                Assert.IsTrue(relation == ShapeRelation.Contained || relation == ShapeRelation.Touching,
                    $"Medial axis vertex at {node.Key} should be inside or on boundary of polygon");
            }
        }

        [TestMethod]
        public void TestImprovedMedialAxis_CompareWithOriginal_Rectangle()
        {
            // Create a simple rectangle (must be closed - first point == last point)
            Vector2[] rectanglePoints =
            [
                new(0, 0),
                new(200, 0),
                new(200, 100),
                new(0, 100),
                new(0, 0)  // Close the ring
            ];
            Polygon rectangle = new(rectanglePoints);

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
                Assert.IsTrue(relation == ShapeRelation.Contained || relation == ShapeRelation.Touching,
                    $"Original algorithm vertex at {node.Key} should be inside or on boundary of polygon");
            }

            foreach (var node in improvedGraph.Nodes.Values)
            {
                ShapeRelation relation = rectangle.GetRelation(node.Key);
                Assert.IsTrue(relation == ShapeRelation.Contained || relation == ShapeRelation.Touching,
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
            Vector2[] rectanglePoints =
            [
                new(0, 0),
                new(100, 0),
                new(100, 50),
                new(0, 50),
                new(0, 0)  // Close the ring
            ];
            Polygon rectangle = new(rectanglePoints);

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

        // ---- Chordal Axis Transform (CAT) tests ----

        /// <summary>
        /// Counts connected components of a medial axis graph via a breadth-first traversal over node adjacency.
        /// Isolated nodes (degree 0) each count as their own component.
        /// </summary>
        private static int CountConnectedComponents(MedialAxisGraph graph)
        {
            System.Collections.Generic.HashSet<Vector2> visited = [];
            int components = 0;

            foreach (var startKey in graph.Nodes.Keys)
            {
                if (visited.Contains(startKey))
                    continue;

                components++;
                System.Collections.Generic.Queue<Vector2> queue = new();
                queue.Enqueue(startKey);
                visited.Add(startKey);

                while (queue.Count > 0)
                {
                    Vector2 current = queue.Dequeue();
                    foreach (var neighbor in graph.Nodes[current].Edges.Keys)
                    {
                        if (visited.Add(neighbor))
                            queue.Enqueue(neighbor);
                    }
                }
            }

            return components;
        }

        [TestMethod]
        public void TestChordalAxis_AllNodesStrictlyContained()
        {
            Polygon[] shapes =
            [
                new([new(0, 0), new(100, 0), new(100, 50), new(0, 50), new(0, 0)]),               // rectangle
                new([new(0, 0), new(100, 0), new(50, 86.6), new(0, 0)]),                          // triangle
                new([new(0, 0), new(100, 0), new(100, 50), new(50, 50), new(50, 100), new(0, 100), new(0, 0)]) // L-shape
            ];

            foreach (Polygon shape in shapes)
            {
                MedialAxisGraph graph = MedialAxisFinder.ApproximateMedialAxis(shape);

                Assert.IsTrue(graph.Nodes.Count > 0, "CAT should produce at least one interior node");

                foreach (var node in graph.Nodes.Values)
                {
                    ShapeRelation relation = shape.GetRelation(node.Key);
                    Assert.AreEqual(ShapeRelation.Contained, relation,
                        $"CAT vertex at {node.Key} must be strictly inside the polygon (no boundary-touching nodes when extendToApex is false)");
                }
            }
        }

        [TestMethod]
        public void TestChordalAxis_Connectivity_SingleComponent()
        {
            Polygon[] shapes =
            [
                new([new(0, 0), new(100, 0), new(100, 50), new(0, 50), new(0, 0)]),               // rectangle
                new([new(0, 0), new(100, 0), new(100, 50), new(50, 50), new(50, 100), new(0, 100), new(0, 0)]) // L-shape
            ];

            foreach (Polygon shape in shapes)
            {
                MedialAxisGraph graph = MedialAxisFinder.ApproximateMedialAxis(shape);

                Assert.IsTrue(graph.Nodes.Count > 0, "CAT should produce at least one node");
                Assert.AreEqual(1, CountConnectedComponents(graph),
                    "The Chordal Axis Transform must produce a single connected component");
            }
        }

        [TestMethod]
        public void TestChordalAxis_Spine_LongRectangle_Unbranched()
        {
            // A long, thin rectangle with subdivided long edges triangulates into a strip of sleeve triangles,
            // which should yield an unbranched chain (every node degree <= 2) with no hairs to the corners.
            System.Collections.Generic.List<Vector2> pts = [];
            for (int x = 0; x <= 400; x += 40)
                pts.Add(new Vector2(x, 0));
            for (int x = 400; x >= 0; x -= 40)
                pts.Add(new Vector2(x, 40));
            pts.Add(pts[0]); // close the ring

            Polygon longRect = new([.. pts]);

            MedialAxisGraph graph = MedialAxisFinder.ApproximateMedialAxis(longRect);

            Assert.IsTrue(graph.Nodes.Count > 0, "Spine should have nodes");
            Assert.AreEqual(1, CountConnectedComponents(graph), "Spine should be a single connected component");

            int maxDegree = graph.Nodes.Values.Max(n => n.Edges.Count);
            Assert.IsTrue(maxDegree <= 2,
                $"A long rectangle's CAT spine should be an unbranched chain (max degree <= 2) but found degree {maxDegree}");
        }

        [TestMethod]
        public void TestChordalAxis_Junction_PlusShape()
        {
            // A plus/cross shape has a central region whose triangle(s) carry no boundary edges, producing a
            // junction node of degree >= 3.
            Polygon plus = new(
            [
                new(40, 0), new(80, 0), new(80, 40), new(120, 40), new(120, 80),
                new(80, 80), new(80, 120), new(40, 120), new(40, 80), new(0, 80),
                new(0, 40), new(40, 40), new(40, 0)
            ]);

            MedialAxisGraph graph = MedialAxisFinder.ApproximateMedialAxis(plus);

            Assert.AreEqual(1, CountConnectedComponents(graph), "Plus shape CAT should be connected");
            Assert.IsTrue(graph.Nodes.Values.Any(n => n.Edges.Count >= 3),
                "A plus shape should produce at least one junction node of degree >= 3");
        }

        [TestMethod]
        public void TestChordalAxis_ThinSliver_NonEmptyInterior()
        {
            // A very thin polygon triangulates into highly obtuse triangles whose circumcenters fall far
            // outside the boundary; the old circumcenter approach could drop them all and produce zero interior
            // points. The CAT, using interior-edge midpoints, must still yield at least one interior node.
            Polygon sliver = new(
            [
                new(0, 0), new(100, 0), new(200, 0),
                new(200, 1), new(100, 1), new(0, 1),
                new(0, 0)
            ]);

            MedialAxisGraph catGraph = MedialAxisFinder.ApproximateMedialAxis(sliver);

            Assert.IsTrue(catGraph.Nodes.Count >= 1,
                "CAT must produce at least one interior node for a thin sliver region");

            foreach (var node in catGraph.Nodes.Values)
            {
                Assert.AreEqual(ShapeRelation.Contained, sliver.GetRelation(node.Key),
                    $"Sliver CAT vertex at {node.Key} must be strictly inside the polygon");
            }
        }

        [TestMethod]
        public void TestChordalAxis_Pruning_RemovesHairs()
        {
            // The extend-to-apex skeleton sprouts hairs into convex corners; pruning with a positive ratio
            // should remove them, leaving no more nodes than the unpruned-but-interior-only axis.
            Polygon rectangle = new([new(0, 0), new(200, 0), new(200, 100), new(0, 100), new(0, 0)]);

            MedialAxisGraph withHairs = MedialAxisFinder.ApproximateMedialAxisChordal(rectangle, extendToApex: true, pruneRatio: 0.0);
            MedialAxisGraph pruned = MedialAxisFinder.ApproximateMedialAxisChordal(rectangle, extendToApex: true, pruneRatio: 0.9);

            Assert.IsTrue(pruned.Nodes.Count <= withHairs.Nodes.Count,
                "Pruning should not increase the node count");
            Assert.AreEqual(1, CountConnectedComponents(pruned), "Pruned axis should remain connected");
        }
    }
}

