using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// Pins <see cref="VolumeToSectionMappingExtensions.TryMapShapeSectionToVolume(IVolumeToSectionTransform, Polygon)"/> and
    /// <see cref="VolumeToSectionMappingExtensions.TryMapShapeVolumeToSection(IVolumeToSectionTransform, Polygon)"/>:
    /// a polygon is unmappable (null) when any vertex of its exterior ring or of any interior ring cannot be mapped,
    /// and a fully mappable polygon keeps its holes.
    /// </summary>
    [TestClass]
    public class TryMapShapeInteriorRingTests
    {
        /// <summary>Identity transform, in both directions, that can only map points for which <c>canMap</c> is true.</summary>
        private sealed class MaskedIdentityTransform(Func<Vector2, bool> canMap) : IVolumeToSectionTransform
        {
            public long ID => 0;
            public Rectangle? SectionBounds => null;
            public Rectangle? VolumeBounds => null;

            public bool TryVolumeToSection(Vector2 P, out Vector2 transformedP)
            {
                transformedP = P;
                return canMap(P);
            }

            public bool[] TryVolumeToSection(in Vector2[] Points, out Vector2[] transformedP)
            {
                transformedP = [.. Points];
                return [.. Points.Select(canMap)];
            }

            public bool TrySectionToVolume(Vector2 P, out Vector2 transformedP) => TryVolumeToSection(P, out transformedP);
            public bool[] TrySectionToVolume(in Vector2[] Points, out Vector2[] transformedP) => TryVolumeToSection(Points, out transformedP);
            public Vector2 SectionToVolume(Vector2 P) => throw new NotSupportedException();
            public Vector2 VolumeToSection(Vector2 P) => throw new NotSupportedException();
            public Vector2[] SectionToVolume(Vector2[] Points) => throw new NotSupportedException();
            public Vector2[] VolumeToSection(Vector2[] Points) => throw new NotSupportedException();
        }

        /// <summary>A vertex of the hole only; every exterior vertex maps.</summary>
        private static readonly Vector2 UnmappableHoleVertex = new(40_000, 40_000);

        /// <summary>Square at volume-scale coordinates with a square hole.</summary>
        private static Polygon SquareWithHole()
        {
            Polygon shape = new([new Vector2(0, 0), new Vector2(100_000, 0), new Vector2(100_000, 100_000), new Vector2(0, 100_000), new Vector2(0, 0)]);
            shape.AddInteriorRing([UnmappableHoleVertex, new Vector2(60_000, 40_000), new Vector2(60_000, 60_000), new Vector2(40_000, 60_000), UnmappableHoleVertex]);
            return shape;
        }

        private static MaskedIdentityTransform AllButHoleVertex() => new(p => p != UnmappableHoleVertex);

        [TestMethod]
        public void SectionToVolume_UnmappableHoleVertex_ReturnsNull()
        {
            Assert.IsNull(AllButHoleVertex().TryMapShapeSectionToVolume(SquareWithHole()));
        }

        [TestMethod]
        public void VolumeToSection_UnmappableHoleVertex_ReturnsNull()
        {
            Assert.IsNull(AllButHoleVertex().TryMapShapeVolumeToSection(SquareWithHole()));
        }

        [TestMethod]
        public void SectionToVolume_FullyMappable_KeepsHole()
        {
            Polygon shape = SquareWithHole();
            Polygon mapped = new MaskedIdentityTransform(_ => true).TryMapShapeSectionToVolume(shape);

            AssertSameRings(shape, mapped);
        }

        [TestMethod]
        public void VolumeToSection_FullyMappable_KeepsHole()
        {
            Polygon shape = SquareWithHole();
            Polygon mapped = new MaskedIdentityTransform(_ => true).TryMapShapeVolumeToSection(shape);

            AssertSameRings(shape, mapped);
        }

        [TestMethod]
        public void UnmappableExteriorVertex_ReturnsNull()
        {
            MaskedIdentityTransform mapper = new(p => p != new Vector2(100_000, 0));

            Assert.IsNull(mapper.TryMapShapeSectionToVolume(SquareWithHole()));
            Assert.IsNull(mapper.TryMapShapeVolumeToSection(SquareWithHole()));
        }

        private static void AssertSameRings(Polygon expected, Polygon actual)
        {
            Assert.IsNotNull(actual);
            CollectionAssert.AreEqual(expected.ExteriorRing, actual.ExteriorRing);
            Assert.AreEqual(1, actual.InteriorPolygons.Count);
            CollectionAssert.AreEqual(expected.InteriorPolygons[0].ExteriorRing, actual.InteriorPolygons[0].ExteriorRing);
        }
    }
}
