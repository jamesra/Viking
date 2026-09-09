using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;

namespace MorphologyMeshTest
{
    [TestClass]
    public class CircleCapTests
    {
        private static Polygon CirclePolygon(Circle circle, int segments)
        {
            Vector2[] ring = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                double angle = (2.0 * Math.PI * i) / segments;
                ring[i] = circle.Center + new Vector2(Math.Cos(angle), Math.Sin(angle)) * circle.Radius;
            }

            return new Polygon(ring);
        }

        [TestMethod]
        public void CapMeshEnd_CircleAnnotation_UsesHalfRadiusConcentricRing()
        {
            Circle source = new Circle(10, -5, 8);
            Polygon contour = CirclePolygon(source, 24);
            LocationType[] types = [LocationType.CIRCLE];
            Circle[] circles = [source];

            SliceTopology topology = new(
                [contour],
                [true],
                [0.0],
                shapeLocationTypes: types,
                shapeCircles: circles,
                sliceThickness: 10.0);

            BajajGeneratorMesh mesh = new(topology);

            int facesBefore = mesh.Faces?.Count ?? 0;
            mesh.CapMeshEnd(true);
            int facesAfter = mesh.Faces?.Count ?? 0;

            Assert.IsTrue(facesAfter > facesBefore, "Circle cap should add faces.");

            double capZ = topology.SliceThickness / 2.0;
            var capVerts = mesh.Vertices
                .Where(v => Math.Abs(v.Position.Z - capZ) < 1e-6)
                .Select(v => v.Position.XY())
                .ToArray();

            Assert.IsTrue(capVerts.Length >= 3, "Cap should add an inner ring at the peak Z.");

            foreach (Vector2 p in capVerts)
            {
                double dist = Vector2.Distance(p, source.Center);
                Assert.AreEqual(source.Radius * 0.5, dist, 0.05,
                    $"Cap vertex {p} should lie on the 50% radius circle.");
            }
        }
    }
}
