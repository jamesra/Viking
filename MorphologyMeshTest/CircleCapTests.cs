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
        /// An isolated annotation lives in a slice with its contour on one band and nothing on the other.  Tiling has
        /// nothing to do there: the polygon path used to fill the lone contour flat at the contour Z, and the cap
        /// (which closes the empty band) never found a shape on that band, so a single circle rendered as a
        /// zero-thickness disc (RPC1 structure 51309 / location 365314).  The cap must instead be built from the
        /// populated band and reach half a section toward the empty one.
        /// </summary>
        [TestMethod]
        public void SingleBandSlice_CapsTowardEmptyBand()
        {
            Circle source = new(0, 0, 52.7);
            Polygon contour = CirclePolygon(source, 10);

            SliceTopology topology = new(
                [contour],
                [true],
                [85400.0],
                shapeLocationTypes: [LocationType.CIRCLE],
                shapeCircles: [source],
                sliceThickness: 70.0);

            BajajGeneratorMesh mesh = new(topology);
            BajajMeshGenerator.GenerateFaces(mesh);
            Assert.AreEqual(0, mesh.Faces.Count, "A single-band slice has nothing to tile; it must not fill the contour flat.");

            //The contour is the upper band, so closing the (empty) lower band caps it downward.
            mesh.CapMeshEnd(false);

            Assert.IsTrue(mesh.Faces.Count > 0, "The cap must be built from the populated band.");
            double minZ = mesh.Vertices.Min(v => v.Position.Z);
            double maxZ = mesh.Vertices.Max(v => v.Position.Z);
            Assert.AreEqual(85400.0, maxZ, 1e-6, "The contour stays at its own Z.");
            Assert.AreEqual(85400.0 - 35.0, minZ, 1e-6, "The cap reaches half a section below the contour.");

            MeshManifoldReport report = MeshManifoldValidator.Validate(mesh);
            Assert.AreEqual(0, report.UnexpectedBoundaryEdges, $"The dome must be closed.  {report}");
            Assert.AreEqual(0, report.NonManifoldEdges, report.ToString());
            Assert.AreEqual(contour.ExteriorRing.Length - 1, report.ContourBoundaryEdges,
                $"Each contour edge carries only the cap face until the slice above supplies the other.  {report}");
        }

        /// <summary>
        /// Production FinishSliceMesh caps both open ends then orients. Cap triangles span Z, and if they vote as
        /// sidewalls they outnumber the loft and invert the frustum (grey interior along the cell centreline).
        /// </summary>
        [TestMethod]
        public void GenerateFaces_CappedCirclePair_OutwardNormalsSurviveOrientation()
        {
            Circle lowerCircle = new(0, 0, 10);
            Circle upperCircle = new(0, 0, 10);
            Polygon lower = CirclePolygon(lowerCircle, 8);
            Polygon upper = CirclePolygon(upperCircle, 8);

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
            mesh.EnsureFacesHaveExternalNormals();
            mesh.RecalculateNormals();

            AssertOutwardCirclePair(mesh, lowerCircle.Center, upperCircle.Center);

            var ctx = MorphMeshOutwardOrientation.ShapeContext.FromSliceTopology(mesh.Topology);
            int flips = MorphMeshOutwardOrientation.OrientComponentsOutward(mesh, ctx);
            mesh.RecalculateNormals();

            Assert.AreEqual(0, flips, "A second outward pass must not invert a capped circle frustum.");
            AssertOutwardCirclePair(mesh, lowerCircle.Center, upperCircle.Center);
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

        /// <summary>
        /// Two same-radius CIRCLE annotations that do not overlap in XY must loft in annotation space.  Centroid-snap
        /// virtual overlap used to stack them, and CapCircleEnd then collapsed one dome to a point.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_DisjointEqualCircles_LoftsWithoutCentroidSnap()
        {
            Circle lowerCircle = new(0, 0, 10);
            Circle upperCircle = new(80, 0, 10);
            Polygon lower = CirclePolygon(lowerCircle, 8);
            Polygon upper = CirclePolygon(upperCircle, 8);
            Assert.IsFalse(lower.Intersects(upper), "Fixture requires no XY overlap so centroid-snap would have stacked them.");

            bool[,] links = new bool[2, 2];
            links[0, 0] = links[1, 1] = links[0, 1] = links[1, 0] = true;

            IShape2D[] copies = [CirclePolygon(lowerCircle, 8), CirclePolygon(upperCircle, 8)];
            Assert.IsNull(
                SliceTopology.TryTranslateNonOverlappingShapes(copies, [false, true], links, [LocationType.CIRCLE, LocationType.CIRCLE]),
                "Exclusive CIRCLE pairs must not be centroid-snapped.");

            SliceTopology topology = new(
                [lower, upper],
                [false, true],
                [0.0, 70.0],
                shapeLocationTypes: [LocationType.CIRCLE, LocationType.CIRCLE],
                shapeCircles: [lowerCircle, upperCircle],
                sliceThickness: 70.0,
                shapesAreLinked: links);

            BajajGeneratorMesh mesh = new(topology);
            BajajMeshGenerator.GenerateFaces(mesh);

            Assert.IsTrue(mesh.WindingInteriorSeeds.Any(seed =>
                    seed.ShapeIndex == 0
                    && Vector2.Distance(seed.Position.XY(), (lowerCircle.Center + upperCircle.Center) / 2.0) < 0.05),
                "The pre-cooked circle loft must retain its centerline as the outward-winding seed.");

            double lowerX = mesh.MorphVerticies
                .Where(v => v.ShapeIndex is PolygonIndex pi && pi.ShapeIndex == 0)
                .Average(v => v.Position.X);
            double upperX = mesh.MorphVerticies
                .Where(v => v.ShapeIndex is PolygonIndex pi && pi.ShapeIndex == 1)
                .Average(v => v.Position.X);
            Assert.AreEqual(0.0, lowerX, 1.0, "Lower ring must stay at its annotation centre.");
            Assert.AreEqual(80.0, upperX, 1.0, "Upper ring must stay at its annotation centre, not snap onto the lower.");
            Assert.IsTrue(mesh.Faces.Count >= 16, "An 8-sample pair should loft at least 16 triangles.");
            Assert.IsFalse(mesh.HasUntiledLinkedPairs, string.Join("; ", mesh.GenerationErrors));

            mesh.CapMeshEnd(true);
            mesh.CapMeshEnd(false);

            Assert.AreEqual(1, CountPolesAt(mesh, upperCircle.Center, 70.0 + 35.0), "Upper cap pole stays at the annotation centre.");
            Assert.AreEqual(1, CountPolesAt(mesh, lowerCircle.Center, 0.0 - 35.0), "Lower cap pole stays at the annotation centre.");

            MeshManifoldReport report = MeshManifoldValidator.Validate(mesh);
            Assert.AreEqual(0, report.NonManifoldEdges, report.ToString());
            Assert.AreEqual(0, report.UnexpectedBoundaryEdges, report.ToString());
            Assert.AreEqual(0, report.ContourBoundaryEdges, report.ToString());
        }

        [TestMethod]
        public void GenerateFaces_UnequalCircleSamples_ZipperClosesRing()
        {
            Circle lowerCircle = new(0, 0, 10);
            Circle upperCircle = new(1, 2, 12);
            Polygon lower = SampledCircle(lowerCircle, 8);
            Polygon upper = SampledCircle(upperCircle, 10);

            SliceTopology topology = new(
                [lower, upper],
                [false, true],
                [0.0, 1.0],
                shapeLocationTypes: [LocationType.CIRCLE, LocationType.CIRCLE],
                shapeCircles: [lowerCircle, upperCircle],
                sliceThickness: 1.0);

            BajajGeneratorMesh mesh = new(topology);
            BajajMeshGenerator.GenerateFaces(mesh);

            Assert.AreEqual(18, mesh.Faces.Count, "The zipper adds one face for every edge in both rings.");
            MeshManifoldReport report = MeshManifoldValidator.Validate(mesh);
            Assert.AreEqual(0, report.NonManifoldEdges, report.ToString());
            Assert.AreEqual(0, report.UnexpectedBoundaryEdges, report.ToString());
        }

        [TestMethod]
        public void GenerateFaces_AlignedUnequalCircles_AvoidsThinTwistedPairs()
        {
            const int n = 16;
            Circle lowerCircle = new(40, 9, 5500);
            Circle upperCircle = new(60, -16, 5972);
            Polygon lower = SampledCircle(lowerCircle, n);
            Polygon upper = SampledCircle(upperCircle, n);

            SliceTopology topology = new(
                [lower, upper],
                [false, true],
                [0.0, 70.0],
                shapeLocationTypes: [LocationType.CIRCLE, LocationType.CIRCLE],
                shapeCircles: [lowerCircle, upperCircle],
                sliceThickness: 70.0);

            BajajGeneratorMesh mesh = new(topology);
            BajajMeshGenerator.GenerateFaces(mesh);

            double minimumAngle = mesh.Faces
                .Cast<IFace>()
                .Min(face => MinimumTriangleAngle(
                    mesh[face.iVerts[0]].Position,
                    mesh[face.iVerts[1]].Position,
                    mesh[face.iVerts[2]].Position));

            Assert.IsTrue(minimumAngle > 0.15,
                $"Aligned samples and zipper traversal should avoid thin twisted triangles; minimum angle was {minimumAngle * 180.0 / Math.PI:F2} degrees.");
        }

        private static void AssertOutwardCirclePair(BajajGeneratorMesh mesh, Vector2 lowerCenter, Vector2 upperCenter)
        {
            double minZ = mesh.Vertices.Min(v => v.Position.Z);
            double maxZ = mesh.Vertices.Max(v => v.Position.Z);
            MorphMeshVertex upperPole = mesh.Vertices.First(v =>
                Math.Abs(v.Position.Z - maxZ) < 1e-6 && Vector2.Distance(v.Position.XY(), upperCenter) < 0.05);
            MorphMeshVertex lowerPole = mesh.Vertices.First(v =>
                Math.Abs(v.Position.Z - minZ) < 1e-6 && Vector2.Distance(v.Position.XY(), lowerCenter) < 0.05);

            Assert.IsTrue(upperPole.Normal.Z > 0.3,
                $"Upper cap pole normal {upperPole.Normal} must point +Z, away from the solid.");
            Assert.IsTrue(lowerPole.Normal.Z < -0.3,
                $"Lower cap pole normal {lowerPole.Normal} must point -Z, away from the solid.");

            int sampled = 0;
            foreach (MorphMeshVertex v in mesh.Vertices)
            {
                if (v.ShapeIndex is null)
                    continue;
                Vector2 fromAxis = v.Position.XY() - (v.Position.Z > 35.0 ? upperCenter : lowerCenter);
                if (fromAxis.Magnitude < 1)
                    continue;
                if (Math.Abs(v.Normal.Z) > 0.5)
                    continue;

                sampled++;
                Assert.IsTrue(Vector2.Dot(fromAxis, v.Normal.XY()) > 0,
                    $"Sidewall vertex {v.Position} normal {v.Normal} must point away from the frustum axis.");
            }

            Assert.IsTrue(sampled > 0, "Expected loft vertices with mostly-horizontal normals.");
        }

        private static Polygon SampledCircle(Circle circle, int segments)
        {
            Vector2[] ring = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                double angle = (2.0 * Math.PI * i) / segments;
                ring[i] = circle.Center + new Vector2(Math.Cos(angle), Math.Sin(angle)) * circle.Radius;
            }

            return new Polygon(ring);
        }

        private static int CountPolesAt(BajajGeneratorMesh mesh, Vector2 center, double z) =>
            mesh.Vertices.Count(v => Math.Abs(v.Position.Z - z) < 1e-6 && Vector2.Distance(v.Position.XY(), center) < 0.05);

        private static double MinimumTriangleAngle(Vector3 a, Vector3 b, Vector3 c)
        {
            double ab = Vector3.Distance(a, b);
            double bc = Vector3.Distance(b, c);
            double ca = Vector3.Distance(c, a);
            double angleA = Math.Acos(Math.Clamp(((ab * ab) + (ca * ca) - (bc * bc)) / (2.0 * ab * ca), -1.0, 1.0));
            double angleB = Math.Acos(Math.Clamp(((ab * ab) + (bc * bc) - (ca * ca)) / (2.0 * ab * bc), -1.0, 1.0));
            return Math.Min(angleA, Math.Min(angleB, Math.PI - angleA - angleB));
        }
    }
}
