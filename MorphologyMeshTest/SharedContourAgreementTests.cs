using AnnotationVizLib;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitsAndScale;
using Viking.AnnotationServiceTypes.Interfaces;

namespace MorphologyMeshTest
{
    /// <summary>
    /// A contour between two sections belongs to both of the slices either side of it, and the composite mesh welds
    /// those slices together keyed on (morph node, vertex index).  An index only refers to the same point in both
    /// slices if they list the same verticies in the same order, so the two slices must agree on the contour's vertex
    /// list.  When they do not, the shared seam is left open and shows up as a gap in the rendered surface.
    ///
    /// Correspondence inserts verticies where shapes intersect, and it has to run after virtual overlap because
    /// non-overlapping shapes do not intersect until they are moved.  Moving a shape yields a private translated
    /// copy, which is where the two slices used to drift apart.
    /// </summary>
    [TestClass]
    public class SharedContourAgreementTests
    {
        const double SectionThickness = 90.0;

        static IScale TestScale => new Scale(new AxisUnits(1, "nm"), new AxisUnits(1, "nm"), new AxisUnits(SectionThickness, "nm"));

        /// <summary>
        /// Four sections where the third contour sits far from the second, so the slice between them cannot tile
        /// until virtual overlap moves the upper shape.  That moved shape is the third contour, which the slice above
        /// also uses - the arrangement that made the two slices disagree about it.
        ///
        /// The contours are offset from one another rather than stacked so correspondence has intersections to insert
        /// verticies at, which is what makes the vertex lists differ in the first place.
        /// </summary>
        static MorphologyGraph BuildChainWithADistantSection()
        {
            MorphologyGraph graph = new(9100, TestScale);

            graph.AddNode(new MorphologyNode(1, PolygonLocation(1, Square(0, 0, 100), 1), graph));
            graph.AddNode(new MorphologyNode(2, PolygonLocation(2, Square(50, 0, 100), 2), graph));
            graph.AddNode(new MorphologyNode(3, PolygonLocation(3, Square(600, 0, 100), 3), graph));
            graph.AddNode(new MorphologyNode(4, PolygonLocation(4, Square(640, 20, 100), 4), graph));

            graph.AddEdge(new MorphologyEdge(graph, 1, 2));
            graph.AddEdge(new MorphologyEdge(graph, 2, 3));
            graph.AddEdge(new MorphologyEdge(graph, 3, 4));

            return graph;
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task SlicesSharingAContourAgreeOnItsVerticies()
        {
            SliceGraph slices = await SliceGraph.Create(BuildChainWithADistantSection(), 2.0);

            //morph node -> the vertex list each slice using it presented
            Dictionary<ulong, List<(ulong Slice, string Signature)>> byNode = [];
            bool anyVirtualOverlap = false;

            foreach (ulong key in slices.Nodes.Keys)
            {
                SliceTopology topology = slices.GetTopology(key);
                if (topology.Shapes is null || topology.ShapeIndexToMorphNodeIndex is null)
                    continue;

                for (int i = 0; i < topology.Shapes.Length; i++)
                {
                    anyVirtualOverlap |= topology.GetVirtualOverlapOffset(i) != Vector2.Zero;

                    ulong node = topology.ShapeIndexToMorphNodeIndex[i];
                    if (byNode.TryGetValue(node, out var seen) == false)
                        byNode[node] = seen = [];

                    seen.Add((key, Signature(topology.Shapes[i], topology.GetVirtualOverlapOffset(i))));
                }
            }

            Assert.IsTrue(anyVirtualOverlap,
                "The distant third section should force a virtual overlap translation, otherwise this fixture is not exercising the case under test.");

            var shared = byNode.Where(kv => kv.Value.Select(v => v.Slice).Distinct().Count() > 1).ToList();
            Assert.IsTrue(shared.Count > 0, "The chain should share contours between adjacent slices.");

            foreach (var kv in shared)
            {
                string[] distinct = [.. kv.Value.Select(v => v.Signature).Distinct()];
                if (distinct.Length == 1)
                    continue;

                Assert.Fail(
                    $"Contour {kv.Key} is used by slices {string.Join(", ", kv.Value.Select(v => v.Slice))} and they disagree about its verticies, " +
                    $"so the composite cannot weld the seam between them.\n{DescribeDisagreement(distinct)}");
            }
        }

        /// <summary>
        /// Reports the vertex counts and the first index where two vertex lists differ, which distinguishes a slice
        /// that inserted extra verticies from one that listed the same points starting at a different index.
        /// </summary>
        static string DescribeDisagreement(string[] signatures)
        {
            StringBuilder sb = new();

            string[][] lists = [.. signatures.Select(s => s.Split(';'))];
            sb.AppendLine($"  vertex counts: {string.Join(" vs ", lists.Select(l => l.Length))}");

            int shortest = lists.Min(l => l.Length);
            int firstDifference = -1;
            for (int i = 0; i < shortest && firstDifference < 0; i++)
            {
                if (lists.Select(l => l[i]).Distinct().Count() > 1)
                    firstDifference = i;
            }

            sb.AppendLine($"  first differing index: {(firstDifference < 0 ? "none (one list is a prefix of the other)" : firstDifference.ToString())}");

            //Distinguishes a genuine difference in which points exist from the same points listed in a different
            //rotation or direction, which are separate defects with separate causes.
            bool sameSet = lists.Select(l => string.Join("|", l.OrderBy(v => v))).Distinct().Count() == 1;
            bool reversed = lists.Length == 2
                            && lists[0].Length == lists[1].Length
                            && lists[0].SequenceEqual(lists[1].Reverse());

            sb.AppendLine($"  same point set: {sameSet}   one is the reverse of the other: {reversed}");

            int from = System.Math.Max(0, firstDifference < 0 ? 0 : firstDifference - 1);
            foreach (string[] list in lists)
                sb.AppendLine($"    [{from}..]: {string.Join(" ", list.Skip(from).Take(4))}");

            return sb.ToString();
        }

        /// <summary>
        /// The contour's verticies in their untranslated positions and in index order.  Virtual overlap is subtracted
        /// out because <c>working = original.Translate(offset)</c> and the mesh restores that translation afterwards,
        /// so a shape a slice moved should compare equal to the same contour in a slice that left it alone.
        /// </summary>
        static string Signature(IShape2D shape, Vector2 virtualOverlapOffset)
        {
            Vector2[] points = shape switch
            {
                Polygon p => p.ExteriorRing,
                Polyline l => [.. l.Points.Select(pt => new Vector2(pt.X, pt.Y))],
                _ => []
            };

            return string.Join(";", points.Select(pt =>
                $"{pt.X - virtualOverlapOffset.X:F3},{pt.Y - virtualOverlapOffset.Y:F3}"));
        }

        static Polygon Square(double x, double y, double size) =>
            new(new Vector2[]
            {
                new(x, y),
                new(x + size, y),
                new(x + size, y + size),
                new(x, y + size),
                new(x, y)
            });

        static TestLocation PolygonLocation(ulong id, Polygon shape, int section) =>
            new()
            {
                ID = id,
                ParentID = 1,
                UnscaledZ = section,
                Z = section * SectionThickness,
                TypeCode = LocationType.POLYGON,
                VolumeGeometryWKT = ToWkt(shape)
            };

        static string ToWkt(Polygon shape)
        {
            StringBuilder sb = new("POLYGON(");
            sb.Append(RingWkt(shape.ExteriorRing));
            sb.Append(')');
            return sb.ToString();
        }

        static string RingWkt(IReadOnlyList<Vector2> ring) =>
            "(" + string.Join(", ", ring.Select(p => string.Format(CultureInfo.InvariantCulture, "{0} {1}", p.X, p.Y))) + ")";
    }
}
