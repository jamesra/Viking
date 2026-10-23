using AnnotationVizLib;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnitsAndScale;
using Viking.AnnotationServiceTypes.Interfaces;

namespace AnnotationVizLibTests.FSCheck
{
    /// <summary>
    /// Curvefit at a junction.  A branch is a fitting point only for the chain on its side with exactly one link;
    /// on its side with two or more links its centroid is the merged cross-section of several objects and lies on
    /// none of their centre lines (see <see cref="MorphologyGraph.CurveFitProcesses"/> and CurveFitControlMask).
    /// </summary>
    [TestClass]
    public class CurveFitBranchSpec
    {
        const double HalfWidth = 100.0;
        const double SectionZ = 90.0;
        const double Tolerance = 1e-6;

        static IScale TestScale => new Scale(new AxisUnits(1, "nm"), new AxisUnits(1, "nm"), new AxisUnits(SectionZ, "nm"));

        /// <summary>
        /// Every process is a straight vertical line and the branch sits on the one-link side's line, so nothing is
        /// misplaced and curvefit must move nothing.  Fitting a daughter through the branch pulled it toward the
        /// merged area's centroid.
        /// </summary>
        [TestMethod]
        public void CurveFitProcesses_StraightJunction_MovesNothing() =>
            CurveFitCheck.Run(
                Prop.ForAll(Arb.From(GenJunction()), junction =>
                {
                    MorphologyGraph graph = Build(junction);
                    Dictionary<ulong, (double X, double Y)> before = graph.Nodes.Values.ToDictionary(n => n.Key, n => (n.Center.X, n.Center.Y));
                    MorphologyGraph.CurveFitProcesses(graph, new MorphologyGraph.CurveFitOptions(junction.HalfWindow, null, null));
                    return graph.Nodes.Values.All(n =>
                        Math.Abs(n.Center.X - before[n.Key].X) < Tolerance && Math.Abs(n.Center.Y - before[n.Key].Y) < Tolerance);
                }),
                nameof(CurveFitProcesses_StraightJunction_MovesNothing));

        /// <summary>
        /// InferiorMonkey c1178 (locations 1328723 / 1328721 / 1557441): a thin tip and shaft leave a soma outline.
        /// The soma links down to both the shaft and its own section below, so the process is on the soma's two-link
        /// side.  Fitting through the soma's centroid moved the shaft halfway into the soma and the tip with it.
        /// </summary>
        [TestMethod]
        public void CurveFitProcesses_ProcessLeavingSoma_IsNotPulledIntoIt()
        {
            MorphologyGraph graph = new(1, TestScale);
            Add(graph, 1, 1000, 0, 1);              // tip
            Add(graph, 2, 1000, 0, 2);              // shaft
            Add(graph, 3, 0, 0, 3, halfWidth: 1500); // soma outline the process leaves
            Add(graph, 4, 0, 0, 4, halfWidth: 1500); // soma above
            Add(graph, 5, 0, 0, 2, halfWidth: 1500); // soma below, same section as the shaft
            Link(graph, 1, 2);
            Link(graph, 2, 3);
            Link(graph, 3, 4);
            Link(graph, 3, 5);

            MorphologyGraph.CurveFitProcesses(graph);

            Assert.AreEqual(1000, graph.Nodes[2].Center.X, Tolerance, "shaft must not move toward the soma centroid");
            Assert.AreEqual(1000, graph.Nodes[1].Center.X, Tolerance, "tip must not move toward the soma centroid");
        }

        /// <summary>
        /// The parent below a split still fits through the branch: from that side the branch is just the parent's next
        /// section.  Without it the parent's last node would be extrapolated from below alone (to X = 0); with it the
        /// node is pulled toward the line between its neighbours (about X = 100).
        /// </summary>
        [TestMethod]
        public void CurveFitProcesses_ParentBelowSplit_StillFitsThroughBranch()
        {
            MorphologyGraph graph = new(1, TestScale);
            Add(graph, 1, 0, 0, 1);
            Add(graph, 2, 0, 0, 2);
            Add(graph, 3, 0, 0, 3);
            Add(graph, 4, 300, 0, 4);                 // parent's last node, jittered
            Add(graph, 5, 200, 0, 5, halfWidth: 600); // branch: one link below, two above
            Add(graph, 6, -400, 0, 6);
            Add(graph, 7, 800, 0, 6);
            Link(graph, 1, 2);
            Link(graph, 2, 3);
            Link(graph, 3, 4);
            Link(graph, 4, 5);
            Link(graph, 5, 6);
            Link(graph, 5, 7);

            MorphologyGraph.CurveFitProcesses(graph, new MorphologyGraph.CurveFitOptions(1, null, null));

            double x = graph.Nodes[4].Center.X;
            Assert.IsTrue(x > 50 && x < 150, $"parent's last node should fit between section 3 and the branch, got X = {x}");
        }

        static Gen<Junction> GenJunction() =>
            from kind in Gen.Elements(JunctionKind.Split, JunctionKind.Merge, JunctionKind.Cross)
            from oneSideLength in Gen.Choose(2, 7)
            from manySideLength in Gen.Choose(2, 7)
            from halfWindow in Gen.Choose(1, 7)
            from offsets in Gen.ArrayOf(4, Gen.Choose(600, 4000))
            from signs in Gen.ArrayOf(4, Gen.Elements(-1, 1))
            from branchX in Gen.Choose(-2000, 2000)
            from branchY in Gen.Choose(-2000, 2000)
            select new Junction(kind, oneSideLength, manySideLength, halfWindow,
                [.. offsets.Zip(signs, (o, s) => o * s)], branchX, branchY);

        /// <summary>
        /// The branch sits on section <c>B = OneSideLength + 1</c>.  Split: one parent below, two daughters above.
        /// Merge: the mirror image.  Cross: two below and two above, so no side has one link.  The one-link side's
        /// process runs through the branch's XY; every other process starts at its own offset.
        /// </summary>
        static MorphologyGraph Build(Junction j)
        {
            MorphologyGraph graph = new(1, TestScale);
            ulong next = 1;
            int branchSection = j.OneSideLength + 1;
            ulong branch = next++;
            Add(graph, branch, j.BranchX, j.BranchY, branchSection, halfWidth: 600);

            void Chain(double x, double y, int direction, int length)
            {
                ulong previous = branch;
                for (int k = 1; k <= length; k++)
                {
                    ulong id = next++;
                    Add(graph, id, x, y, branchSection + direction * k);
                    Link(graph, previous, id);
                    previous = id;
                }
            }

            int oneDirection = j.Kind == JunctionKind.Merge ? 1 : -1;
            if (j.Kind == JunctionKind.Cross)
            {
                Chain(j.BranchX + j.Offsets[0], j.BranchY, -1, j.OneSideLength);
                Chain(j.BranchX + j.Offsets[1], j.BranchY + 300, -1, j.OneSideLength);
            }
            else
            {
                Chain(j.BranchX, j.BranchY, oneDirection, j.OneSideLength);
            }

            Chain(j.BranchX + j.Offsets[2], j.BranchY, -oneDirection, j.ManySideLength);
            Chain(j.BranchX + j.Offsets[3], j.BranchY - 300, -oneDirection, j.ManySideLength);
            return graph;
        }

        static void Add(MorphologyGraph graph, ulong id, double cx, double cy, int section, double halfWidth = HalfWidth) =>
            graph.AddNode(new MorphologyNode(id, new TestLocation
            {
                ID = id,
                ParentID = 1,
                UnscaledZ = section,
                Z = section * SectionZ,
                TypeCode = LocationType.POLYGON,
                VolumeGeometryWKT = string.Format(CultureInfo.InvariantCulture,
                    "POLYGON(({0} {1}, {2} {1}, {2} {3}, {0} {3}, {0} {1}))",
                    cx - halfWidth, cy - halfWidth, cx + halfWidth, cy + halfWidth)
            }, graph));

        static void Link(MorphologyGraph graph, ulong a, ulong b) => graph.AddEdge(new MorphologyEdge(graph, a, b));

        enum JunctionKind { Split, Merge, Cross }

        sealed class Junction(JunctionKind kind, int oneSideLength, int manySideLength, int halfWindow, int[] offsets, int branchX, int branchY)
        {
            public JunctionKind Kind { get; } = kind;
            public int OneSideLength { get; } = oneSideLength;
            public int ManySideLength { get; } = manySideLength;
            public int HalfWindow { get; } = halfWindow;
            public int[] Offsets { get; } = offsets;
            public int BranchX { get; } = branchX;
            public int BranchY { get; } = branchY;

            public override string ToString() =>
                $"{Kind} one={OneSideLength} many={ManySideLength} hw={HalfWindow} offsets=[{string.Join(",", Offsets)}]";
        }

        sealed class TestLocation : ILocationReadOnly
        {
            public ulong ID { get; init; }
            public ulong ParentID { get; init; }
            public bool Terminal { get; init; }
            public bool OffEdge { get; init; }
            public bool IsVericosityCap { get; init; }
            public bool IsUntraceable { get; init; }
            public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();
            public long UnscaledZ { get; init; }
            public LocationType TypeCode { get; init; }
            public double Z { get; init; }
            public double? Width { get; init; }
            public string MosaicGeometryWKT { get; init; }
            public string VolumeGeometryWKT { get; init; }

            public bool Equals(ILocationReadOnly other) => other != null && ID == other.ID;
        }
    }
}
