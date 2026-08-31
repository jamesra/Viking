using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Covers the deletion predicate used by RemoveInvalidEdges.
    ///
    /// Edges minted implicitly by AddFace carry EdgeType.UNKNOWN, and ClassifyMeshEdges runs only once, at the end of
    /// AddDelaunayEdges.  Everything a later pass adds therefore stays unclassified.  Deleting on !IsValid() swept
    /// those up along with the affirmatively invalid types, so the whole surface survived only because
    /// RemoveInvalidEdges happens to be called before the face-generating passes rather than after them.
    /// </summary>
    [TestClass]
    public class RemoveInvalidEdgesTests
    {
        private const double LowerZ = 0.0;
        private const double UpperZ = 10.0;

        private static Polygon Square(double halfWidth) =>
            new(
            [
                new Vector2(-halfWidth, -halfWidth),
                new Vector2(halfWidth, -halfWidth),
                new Vector2(halfWidth, halfWidth),
                new Vector2(-halfWidth, halfWidth),
                new Vector2(-halfWidth, -halfWidth),
            ]);

        private static Polyline HorizontalLine(double y, double startX, double endX, int segments)
        {
            Vector2[] pts = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                pts[i] = new Vector2(startX + ((endX - startX) * t), y);
            }

            return new Polyline(pts);
        }

        /// <summary>Vertex pairs the mesh has no edge between, so a test can attach an edge of a chosen type.</summary>
        private static List<(int A, int B)> FreeVertexPairs(BajajGeneratorMesh mesh, int count)
        {
            List<(int, int)> pairs = [];
            for (int a = 0; a < mesh.Vertices.Count && pairs.Count < count; a++)
            {
                for (int b = a + 1; b < mesh.Vertices.Count && pairs.Count < count; b++)
                {
                    if (mesh.Contains(a, b) == false)
                        pairs.Add((a, b));
                }
            }

            Assert.AreEqual(count, pairs.Count, $"Fixture only offered {pairs.Count} of the {count} unconnected vertex pairs the test needs.");
            return pairs;
        }

        /// <summary>
        /// The stacked ribbon's tiling is added after ClassifyMeshEdges has already run, so its chords are UNKNOWN.
        /// Deleting on !IsValid() took every face with them.
        /// </summary>
        [TestMethod]
        public void RemoveInvalidEdges_AfterChordGeneration_KeepsRibbonTiling()
        {
            BajajGeneratorMesh mesh = new([HorizontalLine(0, 0, 30, 3), HorizontalLine(5, 0, 30, 3)], [LowerZ, UpperZ], [false, true]);
            BajajMeshGenerator.GenerateFaces(mesh);

            int facesBefore = mesh.Faces.Count;
            Assert.IsTrue(facesBefore > 0, "The ribbon should have tiled before the cleanup pass runs.");

            mesh.RemoveInvalidEdges();

            Assert.AreEqual(facesBefore, mesh.Faces.Count,
                $"RemoveInvalidEdges deleted ribbon faces ({facesBefore} -> {mesh.Faces.Count}).");
        }

        /// <summary>
        /// The same hazard on polygons, which is what shows it was never polyline-specific: stacked squares also tile
        /// after classification and also lost every face.
        /// </summary>
        [TestMethod]
        public void RemoveInvalidEdges_AfterChordGeneration_KeepsPolygonTiling()
        {
            BajajGeneratorMesh mesh = new([Square(10), Square(10)], [LowerZ, UpperZ], [false, true]);
            BajajMeshGenerator.GenerateFaces(mesh);

            int facesBefore = mesh.Faces.Count;
            Assert.IsTrue(facesBefore > 0, "Stacked squares should have tiled before the cleanup pass runs.");

            mesh.RemoveInvalidEdges();

            Assert.AreEqual(facesBefore, mesh.Faces.Count,
                $"RemoveInvalidEdges deleted stacked-square faces ({facesBefore} -> {mesh.Faces.Count}).");
        }

        /// <summary>
        /// The delete set was narrowed by exactly one member, UNKNOWN.  Every type that affirmatively rules an edge
        /// off the surface must still be removed, including the region scaffolding types, or Delaunay leftovers reach
        /// the final mesh.
        /// </summary>
        [TestMethod]
        public void RemoveInvalidEdges_StillDeletesAffirmativelyInvalidTypes()
        {
            EdgeType[] doomed =
            [
                EdgeType.INVALID,
                EdgeType.FLIPPED_DIRECTION,
                EdgeType.FLAT,
                EdgeType.FLYING,
                EdgeType.INTERNAL,
                EdgeType.INVAGINATION,
                EdgeType.HOLE,
                EdgeType.UNTILED,
            ];

            BajajGeneratorMesh mesh = new([Square(10), Square(8)], [LowerZ, UpperZ], [false, true]);
            List<(int A, int B)> pairs = FreeVertexPairs(mesh, doomed.Length);

            for (int i = 0; i < doomed.Length; i++)
                mesh.AddEdge(new MorphMeshEdge(doomed[i], pairs[i].A, pairs[i].B));

            mesh.RemoveInvalidEdges();

            for (int i = 0; i < doomed.Length; i++)
            {
                Assert.IsFalse(mesh.Contains(pairs[i].A, pairs[i].B),
                    $"{doomed[i]} edge ({pairs[i].A},{pairs[i].B}) survived and must not have.");
            }
        }

        /// <summary>
        /// An unclassified edge records that no pass reached a decision, which is not the same as a decision to drop
        /// it.  It stays, and the count is traced instead.
        /// </summary>
        [TestMethod]
        public void RemoveInvalidEdges_LeavesUnclassifiedEdgeInPlace()
        {
            BajajGeneratorMesh mesh = new([Square(10), Square(8)], [LowerZ, UpperZ], [false, true]);
            (int A, int B) pair = FreeVertexPairs(mesh, 1)[0];

            mesh.AddEdge(new MorphMeshEdge(EdgeType.UNKNOWN, pair.A, pair.B));

            mesh.RemoveInvalidEdges();

            Assert.IsTrue(mesh.Contains(pair.A, pair.B),
                $"The UNKNOWN edge ({pair.A},{pair.B}) was deleted; unclassified is not the same as invalid.");
        }

        /// <summary>
        /// Pins both halves of the decision: the predicate differs from !IsValid() for UNKNOWN and nothing else, and
        /// UNKNOWN stays at zero.  Giving UNKNOWN its own bit would leave zero unnamed, and an unnamed zero satisfies
        /// "not UNKNOWN and not valid", so it would be silently deleted - the very hazard this predicate removes.
        /// </summary>
        [TestMethod]
        public void IsAffirmativelyInvalid_ExcludesOnlyUnknown()
        {
            Assert.AreEqual(default(EdgeType), EdgeType.UNKNOWN,
                "UNKNOWN must stay at zero so no unnamed zero value can exist.");

            foreach (EdgeType type in Enum.GetValues<EdgeType>())
            {
                bool differs = type.IsAffirmativelyInvalid() != !type.IsValid();

                if (type == EdgeType.UNKNOWN)
                {
                    Assert.IsTrue(differs, "UNKNOWN is the one type the predicate must treat differently from !IsValid().");
                    Assert.IsFalse(type.IsAffirmativelyInvalid(), "UNKNOWN must never be affirmatively invalid.");
                }
                else
                {
                    Assert.IsFalse(differs, $"{type} must be treated exactly as !IsValid() does, but the predicate disagreed.");
                }
            }
        }
    }
}
