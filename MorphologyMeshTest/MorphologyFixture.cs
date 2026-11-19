using AnnotationVizLib;
using Microsoft.SqlServer.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using UnitsAndScale;
using Viking.AnnotationServiceTypes.Interfaces;

namespace MorphologyMeshTest
{
    /// <summary>
    /// A saved piece of one structure's morphology (locations, volume shapes in nm, location links, scale), so mesh
    /// tests built on real annotations run without the OData server. <see cref="Save"/> writes what a live load
    /// returned; <see cref="Load"/> rebuilds the graph from the file in Testdata.
    /// </summary>
    internal static class MorphologyFixture
    {
        private sealed record AxisRecord(double Value, string Units);

        private sealed record NodeRecord(
            ulong Id, ulong ParentId, long Z, int TypeCode, bool Terminal, bool OffEdge, bool VericosityCap,
            bool Untraceable, double? Width, string VolumeWkt);

        private sealed record FixtureRecord(
            string Source, string Captured, ulong StructureId, AxisRecord X, AxisRecord Y, AxisRecord Z,
            List<NodeRecord> Nodes, List<ulong[]> Links);

        private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

        /// <summary>Path of a fixture in the output folder, where the tests read it.</summary>
        public static string OutputPath(string name) => Path.Combine(AppContext.BaseDirectory, "Testdata", name);

        /// <summary>Path of a fixture in this project's source Testdata folder, where a capture writes it.</summary>
        public static string SourcePath(string name, [CallerFilePath] string callerFile = "") =>
            Path.Combine(Path.GetDirectoryName(callerFile), "Testdata", name);

        public static void Save(MorphologyGraph graph, string path, string source)
        {
            IScale scale = graph.scale;
            List<NodeRecord> nodes = [.. graph.Nodes.Values.OrderBy(n => n.ID).Select(n => new NodeRecord(
                n.ID, n.Location.ParentID, n.Location.UnscaledZ, (int)n.Location.TypeCode, n.Location.Terminal,
                n.Location.OffEdge, n.Location.IsVericosityCap, n.Location.IsUntraceable, n.Location.Width,
                n.Geometry?.STAsText().ToSqlString().Value))];
            // MorphologyEdge stores the lower id as the source, and the graph keeps one edge per pair.
            List<ulong[]> links = [.. graph.Edges.Keys
                .Select(e => new[] { e.SourceNodeKey, e.TargetNodeKey })
                .OrderBy(l => l[0]).ThenBy(l => l[1])];

            FixtureRecord record = new(source, DateTime.UtcNow.ToString("yyyy-MM-dd"), graph.StructureID,
                new AxisRecord(scale.X.Value, scale.X.Units), new AxisRecord(scale.Y.Value, scale.Y.Units),
                new AxisRecord(scale.Z.Value, scale.Z.Units), nodes, links);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonSerializer.Serialize(record, Options));
        }

        public static MorphologyGraph Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Morphology fixture is missing: {path}", path);

            FixtureRecord record = JsonSerializer.Deserialize<FixtureRecord>(File.ReadAllText(path), Options)
                ?? throw new InvalidDataException($"Empty morphology fixture: {path}");

            Scale scale = new(new AxisUnits(record.X.Value, record.X.Units), new AxisUnits(record.Y.Value, record.Y.Units),
                new AxisUnits(record.Z.Value, record.Z.Units));
            MorphologyGraph graph = new(record.StructureId, scale);

            foreach (NodeRecord n in record.Nodes)
            {
                TestLocation location = new()
                {
                    ID = n.Id,
                    ParentID = n.ParentId,
                    UnscaledZ = n.Z,
                    Z = n.Z * scale.Z.Value,
                    TypeCode = (LocationType)n.TypeCode,
                    Terminal = n.Terminal,
                    OffEdge = n.OffEdge,
                    IsVericosityCap = n.VericosityCap,
                    IsUntraceable = n.Untraceable,
                    Width = n.Width,
                    VolumeGeometryWKT = n.VolumeWkt,
                };
                graph.AddNode(new MorphologyNode(n.Id, location, graph));
            }

            foreach (ulong[] link in record.Links)
                graph.AddEdge(new MorphologyEdge(graph, link[0], link[1]));

            return graph;
        }
    }
}
