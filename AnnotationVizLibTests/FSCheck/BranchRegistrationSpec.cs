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
    /// Property: a branch never feeds the registration estimate.  Every process of a Y split here is a straight
    /// line in Z, so perfectly registered sections must produce zero second differences and zero residuals.  The
    /// branch node sits off the daughters' lines (it is the merged area) and, by a random lobe shift, off the parent's
    /// line too, so any triple or curve that still runs through it reports a misregistration that is not there.
    /// </summary>
    [TestClass]
    public class BranchRegistrationSpec
    {
        const double HalfWidth = 100.0;
        const double SectionZ = 90.0;
        const double Tolerance = 1e-6;

        static IScale TestScale => new Scale(new AxisUnits(1, "nm"), new AxisUnits(1, "nm"), new AxisUnits(SectionZ, "nm"));

        [TestMethod]
        public void CollectTriples_StraightYSplit_HasNoSecondDifference() =>
            CurveFitCheck.Run(
                Prop.ForAll(Arb.From(GenSplit()), split =>
                {
                    List<ProcessTriple> triples = JointRegistrationSolver.CollectTriples([Build(split)]);
                    return triples.Count > 0 && triples.All(t => t.SecondDifference.Magnitude < Tolerance);
                }),
                nameof(CollectTriples_StraightYSplit_HasNoSecondDifference));

        [TestMethod]
        public void CollectResiduals_StraightYSplit_HasNoResidual() =>
            CurveFitCheck.Run(
                Prop.ForAll(Arb.From(GenSplit()), split =>
                {
                    MorphologyGraph graph = Build(split);
                    List<NeighborResidualField.ResidualSample> residuals = NeighborResidualField.CollectResiduals([graph]);

                    // Process tips are excluded: a one-sided prediction clamps to the nearest neighbour instead of
                    // extrapolating, so a tip on a sloped process always carries a residual.  That limitation predates
                    // and is unrelated to branches; every other node of a straight process must read zero.
                    return residuals
                        .Where(r => !graph.Nodes[r.LocationID].IsProcessTerminal())
                        .All(r => Math.Abs(r.DX) < Tolerance && Math.Abs(r.DY) < Tolerance);
                }),
                nameof(CollectResiduals_StraightYSplit_HasNoResidual));

        static Gen<YSplit> GenSplit() =>
            from parentLength in Gen.Choose(3, 8)
            from daughterLength in Gen.Choose(3, 8)
            from parentSlope in Gen.ArrayOf(2, Gen.Choose(-60, 60))
            from lobeShift in Gen.ArrayOf(2, Gen.Choose(-1500, 1500))
            from gap in Gen.Choose(1000, 4000)
            from slopeD in Gen.ArrayOf(2, Gen.Choose(-60, 60))
            from slopeE in Gen.ArrayOf(2, Gen.Choose(-60, 60))
            select new YSplit(parentLength, daughterLength, parentSlope, lobeShift, gap, slopeD, slopeE);

        /// <summary>
        /// Parent on sections 1..P, the merged area L on P+1, daughters D and E on P+2..P+1+N.
        /// </summary>
        static MorphologyGraph Build(YSplit s)
        {
            MorphologyGraph graph = new(1, TestScale);
            ulong nextId = 1;

            ulong Add(double x, double y, int section)
            {
                ulong id = nextId++;
                graph.AddNode(new MorphologyNode(id, SquareLocation(id, x, y, section), graph));
                return id;
            }

            ulong previous = 0;
            for (int section = 1; section <= s.ParentLength; section++)
            {
                ulong id = Add(section * s.ParentSlope[0], section * s.ParentSlope[1], section);
                if (previous != 0)
                    graph.AddEdge(new MorphologyEdge(graph, previous, id));
                previous = id;
            }

            int branchSection = s.ParentLength + 1;
            double lx = branchSection * s.ParentSlope[0] + s.LobeShift[0];
            double ly = branchSection * s.ParentSlope[1] + s.LobeShift[1];
            ulong branch = Add(lx, ly, branchSection);
            graph.AddEdge(new MorphologyEdge(graph, previous, branch));

            foreach ((double offset, int[] slope) in new[] { (-s.Gap / 2.0, s.SlopeD), (s.Gap / 2.0, s.SlopeE) })
            {
                previous = branch;
                for (int k = 1; k <= s.DaughterLength; k++)
                {
                    ulong id = Add(lx + offset + k * slope[0], ly + k * slope[1], branchSection + k);
                    graph.AddEdge(new MorphologyEdge(graph, previous, id));
                    previous = id;
                }
            }

            return graph;
        }

        static TestLocation SquareLocation(ulong id, double cx, double cy, int section) => new()
        {
            ID = id,
            ParentID = 1,
            UnscaledZ = section,
            Z = section * SectionZ,
            TypeCode = LocationType.POLYGON,
            VolumeGeometryWKT = string.Format(CultureInfo.InvariantCulture,
                "POLYGON(({0} {1}, {2} {1}, {2} {3}, {0} {3}, {0} {1}))",
                cx - HalfWidth, cy - HalfWidth, cx + HalfWidth, cy + HalfWidth)
        };

        sealed class YSplit(int parentLength, int daughterLength, int[] parentSlope, int[] lobeShift, int gap, int[] slopeD, int[] slopeE)
        {
            public int ParentLength { get; } = parentLength;
            public int DaughterLength { get; } = daughterLength;
            public int[] ParentSlope { get; } = parentSlope;
            public int[] LobeShift { get; } = lobeShift;
            public int Gap { get; } = gap;
            public int[] SlopeD { get; } = slopeD;
            public int[] SlopeE { get; } = slopeE;

            public override string ToString() =>
                $"parent={ParentLength} daughters={DaughterLength} lobe=({LobeShift[0]},{LobeShift[1]}) gap={Gap}";
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
