using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Linq;

namespace MorphologyMeshTest
{
    [TestClass]
    public class MeshManifoldTests
    {
        private static GridPolygon Rectangle(double halfWidth, double halfHeight) =>
            new(
            [
                new GridVector2(-halfWidth, -halfHeight),
                new GridVector2(halfWidth, -halfHeight),
                new GridVector2(halfWidth, halfHeight),
                new GridVector2(-halfWidth, halfHeight),
                new GridVector2(-halfWidth, -halfHeight),
            ]);

        private static GridPolygon Square(double halfWidth) => Rectangle(halfWidth, halfWidth);

        /// <summary>
        /// An ellipse sampled at <paramref name="nPoints"/> verticies.  The medial axis approximation needs a
        /// reasonably sampled boundary, like a real annotation contour, to produce interior points at all.
        /// </summary>
        private static GridPolygon Ellipse(double radiusX, double radiusY, int nPoints)
        {
            GridVector2[] ring = new GridVector2[nPoints + 1];
            for (int i = 0; i < nPoints; i++)
            {
                double theta = 2.0 * Math.PI * i / nPoints;
                ring[i] = new GridVector2(radiusX * Math.Cos(theta), radiusY * Math.Sin(theta));
            }

            ring[nPoints] = ring[0];
            return new GridPolygon(ring);
        }

        /// <summary>
        /// SliceTopology indexes its shapes, Z values and upper/lower flags in lockstep.  A caller that filtered the
        /// shape list without filtering the rest used to build a topology that paired each shape with another
        /// shape's data, which is how polyline slices and shapes dropped below MinAnnotationArea corrupted a mesh.
        /// </summary>
        [TestMethod]
        public void SliceTopologyRejectsFewerFlagsThanShapes()
        {
            IShape2D[] shapes = [Square(10), Square(8)];

            var ex = Assert.ThrowsException<ArgumentException>(() => new SliceTopology(shapes, [true], [0.0, 10.0]));
            StringAssert.Contains(ex.Message, "lockstep");
        }

        [TestMethod]
        public void SliceTopologyRejectsFewerZValuesThanShapes()
        {
            IShape2D[] shapes = [Square(10), Square(8)];

            Assert.ThrowsException<ArgumentException>(() => new SliceTopology(shapes, [false, true], [0.0]));
        }

        [TestMethod]
        public void SliceTopologyRejectsMisalignedNodeIndicies()
        {
            IShape2D[] shapes = [Square(10), Square(8)];

            Assert.ThrowsException<ArgumentException>(() => new SliceTopology(shapes, [false, true], [0.0, 10.0], [1UL]));
        }

        /// <summary>
        /// A slice split entirely into one set still has to report the other set as empty rather than sizing it from
        /// the shape count, which previously left null entries in the lower shape array.
        /// </summary>
        [TestMethod]
        public void SliceTopologySeparatesUpperAndLowerByFlag()
        {
            IShape2D[] shapes = [Square(10), Square(8), Square(6)];

            SliceTopology topology = new(shapes, [false, true, true], [0.0, 10.0, 10.0]);

            Assert.AreEqual(1, topology.LowerShapeIndicies.Count);
            Assert.AreEqual(2, topology.UpperShapeIndicies.Count);
            Assert.AreEqual(0, topology.LowerShapeIndicies.Single());
        }

        /// <summary>
        /// The end cap used to place every medial axis vertex at one Z, producing a flat plateau joined to the
        /// contour by a vertical wall.  Each vertex should instead rise in proportion to its distance from the
        /// contour, so the cap is a dome that peaks half a section above the contour it closes.
        /// </summary>
        [TestMethod]
        public void CapMeshEndProducesADomeRatherThanAPlateau()
        {
            const double LowerZ = 0;
            const double UpperZ = 100;

            //An elongated rectangle gives the medial axis interior points at clearly different clearances.
            IShape2D[] shapes = [Ellipse(80, 20, 16), Ellipse(80, 20, 16)];
            BajajGeneratorMesh mesh = new(shapes, [LowerZ, UpperZ], [false, true]);

            mesh.CapMeshEnd(true);

            double[] capZ = [.. mesh.Verticies.Where(v => v.MedialAxisIndex.HasValue).Select(v => v.Position.Z)];

            Assert.IsTrue(capZ.Length > 1, "The cap should add several medial axis verticies to a long rectangle.");

            double halfSection = (UpperZ - LowerZ) / 2.0;
            Assert.AreEqual(UpperZ + halfSection, capZ.Max(), 1.0,
                "The deepest cap vertex should sit half a section above the contour it closes.");

            Assert.IsTrue(capZ.Min() >= UpperZ - Global.Epsilon,
                "No cap vertex should fall below the contour it closes.");

            Assert.IsTrue(capZ.Max() - capZ.Min() > 1.0,
                "Cap verticies must vary in Z.  A single shared Z is the flat plateau this replaced.");
        }

        /// <summary>
        /// Capping the lower end mirrors the upper end, descending below the contour.
        /// </summary>
        [TestMethod]
        public void CapMeshEndDomesDownwardOnTheLowerEnd()
        {
            const double LowerZ = 0;
            const double UpperZ = 100;

            IShape2D[] shapes = [Ellipse(80, 20, 16), Ellipse(80, 20, 16)];
            BajajGeneratorMesh mesh = new(shapes, [LowerZ, UpperZ], [false, true]);

            mesh.CapMeshEnd(false);

            double[] capZ = [.. mesh.Verticies.Where(v => v.MedialAxisIndex.HasValue).Select(v => v.Position.Z)];

            Assert.IsTrue(capZ.Length > 1, "The cap should add several medial axis verticies to a long rectangle.");

            double halfSection = (UpperZ - LowerZ) / 2.0;
            Assert.AreEqual(LowerZ - halfSection, capZ.Min(), 1.0,
                "The deepest cap vertex should sit half a section below the contour it closes.");

            Assert.IsTrue(capZ.Max() <= LowerZ + Global.Epsilon,
                "No cap vertex should rise above the contour it closes.");
        }

        /// <summary>
        /// The generator should produce a surface with no non-manifold edges, no faces disagreeing across a shared
        /// edge, and no holes beyond the contour seams the neighboring slice will close.
        /// </summary>
        [TestMethod]
        public void StackedSquaresProduceAValidSliceSurface()
        {
            IShape2D[] shapes = [Square(10), Square(8)];
            BajajGeneratorMesh mesh = new(shapes, [0, 10], [false, true]);

            BajajMeshGenerator.GenerateFaces(mesh);

            MeshManifoldReport report = MeshManifoldValidator.Validate(mesh);

            Assert.AreEqual(0, report.NonManifoldEdges, $"Edges shared by three or more faces.  {report}");
            Assert.AreEqual(0, report.InconsistentManifoldEdges, $"Faces disagree across a shared edge.  {report}");
            Assert.AreEqual(0, report.UnexpectedBoundaryEdges, $"The surface has holes away from the contour seam.  {report}");
            Assert.IsTrue(report.ContourBoundaryEdges > 0, $"A slice mesh should leave its contour seam open.  {report}");
        }
    }
}
