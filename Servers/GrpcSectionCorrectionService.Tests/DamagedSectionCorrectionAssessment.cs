using AnnotationVizLib;
using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Viking.Common;
using Viking.SectionCorrection;
using Viking.SectionCorrectionBuilder;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>
    /// Opt-in measurement, not a regression test: how well each candidate correction of damaged sections (the blocks'
    /// NonStosSectionNumbers) lines a location up with the locations it links to. Every variant starts from the
    /// published field and moves only locations on damaged sections:
    /// <list type="bullet">
    /// <item><b>field</b>: the published field alone (the baseline).</item>
    /// <item><b>curvefit</b>: <see cref="MorphologyGraph.CurveFitProcesses"/> restricted to damaged-section locations, with
    /// damaged-section locations still fitting points.</item>
    /// <item><b>damagedfit</b>: what the service runs from 1.7.0: the same fit, through undamaged-section locations only,
    /// with the window reaching out to that many of them (so runs of damaged sections are bridged).</item>
    /// <item><b>linear</b>: each damaged-section location's centroid moved to the straight line between the nearest
    /// locations of its process on undamaged sections.</item>
    /// </list>
    /// The score is SBFSEMpy's: for each LocationLink with a location on a damaged section and both shapes closed, the
    /// overlap is shared area over the smaller area. No variant optimises that number, so the comparison is not circular.
    /// Set DAMAGED_ASSESS=1 and CORRECTIONS_ROOT to the published catalog; DAMAGED_ASSESS_CELLS caps cells per volume
    /// (default 12).
    /// </summary>
    [TestClass]
    public class DamagedSectionCorrectionAssessment
    {
        sealed record VolumeCase(string Name, Endpoint Endpoint, string VikingXml);

        static readonly VolumeCase[] Volumes =
        [
            new("RPC1", Endpoint.RPC1, "http://rogue1.codepharm.net/RPC1/SliceToVolume.VikingXML"),
            new("RPC2", Endpoint.RPC2, "http://rogue1.codepharm.net/RPC2/SliceToVolume.VikingXML"),
            // The storage host is internal; the same files are on the share.
            new("RC2", Endpoint.RC2, @"Y:\Volumes\RC2\SliceToVolume.VikingXML"),
            new("InferiorMonkey", Endpoint.INFERIORMONKEY, "http://v2486.host.s.uw.edu/Neitz/InferiorMonkey/InferiorMonkey.xml"),
        ];

        static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

        sealed class Tally
        {
            public int Pairs;
            public double Sum;
            public int Gain, Lose, Moved1um;

            public override string ToString() => Pairs == 0
                ? "no pairs"
                : string.Format(CultureInfo.InvariantCulture, "{0} pairs, mean {1:F3}, vs field +0.1/-0.1: {2}/{3}, moved >1 µm beyond the field: {4}",
                    Pairs, Sum / Pairs, Gain, Lose, Moved1um);
        }

        [TestMethod]
        [TestCategory("LiveData")]
        [Timeout(7200000)]
        public async Task CompareDamagedSectionVariants()
        {
            if (Environment.GetEnvironmentVariable("DAMAGED_ASSESS") != "1")
            {
                Assert.Inconclusive("Set DAMAGED_ASSESS=1 (and CORRECTIONS_ROOT) to run the damaged-section assessment.");
                return;
            }

            string root = Environment.GetEnvironmentVariable("CORRECTIONS_ROOT");
            Assert.IsTrue(Directory.Exists(root), $"CORRECTIONS_ROOT '{root}' does not exist");
            int maxCells = int.TryParse(Environment.GetEnvironmentVariable("DAMAGED_ASSESS_CELLS"), out int n) ? n : 12;
            string[] variants = ["field", "curvefit", "damagedfit", "linear"];
            Dictionary<string, Tally> total = variants.ToDictionary(v => v, _ => new Tally());
            List<(string Label, Dictionary<string, Tally> Tallies)> perCell = [];

            foreach (VolumeCase volume in Volumes)
            {
                SortedSet<int> damaged = await DamagedSectionList.LoadAsync(volume.VikingXml, Console.WriteLine, CancellationToken.None);
                if (damaged is null || damaged.Count == 0)
                {
                    Console.WriteLine($"{volume.Name}: no damaged sections; skipped");
                    continue;
                }

                using VolumeCorrectionCatalog catalog = new(root);
                catalog.Load();
                (string volumeName, PublishedCorrectionSet set) = catalog.Sets
                    .FirstOrDefault(s => string.Equals(s.VolumeName, volume.Name, StringComparison.OrdinalIgnoreCase));
                if (set is null)
                {
                    Console.WriteLine($"{volume.Name}: no published set; skipped");
                    continue;
                }

                CorrectionManifestDto manifest = set.Manifest;
                Console.WriteLine($"{volume.Name}: set {set.StosGroup}, builder {manifest.BuilderVersion}, built {manifest.Provenance?.BuiltUtc:u}, " +
                                  $"damaged in set: {(manifest.Provenance?.DamagedSections is { } d ? d.Count.ToString(CultureInfo.InvariantCulture) : "not recorded")}");
                NeighborResidualField field = NeighborResidualField.FromLattice(set.EnumerateLattice(),
                    set.Provenance?.PitchNm > 0 ? set.Provenance.PitchNm : NeighborResidualField.GridSizeNm,
                    set.Provenance?.KernelRadiusNm > 0 ? set.Provenance.KernelRadiusNm : NeighborResidualField.KernelRadiusNm);

                Uri odata = ODataEndpointCatalog.EndpointMap[volume.Endpoint];
                List<ulong> cellIds = await CellsOnDamagedSections(odata, damaged, maxCells);
                Console.WriteLine($"{volume.Name}: {damaged.Count} damaged sections; cells {string.Join(",", cellIds)}");

                foreach (ulong cellId in cellIds)
                {
                    MorphologyGraph rootGraph;
                    try
                    {
                        rootGraph = await AnnotationVizLib.OData.ODataMorphologyFactory.FromODataAsync([(long)cellId], include_children: false, odata);
                    }
                    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TimeoutException)
                    {
                        Console.WriteLine($"  c{cellId}: load failed ({ex.Message})");
                        continue;
                    }

                    Dictionary<string, Tally> cell = Score(rootGraph, field, damaged);
                    perCell.Add(($"{volume.Name} c{cellId}", cell));
                    Console.WriteLine($"  {volume.Name} c{cellId}:");
                    foreach (string v in variants)
                    {
                        Console.WriteLine($"    {v,-9} {cell[v]}");
                        Add(total[v], cell[v]);
                    }
                }
            }

            Console.WriteLine("TOTAL:");
            foreach (string v in variants)
                Console.WriteLine($"  {v,-9} {total[v]}");

            if (perCell.Count > 1)
            {
                (string largest, _) = perCell.MaxBy(c => c.Tallies["field"].Pairs);
                Console.WriteLine($"TOTAL without the largest cell ({largest}):");
                foreach (string v in variants)
                {
                    Tally rest = new();
                    foreach ((string label, Dictionary<string, Tally> tallies) in perCell.Where(c => c.Label != largest))
                        Add(rest, tallies[v]);
                    Console.WriteLine($"  {v,-9} {rest}");
                }

                // Cells where each variant beats the field on mean overlap, to see how widely an effect holds.
                foreach (string v in variants.Skip(1))
                {
                    int better = perCell.Count(c => c.Tallies["field"].Pairs > 0 && c.Tallies[v].Sum > c.Tallies["field"].Sum + 1e-9);
                    int worse = perCell.Count(c => c.Tallies["field"].Pairs > 0 && c.Tallies[v].Sum < c.Tallies["field"].Sum - 1e-9);
                    Console.WriteLine($"  {v}: mean overlap above the field in {better} cells, below in {worse}");
                }
            }
        }

        static void Add(Tally into, Tally from)
        {
            into.Pairs += from.Pairs;
            into.Sum += from.Sum;
            into.Gain += from.Gain;
            into.Lose += from.Lose;
            into.Moved1um += from.Moved1um;
        }

        /// <summary>The cells with the most locations on damaged sections.</summary>
        static async Task<List<ulong>> CellsOnDamagedSections(Uri odata, SortedSet<int> damaged, int maxCells)
        {
            Dictionary<ulong, int> count = [];
            foreach (int z in damaged)
            {
                string url = $"{odata.AbsoluteUri.TrimEnd('/')}/Locations?$filter=Z%20eq%20{z}&$select=ParentID";
                while (url is not null)
                {
                    using JsonDocument doc = JsonDocument.Parse(await Http.GetStringAsync(url));
                    foreach (JsonElement row in doc.RootElement.GetProperty("value").EnumerateArray())
                    {
                        ulong parent = row.GetProperty("ParentID").GetUInt64();
                        count[parent] = count.GetValueOrDefault(parent) + 1;
                    }
                    url = doc.RootElement.TryGetProperty("@odata.nextLink", out JsonElement next) ? next.GetString() : null;
                }
            }
            return [.. count.OrderByDescending(kv => kv.Value).Take(maxCells).Select(kv => kv.Key)];
        }

        static int SectionOf(MorphologyNode node) => (int)Math.Round(node.UnscaledZ);

        /// <summary>Applies each variant to <paramref name="root"/> in turn and scores the links that touch a damaged section.</summary>
        static Dictionary<string, Tally> Score(MorphologyGraph root, NeighborResidualField field, SortedSet<int> damaged)
        {
            List<MorphologyGraph> cells = [.. root.Subgraphs.Values.Where(g => g.StructureID != 0)];
            if (cells.Count == 0)
                cells = [root];
            List<MorphologyNode> nodes = [.. cells.SelectMany(c => c.Nodes.Values)];
            Dictionary<ulong, SqlGeometry> original = nodes.ToDictionary(nd => nd.Key, nd => nd.Geometry);

            MorphologyGraph.ApplyResidualField(root, field);
            Dictionary<ulong, SqlGeometry> fieldGeometry = nodes.ToDictionary(nd => nd.Key, nd => nd.Geometry);
            HashSet<ulong> onDamaged = [.. nodes.Where(nd => damaged.Contains(SectionOf(nd))).Select(nd => nd.Key)];

            List<(MorphologyNode A, MorphologyNode B)> pairs = [.. cells.SelectMany(c => c.Edges.Values
                .Select(e => (c.Nodes[e.SourceNodeKey], c.Nodes[e.TargetNodeKey]))
                .Where(p => SectionOf(p.Item1) != SectionOf(p.Item2) && (onDamaged.Contains(p.Item1.Key) || onDamaged.Contains(p.Item2.Key))))];

            Dictionary<(ulong, ulong), double> fieldOverlap = [];
            Dictionary<string, Tally> result = [];
            foreach (string variant in new[] { "field", "curvefit", "damagedfit", "linear" })
            {
                foreach (MorphologyNode nd in nodes)
                    nd.Geometry = fieldGeometry[nd.Key];
                if (variant == "curvefit")
                {
                    foreach (MorphologyGraph c in cells)
                        MorphologyGraph.CurveFitProcesses(c, new MorphologyGraph.CurveFitOptions(MorphologyGraph.DefaultCurveFitHalfWindow, null, onDamaged));
                }
                else if (variant == "damagedfit")
                {
                    // What the service runs from 1.7.0: fit only damaged-section locations, through undamaged ones only.
                    HashSet<int> damagedSet = [.. damaged];
                    foreach (MorphologyGraph c in cells)
                        MorphologyGraph.CurveFitProcesses(c, new MorphologyGraph.CurveFitOptions(MorphologyGraph.DefaultCurveFitHalfWindow, null, null, damagedSet));
                }
                else if (variant == "linear")
                {
                    foreach (MorphologyGraph c in cells)
                        Linear(c, damaged);
                }

                Tally t = new();
                foreach (MorphologyNode nd in nodes.Where(nd => onDamaged.Contains(nd.Key)))
                {
                    if ((nd.Geometry.Centroid() - fieldGeometry[nd.Key].Centroid()).Magnitude > 1000)
                        t.Moved1um++;
                }
                foreach ((MorphologyNode a, MorphologyNode b) in pairs)
                {
                    double? o = Overlap(a.Geometry, b.Geometry);
                    if (o is null)
                        continue;
                    t.Pairs++;
                    t.Sum += o.Value;
                    if (variant == "field")
                    {
                        fieldOverlap[(a.Key, b.Key)] = o.Value;
                    }
                    else if (fieldOverlap.TryGetValue((a.Key, b.Key), out double f))
                    {
                        if (o.Value > f + 0.1)
                            t.Gain++;
                        else if (o.Value < f - 0.1)
                            t.Lose++;
                    }
                }
                result[variant] = t;
            }

            foreach (MorphologyNode nd in nodes)
                nd.Geometry = original[nd.Key];
            return result;
        }

        /// <summary>Moves each damaged-section location's centroid onto the line between its process's nearest undamaged locations.</summary>
        static void Linear(MorphologyGraph cell, SortedSet<int> damaged)
        {
            foreach (ulong[] process in cell.Processes())
            {
                MorphologyNode[] chain = [.. process.Select(id => cell.Nodes[id])];
                Vector2[] centroid = [.. chain.Select(nd => nd.Center.XY())];
                for (int i = 1; i + 1 < chain.Length; i++)
                {
                    if (!damaged.Contains(SectionOf(chain[i])))
                        continue;
                    int below = i - 1, above = i + 1;
                    while (below >= 0 && damaged.Contains(SectionOf(chain[below])))
                        below--;
                    while (above < chain.Length && damaged.Contains(SectionOf(chain[above])))
                        above++;
                    if (below < 0 || above >= chain.Length)
                        continue;
                    double alpha = (chain[i].Z - chain[below].Z) / (chain[above].Z - chain[below].Z);
                    Vector2 target = centroid[below] * (1 - alpha) + centroid[above] * alpha;
                    chain[i].Geometry = chain[i].Geometry.Translate(target - centroid[i]);
                }
            }
        }

        /// <summary>Shared area over the smaller area, or null when either shape has no area.</summary>
        static double? Overlap(SqlGeometry a, SqlGeometry b)
        {
            try
            {
                double areaA = a.STArea().Value, areaB = b.STArea().Value;
                double smaller = Math.Min(areaA, areaB);
                if (smaller <= 0)
                    return null;
                return a.STIntersection(b).STArea().Value / smaller;
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or System.Data.SqlTypes.SqlNullValueException)
            {
                return null;
            }
        }
    }
}
