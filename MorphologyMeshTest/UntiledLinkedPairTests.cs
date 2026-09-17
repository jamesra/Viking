using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System.Linq;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Linked cross-band annotations with no spanning face must be flagged as <see cref="BajajGeneratorMesh.HasUntiledLinkedPairs"/>,
    /// even when the manifold report is otherwise clean.
    /// </summary>
    [TestClass]
    public class UntiledLinkedPairTests
    {
        private const double LowerZ = 0.0;
        private const double UpperZ = 10.0;

        private static Polygon Circle(double radius, Vector2 center, int nPoints = 16)
        {
            Vector2[] ring = new Vector2[nPoints + 1];
            for (int i = 0; i < nPoints; i++)
            {
                double theta = 2.0 * System.Math.PI * i / nPoints;
                ring[i] = new Vector2(center.X + (radius * System.Math.Cos(theta)), center.Y + (radius * System.Math.Sin(theta)));
            }

            ring[nPoints] = ring[0];
            return new Polygon(ring);
        }

        private static bool[,] LinkedPairMatrix()
        {
            bool[,] linked = new bool[2, 2];
            linked[0, 0] = linked[1, 1] = linked[0, 1] = linked[1, 0] = true;
            return linked;
        }

        /// <summary>
        /// Two linked circles that already overlap in XY — should tile and must not set the untiled-link flag.
        /// </summary>
        private static SliceTopology OverlappingLinkedPairTopology()
        {
            Polygon lower = Circle(20, new Vector2(0, 0));
            Polygon upper = Circle(20, new Vector2(5, 5));
            Assert.IsTrue(lower.Intersects(upper), "Fixture requires XY overlap.");

            IShape2D[] shapes = [lower, upper];
            bool[] isUpper = [false, true];
            bool[,] links = LinkedPairMatrix();

            System.Collections.Generic.List<IShape2D> shapeList = [.. shapes];
            var corresponding = shapeList.AddCorrespondingVertices();
            SliceTopology.AddPointsBetweenAdjacentCorrespondingVerticies([.. shapeList.OfType<Polygon>()], corresponding);

            return new SliceTopology(
                shapeList,
                isUpper,
                [LowerZ, UpperZ],
                shapeIndexToMorphNodeIndex: [100UL, 101UL],
                sliceThickness: UpperZ - LowerZ,
                virtualOverlapOffsets: null,
                shapesAreLinked: links);
        }

        /// <summary>
        /// Linked circles far apart with virtual overlap suppressed (all-zero offsets).
        /// </summary>
        private static SliceTopology DisjointLinkedPairTopology()
        {
            Polygon lower = Circle(10, new Vector2(0, 0));
            Polygon upper = Circle(10, new Vector2(80, 0));
            Assert.IsFalse(lower.Intersects(upper), "Fixture requires no XY overlap.");

            IShape2D[] shapes = [lower, upper];
            bool[] isUpper = [false, true];
            bool[,] links = LinkedPairMatrix();

            //Non-null zero array suppresses constructor VO so the pair stays disjoint.
            Vector2[] offsets = new Vector2[shapes.Length];

            System.Collections.Generic.List<IShape2D> shapeList = [.. shapes];
            var corresponding = shapeList.AddCorrespondingVertices();
            SliceTopology.AddPointsBetweenAdjacentCorrespondingVerticies([.. shapeList.OfType<Polygon>()], corresponding);

            return new SliceTopology(
                shapeList,
                isUpper,
                [LowerZ, UpperZ],
                shapeIndexToMorphNodeIndex: [200UL, 201UL],
                sliceThickness: UpperZ - LowerZ,
                virtualOverlapOffsets: offsets,
                shapesAreLinked: links);
        }

        [TestMethod]
        public void OverlappingLinkedPair_DoesNotSetUntiledLinkedFlag()
        {
            BajajGeneratorMesh mesh = new(OverlappingLinkedPairTopology());
            BajajMeshGenerator.GenerateFaces(mesh);

            Assert.IsFalse(mesh.HasUntiledLinkedPairs, "Overlapping linked pair must produce spanning faces.");
            Assert.IsTrue(mesh.CountFacesSpanning(0, 1) > 0);
            Assert.AreEqual(0, mesh.UntiledLinkedShapeIndices.Count);
        }

        [TestMethod]
        public void DisjointLinkedPair_WithoutVirtualOverlap_SetsUntiledLinkedFlag()
        {
            BajajGeneratorMesh mesh = new(DisjointLinkedPairTopology());
            BajajMeshGenerator.GenerateFaces(mesh);

            Assert.AreEqual(0, mesh.CountFacesSpanning(0, 1),
                "Fixture: suppressed VO must leave the linked pair untiled.");
            Assert.IsTrue(mesh.HasUntiledLinkedPairs);
            Assert.IsTrue(mesh.GenerationHadErrors);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, mesh.UntiledLinkedShapeIndices.ToArray());
            Assert.IsTrue(mesh.GenerationErrors.Any(e => e.Contains("200/201") || e.Contains("untiled linked")),
                $"Expected location-id error text. Errors: {string.Join("; ", mesh.GenerationErrors)}");
        }
    }
}
