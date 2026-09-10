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

            Assert.IsTrue(capVerts.Length >= 4, "Cap should add an inner ring plus a pole at the peak Z.");

            int poles = 0;
            foreach (Vector2 p in capVerts)
            {
                double dist = Vector2.Distance(p, source.Center);
                if (dist < 0.05)
                {
                    poles++;
                    continue;
                }

                Assert.AreEqual(source.Radius * 0.5, dist, 0.05,
                    $"Cap vertex {p} should lie on the 50% radius circle.");
            }

            Assert.AreEqual(1, poles, "The inner ring is closed by exactly one pole at the circle centre.");
        }

        /// <summary>
        /// A two-circle structure is capped at both ends; the caps must close the surface rather than leave the
        /// inner ring as an open frustum (RPC1 368453/368452 reported holes:20).
        /// </summary>
        [TestMethod]
        public void CapMeshEnd_CirclePair_ClosesSurface()
        {
            Circle lowerCircle = new(0, 0, 44);
            Circle upperCircle = new(26, 54, 44);
            Polygon lower = CirclePolygon(lowerCircle, 10);
            Polygon upper = CirclePolygon(upperCircle, 10);

            SliceTopology topology = new(
                [lower, upper],
                [false, true],
                [0.0, 70.0],
                shapeLocationTypes: [LocationType.CIRCLE, LocationType.CIRCLE],
                shapeCircles: [lowerCircle, upperCircle],
                sliceThickness: 70.0);

            BajajGeneratorMesh mesh = new(topology);
            BajajMeshGenerator.GenerateFaces(mesh);
            mesh.CapMeshEnd(true);
            mesh.CapMeshEnd(false);

            MeshManifoldReport report = MeshManifoldValidator.Validate(mesh);
            Assert.AreEqual(0, report.NonManifoldEdges, $"Caps must not stack faces on an edge.  {report}");
            Assert.AreEqual(0, report.UnexpectedBoundaryEdges, $"Both circle caps must close.  {report}");
            Assert.AreEqual(0, report.ContourBoundaryEdges, $"Every contour edge has a band face and a cap face.  {report}");
        }
    }
}
