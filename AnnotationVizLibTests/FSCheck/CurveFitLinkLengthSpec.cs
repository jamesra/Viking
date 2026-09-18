using AnnotationVizLib;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Globalization;
using UnitsAndScale;
using Viking.AnnotationServiceTypes.Interfaces;

namespace AnnotationVizLibTests.FSCheck
{
    /// <summary>
    /// Property: <see cref="MorphologyGraph.CurveFitProcesses"/> must not increase the sum of
    /// XY lengths of LocationLinks. One-off RC1 IDs are not a substitute for this family of graphs.
    /// </summary>
    [TestClass]
    public class CurveFitLinkLengthSpec
    {
        const double HalfWidth = 100.0;
        const double SectionZ = 90.0;
        const double StepX = 200.0;

        static IScale TestScale => new Scale(new AxisUnits(1, "nm"), new AxisUnits(1, "nm"), new AxisUnits(SectionZ, "nm"));

        [TestMethod]
        public void CurveFitProcesses_DoesNotIncreaseLocationLinkLengthSum() =>
            CurveFitCheck.Run(
                Prop.ForAll(ArbProcessGraph(), spec =>
                {
                    MorphologyGraph graph = Build(spec);
                    double before = MorphologyGraph.SumLocationLinkLengths(graph);
                    MorphologyGraph.CurveFitProcesses(graph, spec.Options());
                    double after = MorphologyGraph.SumLocationLinkLengths(graph);
                    return after <= before + 1e-3;
                }),
                nameof(CurveFitProcesses_DoesNotIncreaseLocationLinkLengthSum));

        [TestMethod]
        public void CurveFitProcesses_SecondPassDoesNotIncreaseLocationLinkLengthSum() =>
            CurveFitCheck.Run(
                Prop.ForAll(ArbProcessGraph(), spec =>
                {
                    MorphologyGraph graph = Build(spec);
                    MorphologyGraph.CurveFitProcesses(graph, spec.Options());
                    double afterFirst = MorphologyGraph.SumLocationLinkLengths(graph);
                    MorphologyGraph.CurveFitProcesses(graph, spec.Options());
                    double afterSecond = MorphologyGraph.SumLocationLinkLengths(graph);
                    return afterSecond <= afterFirst + 1e-3;
                }),
                nameof(CurveFitProcesses_SecondPassDoesNotIncreaseLocationLinkLengthSum));

        static Arbitrary<ProcessGraphCase> ArbProcessGraph() => Arb.From(GenCase());

        static Gen<ProcessGraphCase> GenCase() =>
            from length in Gen.Choose(3, 12)
            from halfWindow in Gen.Choose(1, 7)
            from jitterX in Gen.ArrayOf(length, Gen.Choose(-80, 80))
            from jitterY in Gen.ArrayOf(length, Gen.Choose(-80, 80))
            from branchMode in Gen.Choose(0, 2)
            from branchAt in Gen.Choose(0, length - 1)
            from branchDx in Gen.Choose(-50, 50)
            from branchDy in Gen.Choose(-50, 50)
            from maxOffsetChoice in Gen.Choose(0, 4)
            select new ProcessGraphCase(
                length,
                halfWindow,
                jitterX,
                jitterY,
                branchMode == 0 ? -1 : branchAt,
                branchMode == 2,
                branchDx,
                branchDy,
                maxOffsetChoice == 0 ? null : maxOffsetChoice * 80.0);

        static MorphologyGraph Build(ProcessGraphCase spec)
        {
            MorphologyGraph graph = new(1, TestScale);
            for (int i = 0; i < spec.Length; i++)
            {
                ulong id = (ulong)(i + 1);
                double x = i * StepX + spec.JitterX[i];
                double y = spec.JitterY[i];
                graph.AddNode(new MorphologyNode(id, SquareLocation(id, x, y, i + 1), graph));
            }

            for (ulong i = 1; i < (ulong)spec.Length; i++)
                graph.AddEdge(new MorphologyEdge(graph, i, i + 1));

            if (spec.BranchAt >= 0)
            {
                ulong parentId = (ulong)(spec.BranchAt + 1);
                MorphologyNode parent = graph.Nodes[parentId];
                int parentSection = spec.BranchAt + 1;
                int branchSection = spec.BranchSameSection ? parentSection : parentSection + 1;
                const ulong branchId = 1000;
                graph.AddNode(new MorphologyNode(
                    branchId,
                    SquareLocation(branchId, parent.Center.X + spec.BranchDx, parent.Center.Y + spec.BranchDy, branchSection),
                    graph));
                graph.AddEdge(new MorphologyEdge(graph, parentId, branchId));
            }

            return graph;
        }

        static TestLocation SquareLocation(ulong id, double cx, double cy, int section)
        {
            double z = section * SectionZ;
            return new TestLocation
            {
                ID = id,
                ParentID = 1,
                UnscaledZ = section,
                Z = z,
                TypeCode = LocationType.POLYGON,
                VolumeGeometryWKT = string.Format(CultureInfo.InvariantCulture,
                    "POLYGON(({0} {1}, {2} {1}, {2} {3}, {0} {3}, {0} {1}))",
                    cx - HalfWidth, cy - HalfWidth, cx + HalfWidth, cy + HalfWidth)
            };
        }

        sealed class ProcessGraphCase(
            int length,
            int halfWindow,
            int[] jitterX,
            int[] jitterY,
            int branchAt,
            bool branchSameSection,
            int branchDx,
            int branchDy,
            double? maxOffsetNm)
        {
            public int Length { get; } = length;
            public int HalfWindow { get; } = halfWindow;
            public int[] JitterX { get; } = jitterX;
            public int[] JitterY { get; } = jitterY;
            public int BranchAt { get; } = branchAt;
            public bool BranchSameSection { get; } = branchSameSection;
            public int BranchDx { get; } = branchDx;
            public int BranchDy { get; } = branchDy;
            public double? MaxOffsetNm { get; } = maxOffsetNm;

            public MorphologyGraph.CurveFitOptions Options() => new(HalfWindow, MaxOffsetNm, null);

            public override string ToString() =>
                $"len={Length} hw={HalfWindow} branch={BranchAt} sameSec={BranchSameSection} cap={MaxOffsetNm}";
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
