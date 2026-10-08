using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Unit tests for SegmentationCommand coordinate transformations and point management logic
    /// </summary>
    [TestClass]
    public class SegmentationCommandTests
    {
        #region Gesture Help Tests

        /// <summary>
        /// Finalize moved from a single click to a double-click on a green point; the help bar must say so
        /// and must not still advertise the old gesture.
        /// </summary>
        [TestMethod]
        public void MouseHelpDescribesDoubleClickFinalize()
        {
            string[] help = SegmentationCommand.DefaultMouseHelpStrings;

            Assert.IsTrue(help.Any(line => line.StartsWith("Double-click a green point", StringComparison.Ordinal)));
            Assert.IsFalse(help.Any(line => line.Contains("Left-click inside polygon")));
        }

        #endregion

        #region Point Management Tests

        [TestMethod]
        public void TestFindPointWithinRadius_PointFound()
        {
            // Arrange
            List<Geometry.Vector2> points = new List<Geometry.Vector2>
            {
                new Geometry.Vector2(0, 0),
                new Geometry.Vector2(10, 10),
                new Geometry.Vector2(20, 20)
            };
            Geometry.Vector2 searchPos = new Geometry.Vector2(10.5, 10.5); // Close to second point
            double radiusInPixels = 5.0;

            // Act
            Geometry.Vector2? found = FindPointWithinRadiusHelper(points, searchPos, radiusInPixels);

            // Assert
            Assert.IsNotNull(found, "Should find point within radius");
            Assert.AreEqual(new Geometry.Vector2(10, 10), found.Value);
        }

        [TestMethod]
        public void TestFindPointWithinRadius_PointNotFound()
        {
            // Arrange
            List<Geometry.Vector2> points = new List<Geometry.Vector2>
            {
                new Geometry.Vector2(0, 0),
                new Geometry.Vector2(10, 10),
                new Geometry.Vector2(20, 20)
            };
            Geometry.Vector2 searchPos = new Geometry.Vector2(50, 50); // Far from all points
            double radiusInPixels = 5.0;

            // Act
            Geometry.Vector2? found = FindPointWithinRadiusHelper(points, searchPos, radiusInPixels);

            // Assert
            Assert.IsNull(found, "Should not find point outside radius");
        }

        [TestMethod]
        public void TestFindPointWithinRadius_EmptyList()
        {
            // Arrange
            List<Geometry.Vector2> points = new List<Geometry.Vector2>();
            Geometry.Vector2 searchPos = new Geometry.Vector2(10, 10);
            double radiusInPixels = 5.0;

            // Act
            Geometry.Vector2? found = FindPointWithinRadiusHelper(points, searchPos, radiusInPixels);

            // Assert
            Assert.IsNull(found, "Should return null for empty list");
        }

        /// <summary>
        /// Helper method that simulates the FindPointWithinRadius logic from SegmentationCommand
        /// </summary>
        private Geometry.Vector2? FindPointWithinRadiusHelper(List<Geometry.Vector2> points, Geometry.Vector2 searchPos, double radiusInPixels)
        {
            double radiusSquared = radiusInPixels * radiusInPixels;

            foreach (var pt in points)
            {
                double distSq = Geometry.Vector2.DistanceSquared(pt, searchPos);
                if (distSq <= radiusSquared)
                {
                    return pt;
                }
            }

            return null;
        }

        #endregion

        #region Coordinate Transformation Tests

        [TestMethod]
        public void TestWorldToViewport_BasicTransform()
        {
            // Arrange
            Geometry.Rectangle viewportBounds = new Geometry.Rectangle(
                new Geometry.Vector2(0, 0),
                new Geometry.Vector2(100, 100)
            );
            Geometry.Vector2 worldPos = new Geometry.Vector2(50, 50); // Center
            int viewportWidth = 1000;
            int viewportHeight = 1000;

            // Act
            Geometry.Vector2 result = SegmentationExtensions.MapWorldToViewportPixel(worldPos, viewportBounds, viewportWidth, viewportHeight);

            // Assert
            Assert.AreEqual(500.0, result.X, 0.01, "X coordinate should be at center");
            Assert.AreEqual(500.0, result.Y, 0.01, "Y coordinate should be at center");
        }

        [TestMethod]
        public void TestWorldToViewport_OriginTransform()
        {
            // Arrange
            Geometry.Rectangle viewportBounds = new Geometry.Rectangle(
                new Geometry.Vector2(0, 0),
                new Geometry.Vector2(100, 100)
            );
            Geometry.Vector2 worldPos = new Geometry.Vector2(0, 0); // Origin
            int viewportWidth = 1000;
            int viewportHeight = 1000;

            // Act
            Geometry.Vector2 result = SegmentationExtensions.MapWorldToViewportPixel(worldPos, viewportBounds, viewportWidth, viewportHeight);

            // Assert
            Assert.AreEqual(0.0, result.X, 0.01, "X coordinate should be at origin");
            Assert.AreEqual(0.0, result.Y, 0.01, "Y coordinate should be at origin");
        }

        [TestMethod]
        public void TestWorldToViewport_MaxBoundsTransform()
        {
            // Arrange
            Geometry.Rectangle viewportBounds = new Geometry.Rectangle(
                new Geometry.Vector2(0, 0),
                new Geometry.Vector2(100, 100)
            );
            Geometry.Vector2 worldPos = new Geometry.Vector2(100, 100); // Max bounds
            int viewportWidth = 1000;
            int viewportHeight = 1000;

            // Act
            Geometry.Vector2 result = SegmentationExtensions.MapWorldToViewportPixel(worldPos, viewportBounds, viewportWidth, viewportHeight);

            // Assert
            Assert.AreEqual(1000.0, result.X, 0.01, "X coordinate should be at max");
            Assert.AreEqual(1000.0, result.Y, 0.01, "Y coordinate should be at max");
        }

        [TestMethod]
        public void TestViewportToWorld_BasicTransform()
        {
            // Arrange
            Geometry.Rectangle viewportBounds = new Geometry.Rectangle(
                new Geometry.Vector2(0, 0),
                new Geometry.Vector2(100, 100)
            );
            int pixelX = 500;
            int pixelY = 500;
            int viewportWidth = 1000;
            int viewportHeight = 1000;

            // Act
            Geometry.Vector2 result = SegmentationExtensions.MapViewportPixelToWorld(
                pixelX, pixelY, viewportBounds, viewportWidth, viewportHeight);

            // Assert
            Assert.AreEqual(50.0, result.X, 0.01, "X coordinate should be at center in world space");
            Assert.AreEqual(50.0, result.Y, 0.01, "Y coordinate should be at center in world space");
        }

        [TestMethod]
        public void TestViewportToWorld_RoundTrip()
        {
            // Arrange
            Geometry.Rectangle viewportBounds = new Geometry.Rectangle(
                new Geometry.Vector2(10, 20),
                new Geometry.Vector2(110, 120)
            );
            Geometry.Vector2 originalWorldPos = new Geometry.Vector2(60, 70); // Arbitrary point
            int viewportWidth = 800;
            int viewportHeight = 600;

            // Act: Convert world -> viewport -> world
            Geometry.Vector2 viewportPos = SegmentationExtensions.MapWorldToViewportPixel(
                originalWorldPos, viewportBounds, viewportWidth, viewportHeight);
            Geometry.Vector2 roundTripWorldPos = SegmentationExtensions.MapViewportPixelToWorld(
                (int)viewportPos.X,
                (int)viewportPos.Y,
                viewportBounds,
                viewportWidth,
                viewportHeight);

            // Assert
            Assert.AreEqual(originalWorldPos.X, roundTripWorldPos.X, 1.0, "X coordinate should round-trip correctly");
            Assert.AreEqual(originalWorldPos.Y, roundTripWorldPos.Y, 1.0, "Y coordinate should round-trip correctly");
        }

        #endregion

        #region Polygon Containment Tests

        [TestMethod]
        public void TestPolygonContainsPoint_Inside()
        {
            // Arrange - Create a simple square polygon
            Geometry.Vector2[] vertices = new Geometry.Vector2[]
            {
                new Geometry.Vector2(0, 0),
                new Geometry.Vector2(10, 0),
                new Geometry.Vector2(10, 10),
                new Geometry.Vector2(0, 10)
            };
            Polygon polygon = new Polygon(vertices);
            Geometry.Vector2 testPoint = new Geometry.Vector2(5, 5); // Center point

            // Act
            bool contains = polygon.Contains(testPoint);

            // Assert
            Assert.IsTrue(contains, "Point at center should be inside polygon");
        }

        [TestMethod]
        public void TestPolygonContainsPoint_Outside()
        {
            // Arrange - Create a simple square polygon
            Geometry.Vector2[] vertices = new Geometry.Vector2[]
            {
                new Geometry.Vector2(0, 0),
                new Geometry.Vector2(10, 0),
                new Geometry.Vector2(10, 10),
                new Geometry.Vector2(0, 10)
            };
            Polygon polygon = new Polygon(vertices);
            Geometry.Vector2 testPoint = new Geometry.Vector2(15, 15); // Outside

            // Act
            bool contains = polygon.Contains(testPoint);

            // Assert
            Assert.IsFalse(contains, "Point outside bounds should not be inside polygon");
        }

        [TestMethod]
        public void TestPolygonContainsPoint_OnEdge()
        {
            // Arrange - Create a simple square polygon
            Geometry.Vector2[] vertices = new Geometry.Vector2[]
            {
                new Geometry.Vector2(0, 0),
                new Geometry.Vector2(10, 0),
                new Geometry.Vector2(10, 10),
                new Geometry.Vector2(0, 10)
            };
            Polygon polygon = new Polygon(vertices);
            Geometry.Vector2 testPoint = new Geometry.Vector2(5, 0); // On edge

            // Act
            bool contains = polygon.Contains(testPoint);

            // Assert - Edge cases typically count as inside
            Assert.IsTrue(contains, "Point on edge should be considered inside polygon");
        }

        #endregion

        #region Color Generation Tests

        [TestMethod]
        public void TestGenerateDistinctColors_MultipleColors()
        {
            // Arrange
            int totalColors = 5;
            List<Microsoft.Xna.Framework.Color> colors = new List<Microsoft.Xna.Framework.Color>();

            // Act - Generate distinct colors
            for (int i = 0; i < totalColors; i++)
            {
                var color = SegmentationDistinctColors.GenerateDistinctColor(i, totalColors);
                colors.Add(color);
            }

            // Assert - All colors should be unique (at least by hue)
            for (int i = 0; i < colors.Count; i++)
            {
                for (int j = i + 1; j < colors.Count; j++)
                {
                    bool isDifferent = colors[i].R != colors[j].R || 
                                      colors[i].G != colors[j].G || 
                                      colors[i].B != colors[j].B;
                    Assert.IsTrue(isDifferent, $"Colors at index {i} and {j} should be different");
                }
            }
        }

        [TestMethod]
        public void ColorFromHsl_AutoPolygonizeLocationIdHue_Regression()
        {
            float hue = (float)((42L * 0.6180339887) % 1.0);
            Microsoft.Xna.Framework.Color color = SegmentationDistinctColors.ColorFromHsl(hue, 0.80f, 0.70f, 0.92f);

            Assert.AreEqual(239, color.R);
            Assert.AreEqual(117, color.G);
            Assert.AreEqual(148, color.B);
            Assert.AreEqual(234, color.A);
        }

        #endregion

        #region Tile grid

        [TestMethod]
        public void CellIndex_UsesDownsampleGridFromVolumeOrigin()
        {
            TileCell cell = SegmentationTileGrid.CellIndex(2048, 0, 2);
            Assert.AreEqual(0, cell.Row);
            Assert.AreEqual(1, cell.Col);

            TileCell above = SegmentationTileGrid.CellIndex(0, 2048, 2);
            Assert.AreEqual(1, above.Row);
            Assert.AreEqual(0, above.Col);

            TileCell onBoundary = SegmentationTileGrid.CellIndex(1024 * 2, 0, 2);
            Assert.AreEqual(1, onBoundary.Col);
        }

        [TestMethod]
        public void CellsCovering_DoesNotIncludeTheNextCellWhenTheEdgeLandsOnABoundary()
        {
            List<TileCell> one = SegmentationTileGrid.CellsCovering(0, 0, 2048, 2048, 2);
            Assert.AreEqual(1, one.Count);
            Assert.AreEqual(new TileCell(0, 0), one[0]);

            List<TileCell> two = SegmentationTileGrid.CellsCovering(0, 0, 2049, 2048, 2);
            Assert.AreEqual(2, two.Count);
        }

        [TestMethod]
        public void CellsContainingPoints_NamesOnlyCellsThatHoldThePoints()
        {
            List<Vector2> points =
            [
                new Vector2(100, 100),
                new Vector2(200, 200),
                new Vector2(1500, 100)
            ];

            List<TileCell> cells = SegmentationTileGrid.CellsContainingPoints(points, 1);

            Assert.AreEqual(2, cells.Count);
            CollectionAssert.Contains(cells, new TileCell(0, 0));
            CollectionAssert.Contains(cells, new TileCell(0, 1));
        }

        [TestMethod]
        public void MosaicPixelToWorld_MapsTopLeftPixelToTheHighWorldEdge()
        {
            Vector2 topLeft = SegmentationTileGrid.MosaicPixelToWorld(0, 0, 0, 0, 1024, 2);
            Assert.AreEqual(0, topLeft.X, 1e-6);
            Assert.AreEqual(2048, topLeft.Y, 1e-6);

            Vector2 bottomLeft = SegmentationTileGrid.MosaicPixelToWorld(0, 0, 0, 1024, 1024, 2);
            Assert.AreEqual(0, bottomLeft.X, 1e-6);
            Assert.AreEqual(0, bottomLeft.Y, 1e-6);

            (int x, int y) = SegmentationTileGrid.WorldToMosaicPixel(2048, 1024, 2);
            Assert.AreEqual(1024, x);
            Assert.AreEqual(512, y);
        }

        #endregion
    }
}





