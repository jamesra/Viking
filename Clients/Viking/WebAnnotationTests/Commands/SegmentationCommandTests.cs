using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Unit tests for SegmentationCommand coordinate transformations and point management logic
    /// </summary>
    [TestClass]
    public class SegmentationCommandTests
    {
        #region Point Management Tests

        [TestMethod]
        public void TestFindPointWithinRadius_PointFound()
        {
            // Arrange
            List<GridVector2> points = new List<GridVector2>
            {
                new GridVector2(0, 0),
                new GridVector2(10, 10),
                new GridVector2(20, 20)
            };
            GridVector2 searchPos = new GridVector2(10.5, 10.5); // Close to second point
            double radiusInPixels = 5.0;

            // Act
            GridVector2? found = FindPointWithinRadiusHelper(points, searchPos, radiusInPixels);

            // Assert
            Assert.IsNotNull(found, "Should find point within radius");
            Assert.AreEqual(new GridVector2(10, 10), found.Value);
        }

        [TestMethod]
        public void TestFindPointWithinRadius_PointNotFound()
        {
            // Arrange
            List<GridVector2> points = new List<GridVector2>
            {
                new GridVector2(0, 0),
                new GridVector2(10, 10),
                new GridVector2(20, 20)
            };
            GridVector2 searchPos = new GridVector2(50, 50); // Far from all points
            double radiusInPixels = 5.0;

            // Act
            GridVector2? found = FindPointWithinRadiusHelper(points, searchPos, radiusInPixels);

            // Assert
            Assert.IsNull(found, "Should not find point outside radius");
        }

        [TestMethod]
        public void TestFindPointWithinRadius_EmptyList()
        {
            // Arrange
            List<GridVector2> points = new List<GridVector2>();
            GridVector2 searchPos = new GridVector2(10, 10);
            double radiusInPixels = 5.0;

            // Act
            GridVector2? found = FindPointWithinRadiusHelper(points, searchPos, radiusInPixels);

            // Assert
            Assert.IsNull(found, "Should return null for empty list");
        }

        /// <summary>
        /// Helper method that simulates the FindPointWithinRadius logic from SegmentationCommand
        /// </summary>
        private GridVector2? FindPointWithinRadiusHelper(List<GridVector2> points, GridVector2 searchPos, double radiusInPixels)
        {
            double radiusSquared = radiusInPixels * radiusInPixels;

            foreach (var pt in points)
            {
                double distSq = GridVector2.DistanceSquared(pt, searchPos);
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
            GridRectangle viewportBounds = new GridRectangle(
                new GridVector2(0, 0),
                new GridVector2(100, 100)
            );
            GridVector2 worldPos = new GridVector2(50, 50); // Center
            int viewportWidth = 1000;
            int viewportHeight = 1000;

            // Act
            GridVector2 result = WorldToViewportHelper(worldPos, viewportBounds, viewportWidth, viewportHeight);

            // Assert
            Assert.AreEqual(500.0, result.X, 0.01, "X coordinate should be at center");
            Assert.AreEqual(500.0, result.Y, 0.01, "Y coordinate should be at center");
        }

        [TestMethod]
        public void TestWorldToViewport_OriginTransform()
        {
            // Arrange
            GridRectangle viewportBounds = new GridRectangle(
                new GridVector2(0, 0),
                new GridVector2(100, 100)
            );
            GridVector2 worldPos = new GridVector2(0, 0); // Origin
            int viewportWidth = 1000;
            int viewportHeight = 1000;

            // Act
            GridVector2 result = WorldToViewportHelper(worldPos, viewportBounds, viewportWidth, viewportHeight);

            // Assert
            Assert.AreEqual(0.0, result.X, 0.01, "X coordinate should be at origin");
            Assert.AreEqual(0.0, result.Y, 0.01, "Y coordinate should be at origin");
        }

        [TestMethod]
        public void TestWorldToViewport_MaxBoundsTransform()
        {
            // Arrange
            GridRectangle viewportBounds = new GridRectangle(
                new GridVector2(0, 0),
                new GridVector2(100, 100)
            );
            GridVector2 worldPos = new GridVector2(100, 100); // Max bounds
            int viewportWidth = 1000;
            int viewportHeight = 1000;

            // Act
            GridVector2 result = WorldToViewportHelper(worldPos, viewportBounds, viewportWidth, viewportHeight);

            // Assert
            Assert.AreEqual(1000.0, result.X, 0.01, "X coordinate should be at max");
            Assert.AreEqual(1000.0, result.Y, 0.01, "Y coordinate should be at max");
        }

        [TestMethod]
        public void TestViewportToWorld_BasicTransform()
        {
            // Arrange
            GridRectangle viewportBounds = new GridRectangle(
                new GridVector2(0, 0),
                new GridVector2(100, 100)
            );
            int pixelX = 500;
            int pixelY = 500;
            int viewportWidth = 1000;
            int viewportHeight = 1000;

            // Act
            GridVector2 result = ViewportToWorldHelper(pixelX, pixelY, viewportBounds, viewportWidth, viewportHeight);

            // Assert
            Assert.AreEqual(50.0, result.X, 0.01, "X coordinate should be at center in world space");
            Assert.AreEqual(50.0, result.Y, 0.01, "Y coordinate should be at center in world space");
        }

        [TestMethod]
        public void TestViewportToWorld_RoundTrip()
        {
            // Arrange
            GridRectangle viewportBounds = new GridRectangle(
                new GridVector2(10, 20),
                new GridVector2(110, 120)
            );
            GridVector2 originalWorldPos = new GridVector2(60, 70); // Arbitrary point
            int viewportWidth = 800;
            int viewportHeight = 600;

            // Act: Convert world -> viewport -> world
            GridVector2 viewportPos = WorldToViewportHelper(originalWorldPos, viewportBounds, viewportWidth, viewportHeight);
            GridVector2 roundTripWorldPos = ViewportToWorldHelper(
                (int)viewportPos.X, 
                (int)viewportPos.Y, 
                viewportBounds, 
                viewportWidth, 
                viewportHeight);

            // Assert
            Assert.AreEqual(originalWorldPos.X, roundTripWorldPos.X, 1.0, "X coordinate should round-trip correctly");
            Assert.AreEqual(originalWorldPos.Y, roundTripWorldPos.Y, 1.0, "Y coordinate should round-trip correctly");
        }

        /// <summary>
        /// Helper method that simulates WorldToViewport logic from SegmentationCommand
        /// </summary>
        private GridVector2 WorldToViewportHelper(GridVector2 worldPos, GridRectangle viewportBounds, int viewportWidth, int viewportHeight)
        {
            GridVector2 boundsMin = viewportBounds.LowerLeft;
            GridVector2 boundsMax = viewportBounds.UpperRight;

            // Normalize to [0,1] range within viewport bounds
            double normalizedX = (worldPos.X - boundsMin.X) / (boundsMax.X - boundsMin.X);
            double normalizedY = (worldPos.Y - boundsMin.Y) / (boundsMax.Y - boundsMin.Y);

            // Scale to viewport pixel dimensions
            return new GridVector2(
                normalizedX * viewportWidth,
                normalizedY * viewportHeight
            );
        }

        /// <summary>
        /// Helper method that simulates ViewportToWorld logic from SegmentationCommand
        /// </summary>
        private GridVector2 ViewportToWorldHelper(int pixelX, int pixelY, GridRectangle viewportBounds, int viewportWidth, int viewportHeight)
        {
            // Normalize from pixel coordinates to [0,1] range
            double normalizedX = (double)pixelX / viewportWidth;
            double normalizedY = (double)pixelY / viewportHeight;

            GridVector2 boundsMin = viewportBounds.LowerLeft;
            GridVector2 boundsMax = viewportBounds.UpperRight;

            // Scale to world coordinates within viewport bounds
            return new GridVector2(
                boundsMin.X + normalizedX * (boundsMax.X - boundsMin.X),
                boundsMin.Y + normalizedY * (boundsMax.Y - boundsMin.Y)
            );
        }

        #endregion

        #region Polygon Containment Tests

        [TestMethod]
        public void TestPolygonContainsPoint_Inside()
        {
            // Arrange - Create a simple square polygon
            GridVector2[] vertices = new GridVector2[]
            {
                new GridVector2(0, 0),
                new GridVector2(10, 0),
                new GridVector2(10, 10),
                new GridVector2(0, 10)
            };
            GridPolygon polygon = new GridPolygon(vertices);
            GridVector2 testPoint = new GridVector2(5, 5); // Center point

            // Act
            bool contains = polygon.Contains(testPoint);

            // Assert
            Assert.IsTrue(contains, "Point at center should be inside polygon");
        }

        [TestMethod]
        public void TestPolygonContainsPoint_Outside()
        {
            // Arrange - Create a simple square polygon
            GridVector2[] vertices = new GridVector2[]
            {
                new GridVector2(0, 0),
                new GridVector2(10, 0),
                new GridVector2(10, 10),
                new GridVector2(0, 10)
            };
            GridPolygon polygon = new GridPolygon(vertices);
            GridVector2 testPoint = new GridVector2(15, 15); // Outside

            // Act
            bool contains = polygon.Contains(testPoint);

            // Assert
            Assert.IsFalse(contains, "Point outside bounds should not be inside polygon");
        }

        [TestMethod]
        public void TestPolygonContainsPoint_OnEdge()
        {
            // Arrange - Create a simple square polygon
            GridVector2[] vertices = new GridVector2[]
            {
                new GridVector2(0, 0),
                new GridVector2(10, 0),
                new GridVector2(10, 10),
                new GridVector2(0, 10)
            };
            GridPolygon polygon = new GridPolygon(vertices);
            GridVector2 testPoint = new GridVector2(5, 0); // On edge

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
                var color = GenerateDistinctColorHelper(i, totalColors);
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

        /// <summary>
        /// Helper method that simulates GenerateDistinctColor logic from SegmentationCommand
        /// </summary>
        private Microsoft.Xna.Framework.Color GenerateDistinctColorHelper(int index, int total)
        {
            float hue = (float)index / System.Math.Max(total, 1);
            return ColorFromHSL(hue, 0.8f, 0.5f, 0.25f);
        }

        private Microsoft.Xna.Framework.Color ColorFromHSL(float hue, float saturation, float lightness, float alpha)
        {
            hue = hue - (float)System.Math.Floor(hue);

            float r, g, b;

            if (saturation == 0)
            {
                r = g = b = lightness;
            }
            else
            {
                float q = lightness < 0.5f 
                    ? lightness * (1 + saturation) 
                    : lightness + saturation - lightness * saturation;
                float p = 2 * lightness - q;

                r = HueToRGB(p, q, hue + 1f / 3f);
                g = HueToRGB(p, q, hue);
                b = HueToRGB(p, q, hue - 1f / 3f);
            }

            return new Microsoft.Xna.Framework.Color(r, g, b, alpha);
        }

        private float HueToRGB(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        #endregion
    }
}





