using AnnotationVizLib;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;

namespace MorphologyMeshTest
{
    /// <summary>
    /// LiveData probe: unique verts / perimeter before and after contour simplify for the
    /// BAJAJMULTITEST long-pole slices (RC1 410 and RPC1 2628).
    /// </summary>
    [TestClass]
    public class ContourSimplifyLongPoleProbe
    {
        static readonly ContourSimplifyOptions DefaultOpts = ContourSimplifyOptions.Default; // adaptive 50↔20, tol 10
        static readonly ContourSimplifyOptions LegacyDefault = new(20.0, 10.0);
        static readonly ContourSimplifyOptions Always10 = ContourSimplifyOptions.Always(10.0);
        static readonly ContourSimplifyOptions Always50 = ContourSimplifyOptions.Always(50.0);

        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(600000)]
        public async Task ProbeRc1_410_LongPoleLocations()
        {
            await ProbeSlice(
                "RC1 structure ~410 long-pole",
                Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RC1],
                [200625, 200626, 201467, 1420185]);
        }

        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(600000)]
        public async Task ProbeRpc1_2628_LongPoleLocations()
        {
            await ProbeSlice(
                "RPC1 structure ~2628 long-pole",
                Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RPC1],
                [98942, 98943, 101061, 101147, 101150, 101220, 101221]);
        }

        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(900000)]
        public async Task TimeCostVsSavings_Rc1_410_LongPole()
        {
            await TimeCostVsSavings(
                "RC1 410 long-pole",
                Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RC1],
                [200625, 200626, 201467, 1420185]);
        }

        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(600000)]
        public async Task AbSimplifyDefaults_OnFailingDifficultCases()
        {
            (string Name, ulong[] Ids)[] cases =
            [
                ("368094,368100", [368094UL, 368100UL]),
                ("140224,140233", [140224UL, 140233UL]),
                ("82605,82606", [82605UL, 82606UL]),
            ];
            Uri endpoint = Viking.Common.ODataEndpointCatalog.EndpointMap[Viking.Common.Endpoint.RPC1];
            ContourSimplifyOptions legacy = new(20.0, 10.0);
            ContourSimplifyOptions current = ContourSimplifyOptions.Default;

            foreach ((string name, ulong[] ids) in cases)
            {
                MorphologyGraph morph;
                try
                {
                    morph = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                        [.. ids.Select(id => (long)id)], endpoint, 3);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{name}: LOAD FAIL {e.GetType().Name}: {e.Message}");
                    continue;
                }

                foreach ((string label, ContourSimplifyOptions opts, bool curveFit) in new (string, ContourSimplifyOptions, bool)[]
                {
                    ("legacy-raw", legacy, false),
                    ("legacy-curvefit", legacy, true),
                    ("default50/20-raw", current, false),
                    ("default50/20-curvefit", current, true),
                })
                {
                    // Reload-free: clone geometry by re-fetch is expensive; CurveFit mutates, so re-fetch per curvefit.
                    MorphologyGraph graph = curveFit
                        ? await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                            [.. ids.Select(id => (long)id)], endpoint, 3)
                        : morph;
                    if (curveFit)
                        MorphologyGraph.CurveFitProcesses(graph);

                    try
                    {
                        SliceGraph slices = await SliceGraph.Create(graph, opts);
                        Slice target = slices.Nodes.Values.FirstOrDefault(s => ids.All(id => s.AllNodes.Contains(id)));
                        if (target is null)
                        {
                            Console.WriteLine($"{name} [{label}]: no slice");
                            continue;
                        }

                        if (slices.FailedTopologySlices.TryGetValue(target.Key, out var topoFail))
                        {
                            Console.WriteLine($"{name} [{label}]: topology fail {topoFail}");
                            continue;
                        }

                        BajajGeneratorMesh mesh = new(slices.GetTopology(target), target);
                        BajajMeshGenerator.GenerateFaces(mesh);
                        var report = mesh.ManifoldReport;
                        Console.WriteLine(
                            $"{name} [{label}]: verts={mesh.Vertices.Count} faces={mesh.Faces.Count} " +
                            $"ok={report.IsValidSliceSurface && !mesh.GenerationHadErrors} {report}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"{name} [{label}]: EXCEPTION {e.GetType().Name}: {e.Message}");
                    }
                }
            }
        }


        /// <summary>
        /// Wall-clock: (A) ContourSimplify alone, (B) SliceGraph.Create + GenerateFaces for Default / Aggressive / Always(10).
        /// </summary>
        static async Task TimeCostVsSavings(string label, Uri endpoint, ulong[] locationIds)
        {
            Console.WriteLine($"\n=== Cost vs savings: {label} ===");
            MorphologyGraph graph = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                [.. locationIds.Select(id => (long)id)], endpoint, hops: 0);

            (string Name, ContourSimplifyOptions Opts)[] policies =
            [
                ("Legacy(20/10)", LegacyDefault),
                ("Default(50/20)", DefaultOpts),
                ("Always(10)", Always10),
            ];

            Console.WriteLine("\n-- Simplify-only (ToPolygon(0) once, then N× Simplify per policy; ms for all polygons) --");
            List<(ulong Id, Polygon Poly)> rawPolys = [];
            foreach (ulong id in locationIds)
            {
                if (!graph.Nodes.TryGetValue(id, out MorphologyNode node) || node.Geometry is null)
                    continue;
                SupportedGeometryType gtype = node.Geometry.GeometryType();
                if (gtype is not (SupportedGeometryType.POLYGON or SupportedGeometryType.CURVEPOLYGON))
                    continue;
                rawPolys.Add((id, node.Geometry.ToPolygon(0)));
            }

            const int simplifyIters = 5;
            foreach ((string name, ContourSimplifyOptions opts) in policies)
            {
                // Warmup
                foreach ((_, Polygon poly) in rawPolys)
                    _ = Apply(poly, opts);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                int lastVerts = 0;
                for (int i = 0; i < simplifyIters; i++)
                {
                    lastVerts = 0;
                    foreach ((_, Polygon poly) in rawPolys)
                    {
                        Polygon outP = Apply(poly, opts);
                        lastVerts += Measure(outP.ExteriorRing).UniqueVerts
                            + outP.InteriorRings.Sum(r => Measure(r).UniqueVerts);
                    }
                }

                sw.Stop();
                double msPerPass = sw.Elapsed.TotalMilliseconds / simplifyIters;
                Console.WriteLine($"  {name,-20}  {msPerPass,8:F2} ms/pass  → {lastVerts} verts (Σ rings)");
            }

            Console.WriteLine("\n-- Full slice mesh (SliceGraph.Create + GenerateFaces on matching slice) --");
            Console.WriteLine(
                $"{"policy",20} {"create_s",8} {"faces_s",8} {"total_s",8} {"meshVerts",9} {"faces",7} {"ok?",4}");

            foreach ((string name, ContourSimplifyOptions opts) in policies)
            {
                // Fresh graph geometry each policy (Create mutates nothing on morph, but shapes are rebuilt)
                var swCreate = System.Diagnostics.Stopwatch.StartNew();
                SliceGraph slices = await SliceGraph.Create(graph, opts);
                swCreate.Stop();

                Slice target = slices.Nodes.Values.FirstOrDefault(s => locationIds.All(id => s.AllNodes.Contains(id)));
                if (target is null)
                {
                    Console.WriteLine($"  {name,-20}  no slice contains all locations");
                    continue;
                }

                if (slices.FailedTopologySlices.ContainsKey(target.Key))
                {
                    Console.WriteLine($"  {name,-20}  topology failed: {slices.FailedTopologySlices[target.Key]}");
                    continue;
                }

                SliceTopology topology = slices.GetTopology(target);
                BajajGeneratorMesh mesh = new(topology, target);
                var swFaces = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    BajajMeshGenerator.GenerateFaces(mesh);
                }
                catch (Exception e)
                {
                    swFaces.Stop();
                    Console.WriteLine($"  {name,-20}  GenerateFaces threw: {e.Message}");
                    continue;
                }

                swFaces.Stop();
                double total = swCreate.Elapsed.TotalSeconds + swFaces.Elapsed.TotalSeconds;
                bool ok = !mesh.GenerationHadErrors && mesh.ManifoldReport.IsValidSliceSurface;
                Console.WriteLine(
                    $"  {name,-20} {swCreate.Elapsed.TotalSeconds,8:F2} {swFaces.Elapsed.TotalSeconds,8:F2} {total,8:F2} {mesh.Vertices.Count,9} {mesh.Faces.Count,7} {(ok ? "yes" : "NO"),4}");
            }
        }


        static async Task ProbeSlice(string label, Uri endpoint, ulong[] locationIds)
        {
            Console.WriteLine($"\n=== {label} ===");
            Console.WriteLine($"Endpoint: {endpoint}");
            Console.WriteLine($"Locations: {string.Join(",", locationIds)}");

            MorphologyGraph graph = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataLocationIDsAsync(
                [.. locationIds.Select(id => (long)id)], endpoint, hops: 0);

                Console.WriteLine(
                    $"{"Location",12} {"Type",10} {"rawVerts",8} {"periNm",10} {"nm/vert",8} {"gate50",6} {"defVerts",8} {"def%",7} {"legVerts",8} {"leg%",7} {"a10Verts",8} {"a10%",7} {"a50Verts",8} {"a50%",7}");

            int sumRaw = 0, sumDef = 0, sumLeg = 0, sumA10 = 0, sumA50 = 0;
            foreach (ulong id in locationIds.OrderBy(x => x))
            {
                if (!graph.Nodes.TryGetValue(id, out MorphologyNode node) || node.Geometry is null)
                {
                    Console.WriteLine($"{id,12}  MISSING");
                    continue;
                }

                SupportedGeometryType gtype = node.Geometry.GeometryType();
                if (gtype is SupportedGeometryType.POLYLINE)
                {
                    Polyline line = node.Geometry.ToPolyLine(0);
                    int verts = UniquePolylineVerts(line);
                    Console.WriteLine($"{id,12} {"POLYLINE",10} {verts,8} {"n/a",10} {"n/a",8} {"never",6} {verts,8} {"0%",7} {verts,8} {"0%",7} {verts,8} {"0%",7} {verts,8} {"0%",7}");
                    sumRaw += verts;
                    sumDef += verts;
                    sumLeg += verts;
                    sumA10 += verts;
                    sumA50 += verts;
                    continue;
                }

                if (gtype is not (SupportedGeometryType.POLYGON or SupportedGeometryType.CURVEPOLYGON))
                {
                    Console.WriteLine($"{id,12} {gtype,10}  (skipped)");
                    continue;
                }

                // Match SliceGraph: ToPolygon(preTol) then density-gated Simplify.
                // Raw path uses preTol=0 so only the MaxPolygonRingPoints safety net can reduce.
                Polygon raw = node.Geometry.ToPolygon(0);
                RingStats rawStats = Measure(raw.ExteriorRing);
                bool gateExt = ContourSimplifyOptions.RingExceedsDensity(raw.ExteriorRing, DefaultOpts.MinNmPerVertex);
                bool gateAny = gateExt || raw.InteriorRings.Any(r => ContourSimplifyOptions.RingExceedsDensity(r, DefaultOpts.MinNmPerVertex));
                string gateLabel = gateAny ? (gateExt ? "YES" : "hole") : "no";

                Polygon def = Apply(raw, DefaultOpts);
                Polygon leg = Apply(raw, LegacyDefault);
                Polygon a10 = Apply(raw, Always10);
                Polygon a50 = Apply(raw, Always50);

                RingStats d = Measure(def.ExteriorRing);
                RingStats l = Measure(leg.ExteriorRing);
                RingStats t10 = Measure(a10.ExteriorRing);
                RingStats t50 = Measure(a50.ExteriorRing);

                Console.WriteLine(
                    $"{id,12} {gtype,10} {rawStats.UniqueVerts,8} {rawStats.PerimeterNm,10:F0} {rawStats.NmPerVert,8:F1} {gateLabel,6} " +
                    $"{d.UniqueVerts,8} {Pct(rawStats.UniqueVerts, d.UniqueVerts),7} " +
                    $"{l.UniqueVerts,8} {Pct(rawStats.UniqueVerts, l.UniqueVerts),7} " +
                    $"{t10.UniqueVerts,8} {Pct(rawStats.UniqueVerts, t10.UniqueVerts),7} " +
                    $"{t50.UniqueVerts,8} {Pct(rawStats.UniqueVerts, t50.UniqueVerts),7}");

                if (raw.InteriorRings.Count > 0)
                {
                    foreach (Vector2[] hole in raw.InteriorRings)
                    {
                        RingStats hs = Measure(hole);
                        bool holeGate = ContourSimplifyOptions.RingExceedsDensity(hole, DefaultOpts.MinNmPerVertex);
                        Console.WriteLine($"{ "(hole)",12} {(holeGate ? "gate!" : ""),10} {hs.UniqueVerts,8} {hs.PerimeterNm,10:F0} {hs.NmPerVert,8:F1}");
                    }
                }

                sumRaw += rawStats.UniqueVerts + raw.InteriorRings.Sum(r => Measure(r).UniqueVerts);
                sumDef += CountAll(def);
                sumLeg += CountAll(leg);
                sumA10 += CountAll(a10);
                sumA50 += CountAll(a50);
            }

            Console.WriteLine(
                $"{"SLICE Σ",12} {"",10} {sumRaw,8} {"",10} {"",8} {"",6} {sumDef,8} {Pct(sumRaw, sumDef),7} {sumLeg,8} {Pct(sumRaw, sumLeg),7} {sumA10,8} {Pct(sumRaw, sumA10),7} {sumA50,8} {Pct(sumRaw, sumA50),7}");
            Console.WriteLine(
                "Columns: gate50 = density gate at default 1 vert/50 nm; def = Default(50,20); leg = Legacy(20,10); a10/a50 = Always(10)/Always(50). % = exterior verts remaining.");
        }

        static Polygon Apply(Polygon raw, ContourSimplifyOptions opts)
        {
            double tol = opts.ToleranceFor(raw);
            if (tol <= 0)
                return raw;
            try
            {
                return raw.Simplify(tol);
            }
            catch (ArgumentException)
            {
                return raw;
            }
        }

        static int CountAll(Polygon p) =>
            Measure(p.ExteriorRing).UniqueVerts + p.InteriorRings.Sum(r => Measure(r).UniqueVerts);

        static RingStats Measure(Vector2[] ring)
        {
            if (ring is null || ring.Length < 2)
                return new RingStats(0, 0, 0);

            bool closed = ring[0] == ring[^1];
            int unique = closed ? ring.Length - 1 : ring.Length;
            double peri = ring.PerimeterLength();
            double nmPer = unique > 0 ? peri / unique : 0;
            return new RingStats(unique, peri, nmPer);
        }

        static int UniquePolylineVerts(Polyline line)
        {
            IReadOnlyList<IPoint2D> pts = line.Points;
            if (pts is null || pts.Count == 0)
                return 0;
            bool closed = pts[0].Equals(pts[^1]);
            return closed ? pts.Count - 1 : pts.Count;
        }

        static string Pct(int before, int after) =>
            before <= 0 ? "n/a" : ((100.0 * after) / before).ToString("F0", CultureInfo.InvariantCulture) + "%";

        readonly record struct RingStats(int UniqueVerts, double PerimeterNm, double NmPerVert);
    }
}
